using System;
using System.Threading;

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

        var voiceManager = discord.GetVoiceManager();
        voiceManager.SetSelfMute(true);

        Console.WriteLine($"self mute: {voiceManager.IsSelfMute()}");
      };


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
      };

      var lobbyManager = discord.GetLobbyManager();
      lobbyManager.OnMemberConnect += (lobbyID, userID) =>
      {
        Console.WriteLine("user {0} connected to lobby: {1}", userID, lobbyID);
      };

      var relationshipManager = discord.GetRelationshipManager();
      // It is important to assign this handle right away to get the initial relationships refresh.
      // This callback will only be fired when the whole list is initially loaded or was reset
      relationshipManager.OnRefresh += () =>
      {
        // Filter a user's relationship list to be just friends
        relationshipManager.Filter((ref Discord.Relationship relationship) => { return relationship.Type == Discord.RelationshipType.Friend; });
        // Loop over all friends a user has.
        Console.WriteLine("relationships updated: {0}", relationshipManager.Count());
        for (var i = 0; i < Math.Min(relationshipManager.Count(), 10); i++)
        {
          // Get an individual relationship from the list
          var r = relationshipManager.GetAt((uint)i);
          Console.WriteLine("relationships: {0} {1} {2} {3}", r.Type, r.User.Username, r.Presence.Status, r.Presence.Activity.Name);
        }
      };
      // All following relationship updates are delivered individually.
      // These are fired when a user gets a new friend, removes a friend, or a relationship's presence changes.
      relationshipManager.OnRelationshipUpdate += (ref Discord.Relationship r) =>
      {
        Console.WriteLine("relationship updated: {0} {1} {2} {3}", r.Type, r.User.Username, r.Presence.Status, r.Presence.Activity.Name);
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
