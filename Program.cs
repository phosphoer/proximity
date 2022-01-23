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

        float elapsedTime = 0;

        while (true)
        {
          float dt = (float)TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds).TotalSeconds;
          timer.Restart();

          if (_players.Count > 1)
          {
            string ownerPlayerId = _proximityChat.GetPlayerGameId(_proximityChat.LobbyOwnerId);
            if (ownerPlayerId != null)
            {
              Player ownerPlayer = GetPlayer(ownerPlayerId);
              elapsedTime += dt;
              ownerPlayer.X = MathF.Sin(elapsedTime) * 10;
              _proximityChat.SetPlayerPosition(_proximityChat.LobbyOwnerId, ownerPlayer.X, ownerPlayer.Y, 0);

              Console.WriteLine($"Owner player pos.x = {ownerPlayer.X}");
            }
            else
            {
              Console.WriteLine($"Failed to find owner player with id {_proximityChat.LobbyOwnerId} and game id {ownerPlayerId}");
            }
          }

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
      Console.WriteLine($"Player connected: {userId}");

      Player player = new Player();
      player.Id = userId.ToString();

      _players.Add(player);

      if (userId == _proximityChat.UserId)
      {
        _proximityChat.SetPlayerGameId(player.Id);
      }
    }

    private void OnUserDisconnected(long userId)
    {
      Console.WriteLine($"Player disconnected: {userId}");

      string playerId = _proximityChat.GetPlayerGameId(userId);
      Player player = GetPlayer(playerId);
      if (player != null)
      {
        _players.Remove(player);
      }
    }

    private Player GetPlayer(string playerId)
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
    public string Id = string.Empty;
  }
}
