using System;
using System.Threading;
using System.Text;
using System.Diagnostics;

namespace proximity_mine
{
  class Program
  {
    static void Main(string[] args)
    {
      var proximityChat = new ProximityMine.ProximityChat();
      proximityChat.Initialize();

      // Pump the event look to ensure all callbacks continue to get fired.
      try
      {
        Stopwatch timer = new Stopwatch();
        timer.Start();

        while (true)
        {
          float dt = timer.ElapsedTicks / (float)TimeSpan.TicksPerSecond;
          timer.Restart();

          proximityChat.Update();
          Thread.Sleep(1000 / 60);
        }
      }
      finally
      {
        proximityChat.Dispose();
      }
    }
  }
}
