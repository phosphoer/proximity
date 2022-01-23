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

    public long LobbyOwnerId => _currentLobbyOwnerId;
    public long UserId => _currentUserId;
    public int UserCount => _players.Count;

    private static readonly long kClientId = 926574841237209158;

    private bool _userInitialized = false;
    private long _currentUserId = 0;
    private long _currentLobbyId = 0;
    private long _currentLobbyOwnerId = 0;
    private bool _isJoiningLobby = false;
    private uint _lobbyCapacity = 4;
    private string _playerGameId = null;
    private Discord.Discord _discord;
    private List<Player> _players = new List<Player>();

    private class Player
    {
      public float X = 0;
      public float Y = 0;
      public float Z = 0;
      public long DiscordId = 0;
      public string GameId = null;

      public Player(long id)
      {
        DiscordId = id;
      }
    }

    public void Initialize()
    {
      LogStringInfo("ProximityChat Initializing...");

      // Create discord api instance and set up logging
      _discord = new Discord.Discord(kClientId, (UInt64)Discord.CreateFlags.Default);
      _discord.SetLogHook(Discord.LogLevel.Info, OnDiscordLog);

      // Get managers we need
      var userManager = _discord.GetUserManager();
      var activityManager = _discord.GetActivityManager();
      var lobbyManager = _discord.GetLobbyManager();
      var voiceManager = _discord.GetVoiceManager();

      userManager.OnCurrentUserUpdate += OnCurrentUserUpdate;
      activityManager.OnActivityJoin += OnActivityJoin;
      lobbyManager.OnMemberConnect += OnMemberConnect;
      lobbyManager.OnMemberDisconnect += OnMemberDisconnect;
      lobbyManager.OnLobbyMessage += OnLobbyMessage;
      lobbyManager.OnNetworkMessage += OnNetworkMessage;
    }

    public void Update()
    {
      Player localPlayer = GetPlayer(LobbyOwnerId);
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
          float distToPlayer = (float)Math.Sqrt(xDelta * xDelta + yDelta * yDelta + zDelta * zDelta);
          float volume = 1.0f - (distToPlayer / 10.0f);

          voiceManager.SetLocalVolume(remotePlayer.DiscordId, (byte)(volume * 200));
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

    public void SetPlayerGameId(string playerGameId)
    {
      LogStringInfo($"Set local player game Id: {playerGameId}");

      if (_players.Count == 0)
      {
        _playerGameId = playerGameId;
      }
      else if (_playerGameId != playerGameId)
      {
        GetPlayer(_currentUserId).GameId = playerGameId;
        _playerGameId = playerGameId;
        if (_currentLobbyId != 0)
        {
          var lobbyManager = _discord.GetLobbyManager();
          lobbyManager.SendLobbyMessage(_currentLobbyId, Encoding.UTF8.GetBytes(_playerGameId), OnLobbySendMessageResult);
        }
      }
    }

    public string GetPlayerGameId(long playerDiscordId)
    {
      Player player = GetPlayer(playerDiscordId);
      return player.GameId;
    }

    public long GetPlayerDiscordId(string playerGameId)
    {
      Player player = GetPlayer(playerGameId);
      return player.DiscordId;
    }

    public long GetPlayerDiscordId(int userIndex)
    {
      return _players[userIndex].DiscordId;
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

      LogStringInfo($"Updating activity for lobby {_currentLobbyId}");
      LogStringInfo($"Lobby owner Id is {_currentLobbyOwnerId}");

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
      _discord.GetActivityManager().UpdateActivity(activity, OnActivityUpdateResult);
    }

    private void LogStringInfo(string logStr)
    {
      Console.WriteLine(logStr);
      LogInfo?.Invoke(logStr);
    }

    private Player GetPlayer(long playerDiscordId)
    {
      for (int i = 0; i < _players.Count; ++i)
      {
        if (_players[i].DiscordId == playerDiscordId)
          return _players[i];
      }

      return null;
    }

    private Player GetPlayer(string playerGameId)
    {
      for (int i = 0; i < _players.Count; ++i)
      {
        if (_players[i].GameId == playerGameId)
          return _players[i];
      }

      return null;
    }

    private void CreateDefaultLobby()
    {
      // Create a lobby for our local game
      var lobbyManager = _discord.GetLobbyManager();
      var lobbyTxn = lobbyManager.GetLobbyCreateTransaction();
      lobbyTxn.SetCapacity(_lobbyCapacity);
      lobbyTxn.SetType(Discord.LobbyType.Private);

      lobbyManager.CreateLobby(lobbyTxn, OnLobbyCreateResult);
    }

    private void OnDiscordLog(Discord.LogLevel level, string message)
    {
      LogStringInfo($"Discord: [{level}] {message}");
    }

    private void OnLobbySendMessageResult(Discord.Result lobbyResult)
    {
      LogStringInfo($"Sent lobby player game ID: {lobbyResult}");
    }

    private void OnLobbyCreateResult(Discord.Result result, ref Discord.Lobby lobby)
    {
      LogStringInfo($"Lobby Create Result: {result}");

      // If the lobby failed to create for some reason - try again
      if (result != Discord.Result.Ok)
      {
        CreateDefaultLobby();
        return;
      }

      UpdateActivity(lobby);

      // Connect to the network of this lobby and send everyone a message
      var lobbyManager = _discord.GetLobbyManager();
      lobbyManager.ConnectNetwork(lobby.Id);
      lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
      lobbyManager.ConnectVoice(lobby.Id, OnVoiceConnectResult);
    }

    private void OnConnectLobbyResult(Discord.Result result, ref Discord.Lobby lobby)
    {
      _isJoiningLobby = false;
      LogStringInfo($"Lobby Join Result: {result}");
      if (result != Discord.Result.Ok)
      {
        return;
      }

      LogStringInfo($"Connected to lobby: {lobby.Id}");
      UpdateActivity(lobby);

      // Connect to the network of this lobby and send everyone a message
      var lobbyManager = _discord.GetLobbyManager();
      lobbyManager.ConnectNetwork(lobby.Id);
      lobbyManager.OpenNetworkChannel(lobby.Id, 0, true);
      lobbyManager.ConnectVoice(lobby.Id, OnVoiceConnectResult);

      var userManager = _discord.GetUserManager();
      var localUser = userManager.GetCurrentUser();
      foreach (var user in lobbyManager.GetMemberUsers(lobby.Id))
      {
        if (user.Id != localUser.Id)
        {
          OnUserConnect(user.Id);
        }
      }

      if (_playerGameId != null)
      {
        LogStringInfo($"Sending player game ID to lobby");
        lobbyManager.SendLobbyMessage(_currentLobbyId, Encoding.UTF8.GetBytes(_playerGameId), OnLobbySendMessageResult);
      }
    }

    private void OnActivityUpdateResult(Discord.Result result)
    {
      LogStringInfo($"Update Activity result: {result}");
    }

    private void OnVoiceConnectResult(Discord.Result voiceResult)
    {
      LogStringInfo($"Connect to voice result: {voiceResult}");
    }

    // Handle current user changing, can't get current user until this fires once
    private void OnCurrentUserUpdate()
    {
      if (_currentUserId != 0)
        UserDisconnected?.Invoke(_currentUserId);

      _userInitialized = true;

      var currentUser = _discord.GetUserManager().GetCurrentUser();
      LogStringInfo($"Current user updated: {currentUser.Username} {currentUser.Id}");

      _currentUserId = currentUser.Id;
      _discord.GetVoiceManager().SetSelfMute(false);

      OnUserConnect(_currentUserId);
      CreateDefaultLobby();
    }

    private void OnActivityJoin(string secret)
    {
      if (_isJoiningLobby)
        return;

      LogStringInfo($"OnActivityJoin {secret}");

      // When we join an activity, try to connect to the relevant lobby
      _isJoiningLobby = true;
      var lobbyManager = _discord.GetLobbyManager();
      lobbyManager.ConnectLobbyWithActivitySecret(secret, OnConnectLobbyResult);
    }

    private void OnMemberConnect(long lobbyID, long userID)
    {
      LogStringInfo($"User {userID} connected to lobby: {lobbyID}");
      OnUserConnect(userID);

      if (!string.IsNullOrEmpty(_playerGameId))
      {
        var lobbyManager = _discord.GetLobbyManager();
        lobbyManager.SendNetworkMessage(lobbyID, userID, 0, Encoding.UTF8.GetBytes(_playerGameId));
      }
    }

    private void OnMemberDisconnect(long lobbyID, long userID)
    {
      LogStringInfo($"User {userID} disconnected from lobby: {lobbyID}");
      OnUserDisconnect(userID);
    }

    private void OnLobbyMessage(long lobbyID, long userID, byte[] data)
    {
      string playerGameId = Encoding.UTF8.GetString(data);
      LogStringInfo($"Got lobby player game id: {userID} {playerGameId}");

      Player player = GetPlayer(userID);
      player.GameId = playerGameId;
    }

    private void OnNetworkMessage(long lobbyID, long userID, byte channelID, byte[] data)
    {
      string playerGameId = Encoding.UTF8.GetString(data);
      LogStringInfo($"Got network player game id: {userID} {playerGameId}");

      Player player = GetPlayer(userID);
      player.GameId = playerGameId;
    }

    private void OnUserConnect(long userId)
    {
      Player player = new Player(userId);
      _players.Add(player);

      if (userId == _currentUserId)
        player.GameId = _playerGameId;

      UserConnected?.Invoke(userId);
    }

    private void OnUserDisconnect(long userId)
    {
      Player player = GetPlayer(userId);
      if (player != null)
      {
        _players.Remove(player);
      }

      UserDisconnected?.Invoke(userId);
    }
  }
}
