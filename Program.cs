using System;
using System.Threading;
using System.Text;

namespace proximity_mine
{
  class Program
  {
    private static readonly long kClientId = 926574841237209158;

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

      // Handle current user changing, can't get current user until this fires once
      userManager.OnCurrentUserUpdate += () =>
      {
        var currentUser = userManager.GetCurrentUser();
        Console.WriteLine("Got current discord user!");
        Console.WriteLine(currentUser.Username);
        Console.WriteLine(currentUser.Id);
      };

      // Create a lobby for our game
      var lobbyTxn = lobbyManager.GetLobbyCreateTransaction();
      lobbyTxn.SetCapacity(4);
      lobbyTxn.SetType(Discord.LobbyType.Private);

      lobbyManager.CreateLobby(lobbyTxn, (Discord.Result result, ref Discord.Lobby lobby) =>
      {
        UpdateActivity(discord, lobby);
      });

      // When we join an activity, try to connect to the relevant lobby
      activityManager.OnActivityJoin += secret =>
      {
        Console.WriteLine($"OnActivityJoin {secret}");

        lobbyManager.ConnectLobbyWithActivitySecret(secret, (Discord.Result result, ref Discord.Lobby lobby) =>
        {
          Console.WriteLine("Connected to lobby: {0}", lobby.Id);

          UpdateActivity(discord, lobby);

          // Connect to the network of this lobby and send everyone a message
          lobbyManager.ConnectNetwork(lobby.Id);
          lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
          foreach (var user in lobbyManager.GetMemberUsers(lobby.Id))
          {
            lobbyManager.SendNetworkMessage(lobby.Id, user.Id, 0, Encoding.UTF8.GetBytes(String.Format("Hello, {0}!", user.Username)));
          }
        });
      };

      lobbyManager.OnMemberConnect += (lobbyID, userID) =>
      {
        Console.WriteLine("user {0} connected to lobby: {1}", userID, lobbyID);
      };

      lobbyManager.OnLobbyMessage += (lobbyID, userID, data) =>
      {
        Console.WriteLine("lobby message: {0} {1}", lobbyID, Encoding.UTF8.GetString(data));
      };

      lobbyManager.OnNetworkMessage += (lobbyID, userID, channelID, data) =>
      {
        Console.WriteLine("channel message: {0} {1} {2}", lobbyID, channelID, Encoding.UTF8.GetString(data));
      };

      // Pump the event look to ensure all callbacks continue to get fired.
      try
      {
        while (true)
        {
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
