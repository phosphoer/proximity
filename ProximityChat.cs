using System;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

namespace ProximityMine
{
  public class ProximityChat
  {
    public static event System.Action<string> LogInfo;

    public event System.Action<long> UserConnected;
    public event System.Action<long> UserDisconnected;

    public long OwnerId => _currentLobbyOwnerId;

    private static readonly long kClientId = 926574841237209158;

    private bool _userInitialized = false;
    private long _currentUserId = 0;
    private long _otherUserId = 0;
    private long _currentLobbyId = 0;
    private long _currentLobbyOwnerId = 0;
    private uint _lobbyCapacity = 4;
    private Discord.Discord _discord;
    private Stopwatch _frameTimer = new Stopwatch();
    private System.Timers.Timer _timer = new System.Timers.Timer();
    private List<Player> _players = new List<Player>();


    private class Player
    {
      public float X = 0;
      public float Y = 0;
      public float Z = 0;
      public long Id = 0;

      public Player(long id)
      {
        Id = id;
      }
    }

    public void Initialize()
    {
      LogStringInfo("ProximityChat Initializing...");

      // Create discord api instance and set up logging
      _discord = new Discord.Discord(kClientId, (UInt64)Discord.CreateFlags.Default);
      _discord.SetLogHook(Discord.LogLevel.Info, (level, message) =>
      {
        LogStringInfo($"Discord: [{level}] {message}");
      });

      // Get managers we need
      var userManager = _discord.GetUserManager();
      var activityManager = _discord.GetActivityManager();
      var lobbyManager = _discord.GetLobbyManager();
      var voiceManager = _discord.GetVoiceManager();

      userManager.OnCurrentUserUpdate += OnCurrentUserUpdate;
      activityManager.OnActivityJoin += OnActivityJoin;
      lobbyManager.OnMemberConnect += OnMemberConnect;
      lobbyManager.OnLobbyMessage += OnLobbyMessage;
      lobbyManager.OnNetworkMessage += OnNetworkMessage;

      UserConnected += OnUserConnect;
      UserDisconnected += OnUserDisconnect;
    }

    public void Update()
    {
      // float dt = _frameTimer.ElapsedTicks / (float)TimeSpan.TicksPerSecond;
      // _frameTimer.Restart();

      Player localPlayer = GetPlayer(OwnerId);
      if (localPlayer != null)
      {
        var voiceManager = _discord.GetVoiceManager();

        // Local player is always player 0
        for (int i = 1; i < _players.Count; ++i)
        {
          Player remotePlayer = _players[i];
          float xDelta = (remotePlayer.X - localPlayer.X);
          float yDelta = (remotePlayer.Y - localPlayer.Y);
          float zDelta = (remotePlayer.Z - localPlayer.Z);
          float distToPlayer = MathF.Sqrt(xDelta * xDelta + yDelta * yDelta + zDelta * zDelta);
          float volume = 1.0f - (distToPlayer / 10.0f);

          voiceManager.SetLocalVolume(remotePlayer.Id, (byte)(volume * 200));
        }
      }

      // Pump the event look to ensure all callbacks continue to get fired.
      _discord.RunCallbacks();
    }

    public void SetLobbyCapacity(uint capacity)
    {
      _lobbyCapacity = capacity;
      if (_currentLobbyId != 0)
      {
        var updateTxn = _discord.GetLobbyManager().GetLobbyUpdateTransaction(_currentLobbyId);
        updateTxn.SetCapacity(_lobbyCapacity);
      }
    }

    public void SetPlayerPosition(long playerId, float x, float y, float z)
    {
      var player = GetPlayer(playerId);
      player.X = x;
      player.Y = y;
      player.Z = z;
    }

    public void Dispose()
    {
      _discord.Dispose();
    }

    private void UpdateActivity(Discord.Lobby lobby)
    {
      _currentLobbyId = lobby.Id;
      _currentLobbyOwnerId = lobby.OwnerId;

      // Get the special activity secret
      var secret = _discord.GetLobbyManager().GetLobbyActivitySecret(lobby.Id);

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
            CurrentSize = _discord.GetLobbyManager().MemberCount(lobby.Id),
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

    // Handle current user changing, can't get current user until this fires once
    private void OnCurrentUserUpdate()
    {
      if (_currentUserId != 0)
        UserDisconnected?.Invoke(_currentUserId);

      _userInitialized = true;

      var currentUser = _discord.GetUserManager().GetCurrentUser();
      LogStringInfo("Got current discord user!");
      LogStringInfo(currentUser.Username);
      LogStringInfo(currentUser.Id.ToString());

      _currentUserId = currentUser.Id;
      _discord.GetVoiceManager().SetSelfMute(false);

      UserConnected?.Invoke(_currentUserId);

      // Create a lobby for our local game
      var lobbyManager = _discord.GetLobbyManager();
      var lobbyTxn = lobbyManager.GetLobbyCreateTransaction();
      lobbyTxn.SetCapacity(_lobbyCapacity);
      lobbyTxn.SetType(Discord.LobbyType.Private);

      lobbyManager.CreateLobby(lobbyTxn, (Discord.Result result, ref Discord.Lobby lobby) =>
      {
        UpdateActivity(lobby);

        // Connect to the network of this lobby and send everyone a message
        lobbyManager.ConnectNetwork(lobby.Id);
        lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
        lobbyManager.ConnectVoice(lobby.Id, voiceResult =>
        {
          LogStringInfo($"Connect to voice: {voiceResult}");
        });
      });
    }

    private void OnActivityJoin(string secret)
    {
      LogStringInfo($"OnActivityJoin {secret}");

      // When we join an activity, try to connect to the relevant lobby
      var lobbyManager = _discord.GetLobbyManager();
      lobbyManager.ConnectLobbyWithActivitySecret(secret, (Discord.Result result, ref Discord.Lobby lobby) =>
      {
        LogStringInfo($"Connected to lobby: {lobby.Id}");

        UpdateActivity(lobby);

        // Connect to the network of this lobby and send everyone a message
        lobbyManager.ConnectNetwork(lobby.Id);
        lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
        lobbyManager.ConnectVoice(lobby.Id, voiceResult =>
        {
          LogStringInfo($"Connect to voice: {voiceResult}");
        });

        var userManager = _discord.GetUserManager();
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
    }

    private void OnMemberConnect(long lobbyID, long userID)
    {
      LogStringInfo($"user {userID} connected to lobby: {lobbyID}");
      UserConnected?.Invoke(userID);
    }

    private void OnMemberDisconnect(long lobbyID, long userID)
    {
      LogStringInfo($"user {userID} disconnected to lobby: {lobbyID}");
      UserDisconnected?.Invoke(userID);
    }

    private void OnLobbyMessage(long lobbyID, long userID, byte[] data)
    {
      LogStringInfo($"lobby message: {userID} {Encoding.UTF8.GetString(data)}");
    }

    private void OnNetworkMessage(long lobbyID, long userID, byte channelID, byte[] data)
    {
      LogStringInfo($"channel message: {lobbyID} {channelID} {Encoding.UTF8.GetString(data)}");
    }

    private void OnUserConnect(long userId)
    {
      Player player = new Player(userId);
      _players.Add(player);
    }

    private void OnUserDisconnect(long userId)
    {
      Player player = GetPlayer(userId);
      if (player != null)
      {
        _players.Remove(player);
      }
    }

    private Player GetPlayer(long playerId)
    {
      for (int i = 0; i < _players.Count; ++i)
      {
        if (_players[i].Id == playerId)
          return _players[i];
      }

      return null;
    }
  }
}
