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
