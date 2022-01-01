using System;
using System.Threading;
using System.Text;

namespace proximity_mine
{
  class Program
  {
    private static readonly long kClientId = 926574841237209158;

    static void Main(string[] args)
    {
      // Create discord api instance and set up logging
      var discord = new Discord.Discord(kClientId, (UInt64)Discord.CreateFlags.Default);
      discord.SetLogHook(Discord.LogLevel.Debug, (level, message) =>
      {
        Console.WriteLine("Log[{0}] {1}", level, message);
      });

      // Get user manager and prepare to receive current user info 
      var userManager = discord.GetUserManager();

      // The auth manager fires events as information about the current user changes.
      // This event will fire once on init.
      //
      // GetCurrentUser will error until this fires once.
      userManager.OnCurrentUserUpdate += () =>
      {
        var currentUser = userManager.GetCurrentUser();
        Console.WriteLine("Got current discord user!");
        Console.WriteLine(currentUser.Username);
        Console.WriteLine(currentUser.Id);
      };


      var lobbyManager = discord.GetLobbyManager();
      var activityManager = discord.GetActivityManager();
      var activity = new Discord.Activity
      {
        State = "Testing",
        Details = "Testing discord activities!",
        Timestamps =
        {
            Start = 5,
        },
        Party =
        {
            Id = "foo partyID",
            Size = {
                CurrentSize = 1,
                MaxSize = 4,
            },
        },
        Secrets =
        {
            Match = "foo matchSecret",
            Join = "foo joinSecret",
            Spectate = "foo spectateSecret",
        },
        Instance = true,
      };

      activityManager.UpdateActivity(activity, (result) =>
      {
        if (result == Discord.Result.Ok)
        {
          Console.WriteLine("Activity Success!");
        }
        else
        {
          Console.WriteLine("Activity Failed");
        }
      });

      activityManager.OnActivityJoin += secret =>
      {
        Console.WriteLine($"{secret}");

        lobbyManager.ConnectLobbyWithActivitySecret(secret, (Discord.Result result, ref Discord.Lobby lobby) =>
        {
          Console.WriteLine("Connected to lobby: {0}", lobby.Id);
          lobbyManager.ConnectNetwork(lobby.Id);
          lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
          foreach (var user in lobbyManager.GetMemberUsers(lobby.Id))
          {
            lobbyManager.SendNetworkMessage(lobby.Id, user.Id, 0,
                            Encoding.UTF8.GetBytes(String.Format("Hello, {0}!", user.Username)));
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
