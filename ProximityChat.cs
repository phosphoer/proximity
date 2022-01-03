using System;
using System.Text;
using System.Diagnostics;

namespace ProximityMine
{
  public class ProximityChat
  {
    public static event System.Action<string> LogInfo;

    private static readonly long kClientId = 926574841237209158;

    private bool _userInitialized = false;
    private long _otherUserId = 0;
    private Discord.Discord _discord;
    private Stopwatch _frameTimer = new Stopwatch();
    private System.Timers.Timer _timer = new System.Timers.Timer();

    private void UpdateActivity(Discord.Discord discord, Discord.Lobby lobby)
    {
      // Get the special activity secret
      var secret = discord.GetLobbyManager().GetLobbyActivitySecret(lobby.Id);

      // Create a new activity
      // Set the party id to the lobby id, so everyone in the lobby has the same value
      // Set the join secret to the special activity secret
      var activity = new Discord.Activity
      {
        Party =
        {
          Id = lobby.Id.ToString(),
          Size =
          {
            CurrentSize = discord.GetLobbyManager().MemberCount(lobby.Id),
            MaxSize = (int)lobby.Capacity
          }
        },
        Secrets =
        {
          Join = secret
        }
      };

      // Set this activity as our current one for the user
      // The activity + party info inside allows people to invite on discord
      _discord.GetActivityManager().UpdateActivity(activity, (result) =>
      {
        if (result == Discord.Result.Ok)
        {
          LogStringInfo($"Set activity success, join secret: {activity.Secrets.Join}");
        }
        else
        {
          LogStringInfo("Activity Failed");
        }
      });
    }

    private void LogStringInfo(string logStr)
    {
      Console.WriteLine(logStr);
      LogInfo?.Invoke(logStr);
    }

    public void Initialize()
    {
      LogStringInfo("ProximityChat Initializing...");

      // Create discord api instance and set up logging
      _discord = new Discord.Discord(kClientId, (UInt64)Discord.CreateFlags.Default);
      _discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
      {
        LogStringInfo($"Log[{level}] {message}");
      });

      // Get managers we need
      var userManager = _discord.GetUserManager();
      var activityManager = _discord.GetActivityManager();
      var lobbyManager = _discord.GetLobbyManager();
      var voiceManager = _discord.GetVoiceManager();

      // Handle current user changing, can't get current user until this fires once
      _discord.GetUserManager().OnCurrentUserUpdate += () =>
      {
        _userInitialized = true;

        var currentUser = _discord.GetUserManager().GetCurrentUser();
        LogStringInfo("Got current discord user!");
        LogStringInfo(currentUser.Username);
        LogStringInfo(currentUser.Id.ToString());

        _discord.GetVoiceManager().SetSelfMute(false);

        // Create a lobby for our game
        var lobbyTxn = lobbyManager.GetLobbyCreateTransaction();
        lobbyTxn.SetCapacity(4);
        lobbyTxn.SetType(Discord.LobbyType.Private);

        lobbyManager.CreateLobby(lobbyTxn, (Discord.Result result, ref Discord.Lobby lobby) =>
        {
          UpdateActivity(_discord, lobby);

          // Connect to the network of this lobby and send everyone a message
          lobbyManager.ConnectNetwork(lobby.Id);
          lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
          lobbyManager.ConnectVoice(lobby.Id, voiceResult =>
          {
            LogStringInfo($"Connect to voice: {voiceResult}");
          });
        });
      };

      // When we join an activity, try to connect to the relevant lobby
      activityManager.OnActivityJoin += secret =>
      {
        LogStringInfo($"OnActivityJoin {secret}");

        lobbyManager.ConnectLobbyWithActivitySecret(secret, (Discord.Result result, ref Discord.Lobby lobby) =>
        {
          LogStringInfo($"Connected to lobby: {lobby.Id}");

          UpdateActivity(_discord, lobby);

          // Connect to the network of this lobby and send everyone a message
          lobbyManager.ConnectNetwork(lobby.Id);
          lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
          lobbyManager.ConnectVoice(lobby.Id, voiceResult =>
          {
            LogStringInfo($"Connect to voice: {voiceResult}");
          });

          var localUser = userManager.GetCurrentUser();
          foreach (var user in lobbyManager.GetMemberUsers(lobby.Id))
          {
            LogStringInfo($"Sending network message to {user.Id}");
            lobbyManager.SendNetworkMessage(lobby.Id, user.Id, 0, Encoding.UTF8.GetBytes(String.Format("Hello, {0}!", user.Username)));

            if (user.Id != localUser.Id)
            {
              _otherUserId = user.Id;
              LogStringInfo($"Storing other user id {_otherUserId}");
            }
          }

          lobbyManager.SendLobbyMessage(lobby.Id, Encoding.UTF8.GetBytes($"Hello Lobby!"), lobbyResult =>
          {
            LogStringInfo($"Send lobby message result: {lobbyResult}");
          });
        });
      };

      lobbyManager.OnMemberConnect += (lobbyID, userID) =>
      {
        LogStringInfo($"user {userID} connected to lobby: {lobbyID}");
      };

      lobbyManager.OnLobbyMessage += (lobbyID, userID, data) =>
      {
        LogStringInfo($"lobby message: {userID} {Encoding.UTF8.GetString(data)}");
      };

      lobbyManager.OnNetworkMessage += (lobbyID, userID, channelID, data) =>
      {
        LogStringInfo($"channel message: {lobbyID} {channelID} {Encoding.UTF8.GetString(data)}");
      };
    }

    public void Update()
    {
      // Pump the event look to ensure all callbacks continue to get fired.
      // float dt = _frameTimer.ElapsedTicks / (float)TimeSpan.TicksPerSecond;
      // _frameTimer.Restart();

      _discord.RunCallbacks();
    }

    public void Dispose()
    {
      _discord.Dispose();
    }
  }
}
