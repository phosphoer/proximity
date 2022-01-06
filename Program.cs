using System;
using System.Threading;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

namespace proximity_mine
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var exampleGame = new ExampleGame();
      exampleGame.Initialize();
      exampleGame.GameLoop();
      exampleGame.Uninitialize();
    }
  }

  internal class ExampleGame
  {
    private List<Player> _players = new List<Player>();
    private ProximityMine.ProximityChat _proximityChat;

    public void Initialize()
    {
      _proximityChat = new ProximityMine.ProximityChat();
      _proximityChat.Initialize();

      _proximityChat.UserConnected += OnUserConnected;
      _proximityChat.UserDisconnected += OnUserDisconnected;
    }

    public void Uninitialize()
    {
      _proximityChat.UserConnected -= OnUserConnected;
      _proximityChat.UserDisconnected -= OnUserDisconnected;
    }

    public void GameLoop()
    {
      // Pump the event look to ensure all callbacks continue to get fired.
      try
      {
        Stopwatch timer = new Stopwatch();
        timer.Start();

        while (true)
        {
          float dt = timer.ElapsedTicks / (float)TimeSpan.TicksPerSecond;
          timer.Restart();

          _proximityChat.Update();
          Thread.Sleep(1000 / 60);
        }
      }
      finally
      {
        _proximityChat.Dispose();
      }
    }

    private void OnUserConnected(long userId)
    {
      Player player = new Player();
      player.Id = userId;

      _players.Add(player);

      Console.Write($"Player connected: {userId}");
    }

    private void OnUserDisconnected(long userId)
    {
      Player player = GetPlayer(userId);
      if (player != null)
      {
        _players.Remove(player);
      }

      Console.Write($"Player disconnected: {userId}");
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

  internal class Player
  {
    public float X = 0;
    public float Y = 0;
    public long Id = 0;
  }
}
