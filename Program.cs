using System;
using System.Threading;
using System.Text;
using System.Diagnostics;

namespace proximity_mine
{
  class Program
  {
    private static readonly long kClientId = 926574841237209158;
    private static bool _userInitialized = false;
    private static long _otherUserId = 0;

    private static void UpdateActivity(Discord.Discord discord, Discord.Lobby lobby)
    {
      // Get the special activity secret
      var activityManager = discord.GetActivityManager();
      var lobbyManager = discord.GetLobbyManager();
      var secret = lobbyManager.GetLobbyActivitySecret(lobby.Id);

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
            CurrentSize = lobbyManager.MemberCount(lobby.Id),
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
      activityManager.UpdateActivity(activity, (result) =>
      {
        if (result == Discord.Result.Ok)
        {
          Console.WriteLine($"Set activity success, join secret: {activity.Secrets.Join}");
        }
        else
        {
          Console.WriteLine("Activity Failed");
        }
      });
    }

    static void Main(string[] args)
    {
      // Create discord api instance and set up logging
      var discord = new Discord.Discord(kClientId, (UInt64)Discord.CreateFlags.Default);
      discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
      {
        Console.WriteLine("Log[{0}] {1}", level, message);
      });

      // Get managers we need
      var userManager = discord.GetUserManager();
      var activityManager = discord.GetActivityManager();
      var lobbyManager = discord.GetLobbyManager();
      var voiceManager = discord.GetVoiceManager();

      // Handle current user changing, can't get current user until this fires once
      userManager.OnCurrentUserUpdate += () =>
      {
        _userInitialized = true;

        var currentUser = userManager.GetCurrentUser();
        Console.WriteLine("Got current discord user!");
        Console.WriteLine(currentUser.Username);
        Console.WriteLine(currentUser.Id);

        voiceManager.SetSelfMute(false);
      };

      // Create a lobby for our game
      var lobbyTxn = lobbyManager.GetLobbyCreateTransaction();
      lobbyTxn.SetCapacity(4);
      lobbyTxn.SetType(Discord.LobbyType.Private);

      lobbyManager.CreateLobby(lobbyTxn, (Discord.Result result, ref Discord.Lobby lobby) =>
      {
        UpdateActivity(discord, lobby);

        // Connect to the network of this lobby and send everyone a message
        lobbyManager.ConnectNetwork(lobby.Id);
        lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
        lobbyManager.ConnectVoice(lobby.Id, result =>
        {
          Console.WriteLine($"Connect to voice: {result}");
        });
      });

      // When we join an activity, try to connect to the relevant lobby
      activityManager.OnActivityJoin += secret =>
      {
        Console.WriteLine($"OnActivityJoin {secret}");

        lobbyManager.ConnectLobbyWithActivitySecret(secret, (Discord.Result result, ref Discord.Lobby lobby) =>
        {
          Console.WriteLine($"Connected to lobby: {lobby.Id}");

          UpdateActivity(discord, lobby);

          // Connect to the network of this lobby and send everyone a message
          lobbyManager.ConnectNetwork(lobby.Id);
          lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
          lobbyManager.ConnectVoice(lobby.Id, result =>
          {
            Console.WriteLine($"Connect to voice: {result}");
          });

          var localUser = userManager.GetCurrentUser();
          foreach (var user in lobbyManager.GetMemberUsers(lobby.Id))
          {
            Console.WriteLine($"Sending network message to {user.Id}");
            lobbyManager.SendNetworkMessage(lobby.Id, user.Id, 0, Encoding.UTF8.GetBytes(String.Format("Hello, {0}!", user.Username)));

            if (user.Id != localUser.Id)
            {
              _otherUserId = user.Id;
              Console.WriteLine($"Storing other user id {_otherUserId}");
            }
          }

          lobbyManager.SendLobbyMessage(lobby.Id, Encoding.UTF8.GetBytes($"Hello Lobby!"), result =>
          {
            Console.WriteLine($"Send lobby message result: {result}");
          });
        });
      };

      lobbyManager.OnMemberConnect += (lobbyID, userID) =>
      {
        Console.WriteLine($"user {userID} connected to lobby: {lobbyID}");
      };

      lobbyManager.OnLobbyMessage += (lobbyID, userID, data) =>
      {
        Console.WriteLine($"lobby message: {userID} {Encoding.UTF8.GetString(data)}");
      };

      lobbyManager.OnNetworkMessage += (lobbyID, userID, channelID, data) =>
      {
        Console.WriteLine($"channel message: {lobbyID} {channelID} {Encoding.UTF8.GetString(data)}");
      };

      // Pump the event look to ensure all callbacks continue to get fired.
      try
      {
        Stopwatch timer = new Stopwatch();
        timer.Start();

        float volumeTimer = 0;

        while (true)
        {
          float dt = timer.ElapsedTicks / (float)TimeSpan.TicksPerSecond;
          timer.Restart();

          if (_otherUserId > 0)
          {
            volumeTimer += dt;
            float sinNormalized = (float)(Math.Sin(volumeTimer) * 0.5 + 0.5);
            float volume = sinNormalized * 200;
            voiceManager.SetLocalVolume(_otherUserId, (byte)volume);
          }

          discord.RunCallbacks();
          Thread.Sleep(1000 / 60);
        }
      }
      finally
      {
        discord.Dispose();
      }
    }
  }
}
