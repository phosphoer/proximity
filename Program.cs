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

  // This example 'game' shows a simple use case of the library 
  // with multi-user voice chat where the host player constantly moves
  // in and out of voice range 
  internal class ExampleGame
  {
    // The simple player class for our game, it has a position and an Id to identify it
    internal class Player
    {
      public float X = 0;
      public float Y = 0;
      public string Id = string.Empty;
    }

    private List<Player> _players = new List<Player>();
    private ProximityMine.ProximityChat _proximityChat;

    public void Initialize()
    {
      // Create and initialize proximity chat 
      _proximityChat = new ProximityMine.ProximityChat();
      _proximityChat.Initialize();

      // Listen to a few events 
      _proximityChat.UserConnected += OnUserConnected;
      _proximityChat.UserDisconnected += OnUserDisconnected;
      _proximityChat.UserGameIdUpdated += OnUserGameIdReceived;

      // Customize the distances at which voices will be the loudest and quietest respectively
      _proximityChat.VoiceMinDistance = 1;
      _proximityChat.VoiceMaxDistance = 10;
    }

    public void Uninitialize()
    {
      _proximityChat.UserConnected -= OnUserConnected;
      _proximityChat.UserDisconnected -= OnUserDisconnected;
    }

    public void GameLoop()
    {
      try
      {
        // Set up some timing info
        float elapsedTime = 0;
        Stopwatch timer = new Stopwatch();
        timer.Start();

        // Main game loop
        while (true)
        {
          float dt = (float)TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds).TotalSeconds;
          timer.Restart();

          MoveHostPlayerLeftRight(dt, elapsedTime);

          // We must tell proximity chat where each player is locally in order for positional
          // audio to work, proximity chat does not network the position data
          for (int i = 0; i < _players.Count; ++i)
          {
            // Proximity chat knows the mapping of discord IDs to player IDs, so we can 
            // get the player's discord ID here
            long playerDiscordId = _proximityChat.GetPlayerDiscordId(_players[i].Id);

            // Update the player position in proximity chat
            _proximityChat.SetPlayerPosition(playerDiscordId, _players[i].X, _players[i].Y, 0);
          }

          // We must call Update() on proximity chat each frame to pump the discord message loop and 
          // update player volumes based on their position
          _proximityChat.Update();

          // Aim for ~60fps
          Thread.Sleep(1000 / 60);
        }
      }
      finally
      {
        // Make sure to call Dispose on exit or session end to clean up the discord
        // resources
        _proximityChat.Dispose();
      }
    }

    // We get a user connect event for every user, including ourself, that connects to the discord lobby
    // In a normal game you may not need to do anything in response to this, unless you want to display 
    // a notification of some kind
    private void OnUserConnected(long userId)
    {
      Console.WriteLine($"Player connected: {userId}");

      if (userId == _proximityChat.UserId)
      {
        // A made up unique string that our 'game' uses to identify players
        string gameId = Guid.NewGuid().ToString();

        // We must tell proximity chat what the player game id is of the local player
        // This would be whatever unique identifier your game uses to identify
        // players in the multiplayer session
        _proximityChat.SetPlayerGameId(gameId);

        // In this case our game waits for the first user connect (ourselves) to create 
        // the local player, your game will likely have it's own way of doing this
        Player localPlayer = new Player();
        localPlayer.Id = gameId;
        _players.Add(localPlayer);
      }
    }

    // Typically the game would have it's own networking to inform other clients 
    // of our player's name but in this simple example we just rely on proximity chat's
    // game id sharing to accomplish this
    private void OnUserGameIdReceived(long userId, string gameId)
    {
      Console.WriteLine($"Player game ID received: {userId}:{gameId}");

      // Now that we have the game id we can create a player object to represent this user
      Player player = new Player();
      player.Id = gameId;
      _players.Add(player);
    }

    // This event occurs whenever a discord user disconnects, as with the connect event
    // there may be nothing for you to do here short of notifying the player
    private void OnUserDisconnected(long userId)
    {
      Console.WriteLine($"Player disconnected: {userId}");

      // Since we're piggy-backing off the networking of proximity chat in this game,
      // we use this event to remove the relevant player
      // Since we have the discord user Id, we can get the user's player game Id from that
      string playerId = _proximityChat.GetPlayerGameId(userId);
      Player player = GetPlayer(playerId);
      if (player != null)
      {
        _players.Remove(player);
      }
    }

    // Hopefully your game has a faster method of retrieving a player by their id
    private Player GetPlayer(string playerId)
    {
      for (int i = 0; i < _players.Count; ++i)
      {
        if (_players[i].Id == playerId)
          return _players[i];
      }

      return null;
    }

    // Some made up 'gameplay' code that moves the host player back and forth in and out of audio range
    // Note this movement is being done locally on all clients since this sample is too simple
    // to include an actual networked game
    private void MoveHostPlayerLeftRight(float dt, float elapsedTime)
    {
      if (_players.Count > 1)
      {
        string ownerPlayerId = _proximityChat.GetPlayerGameId(_proximityChat.LobbyOwnerId);
        if (ownerPlayerId != null)
        {
          Player ownerPlayer = GetPlayer(ownerPlayerId);
          elapsedTime += dt;
          ownerPlayer.X = MathF.Sin(elapsedTime) * _proximityChat.VoiceMaxDistance;

          Console.WriteLine($"Owner player pos.x = {ownerPlayer.X}");
        }
        else
        {
          Console.WriteLine($"Failed to find owner player with id {_proximityChat.LobbyOwnerId} and game id {ownerPlayerId}");
        }
      }
    }
  }
}
