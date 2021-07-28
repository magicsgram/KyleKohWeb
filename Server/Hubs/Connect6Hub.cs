using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace KyleKoh.Server.Hubs
{
  public class Connect6Hub : Hub
  {
    static Boolean initialized = false;

    static UInt64 totalSessions = 0;
    static UInt64 totalConnections = 0;
    static UInt64 totalMultiplayerGame = 0;
    static readonly Dictionary<String, HashSet<String>> connections = new();
    static readonly Dictionary<String, String> reverseMapping = new();
    static readonly Queue<String> serverLogsQueue = new();
    static LiteDB.LiteDatabase liteDatabase;
    static LiteDB.ILiteCollection<GameSession> liteCollection;

    public Connect6Hub() : base()
    {
      if (!initialized)
      {
        String totalSessionsFileName = Path.Combine(Directory.GetParent(".").FullName, "totalSessions.dat");
        if (File.Exists(totalSessionsFileName))
        {
          StreamReader sr = new(totalSessionsFileName);
          totalSessions = UInt64.Parse(sr.ReadLine() as String);
          totalConnections = UInt64.Parse(sr.ReadLine() as String);
          totalMultiplayerGame = UInt64.Parse(sr.ReadLine() as String);
          sr.Close();
        }

        liteDatabase = new LiteDB.LiteDatabase("Filename = gamedb.db; Connection = shared");
        liteCollection = liteDatabase.GetCollection<GameSession>("GameSessions");
        liteCollection.EnsureIndex(x => x.GameId);
        foreach (GameSession gameSession in liteCollection.FindAll())
          connections.Add(gameSession.GameId, new HashSet<String>());

        initialized = true;
      }
    }

    public async Task CreateNewGame()
    {
      DateTime cutoffTime = DateTime.Now - TimeSpan.FromDays(200);
      List<GameSession> oldGameSessions = liteCollection.Find(x => x.SessionUpdatedAt < cutoffTime).ToList();
      HashSet<String> gameIdsToRemove = oldGameSessions.Select(x => x.GameId).ToHashSet();
      foreach (String gameIdToRemove in gameIdsToRemove)
      {
        connections.Remove(gameIdToRemove);
        foreach (var keyValuePair in reverseMapping.ToList())
          if (keyValuePair.Value == gameIdToRemove)
            reverseMapping.Remove(keyValuePair.Key);
        await Report(gameIdToRemove, "Session destroyed");
      }
      liteCollection.DeleteMany(x => gameIdsToRemove.Contains(x.GameId));

      String newGameId = "";
      do
      {
        Guid g = Guid.NewGuid();
        newGameId = g.ToString().Substring(0, 8);
      } while (liteCollection.FindOne(x => x.GameId == newGameId) != null);

      GameSession newGameSession = new(newGameId);
      liteCollection.Insert(newGameSession);
      connections.Add(newGameId, new HashSet<String>());
      ++totalSessions;
      await Clients.Caller.SendAsync("NewGameIdReceived", newGameId);
      await Report(newGameId, "New game made");
    }

    public async Task InitializeBoardAndConnection(String gameId)
    {
      GameSession currentGameSession = await FindGameAndHandleNoGameFound(gameId);
      if (currentGameSession == null)
        return;

      await Groups.AddToGroupAsync(Context.ConnectionId, currentGameSession.GameId);
      if (!connections[currentGameSession.GameId].Contains(Context.ConnectionId))
      {
        connections[currentGameSession.GameId].Add(Context.ConnectionId);
        reverseMapping.Add(Context.ConnectionId, currentGameSession.GameId);
      }
      await SendCurrentStateAsync(currentGameSession);
      await SendConnectionSize(currentGameSession.GameId);
      ++totalConnections;
      if (connections[currentGameSession.GameId].Count == 2)
        ++totalMultiplayerGame;
      await Report(currentGameSession.GameId, "New user connected to game");
    }

    public async Task RegisterAdminConnection()
    {
      await Groups.AddToGroupAsync(Context.ConnectionId, "AdminAdminAdmin");
      await Report("", "");
    }

    public async Task PlaceStone(String gameId, Int32 x, Int32 y)
    {
      GameSession currentGameSession = await FindGameAndHandleNoGameFound(gameId);
      if (currentGameSession == null)
        return;
      
      Boolean result = currentGameSession.PlaceStone(x, y);
      if (result)
        liteCollection.Update(currentGameSession);
      await SendCurrentStateAsync(currentGameSession, result ? "placeStone" : "");
      await Report(currentGameSession.GameId, $"User placed stone ({x:D2}, {y:D2})");
    }

    public async Task<GameSession> FindGameAndHandleNoGameFound(String gameId)
    {
      GameSession currentGameSession = liteCollection.FindOne(x => x.GameId == gameId);
      if (currentGameSession == null)
      {
        await Clients.Caller.SendAsync("NoGameFound", "");
        return null;
      }
      return currentGameSession;
    }

    public async Task UndoStone(String gameId)
    {
      GameSession currentGameSession = await FindGameAndHandleNoGameFound(gameId);
      if (currentGameSession == null)
        return;
      currentGameSession.UndoStone();
      liteCollection.Update(currentGameSession);
      await SendCurrentStateAsync(currentGameSession);
      await Report(currentGameSession.GameId, "User undid");
    }

    public async Task NewGame(String gameId)
    {
      GameSession currentGameSession = await FindGameAndHandleNoGameFound(gameId);
      if (currentGameSession == null)
        return;
      currentGameSession.ResetBoard();
      liteCollection.Update(currentGameSession);
      await SendCurrentStateAsync(currentGameSession);
      await Report(currentGameSession.GameId, "Board reset");
    }

    private async Task SendCurrentStateAsync(GameSession gameSession, String soundCue = "")
    {
      Dictionary<String, String> state = new()
      {
        { "currentTurn", gameSession.CurrentTurn().ToString() },
        { "currentTurnRemaining", gameSession.CurrentTurnRemaining().ToString() },
        { "boardString", gameSession.GetCurrentBoardAsString() },
        { "soundCue", soundCue }
      };

      if (gameSession.PlaysX.Count > 0)
      {
        var lastPlayX = gameSession.PlaysX.Last();
        var lastPlayY = gameSession.PlaysY.Last();
        state.Add("lastPlayX", lastPlayX.ToString());
        state.Add("lastPlayY", lastPlayY.ToString());
        if (gameSession.PlaysX.Count > 1)
        {
          Char lastTurn = gameSession.CurrentTurn(gameSession.PlaysX.Count - 1);
          Char lastLastTurn = gameSession.CurrentTurn(gameSession.PlaysX.Count - 2);
          if (lastTurn == lastLastTurn)
          {
            var lastLastPlayX = gameSession.PlaysX[^2];
            var lastLastPlayY = gameSession.PlaysY[^2];
            state.Add("lastLastPlayX", lastLastPlayX.ToString());
            state.Add("lastLastPlayY", lastLastPlayY.ToString());
          }
          else
          {
            state.Add("lastLastPlayX", (-1).ToString());
            state.Add("lastLastPlayY", (-1).ToString());
          }
        }
        else
        {
          state.Add("lastLastPlayX", (-1).ToString());
          state.Add("lastLastPlayY", (-1).ToString());
        }
      }
      else
      {
        state.Add("lastPlayX", (-1).ToString());
        state.Add("lastPlayY", (-1).ToString());
        state.Add("lastLastPlayX", (-1).ToString());
        state.Add("lastLastPlayY", (-1).ToString());
      }
      await Clients.Group(gameSession.GameId).SendAsync("CurrentBoard", state);
    }

    private async Task SendConnectionSize(String gameId) => await Clients.Group(gameId).SendAsync("ConnectionSize", connections[gameId].Count);

    public async override Task OnDisconnectedAsync(Exception exception)
    {
      if (reverseMapping.ContainsKey(Context.ConnectionId))
      {
        try
        {
          String gameId = reverseMapping[Context.ConnectionId];
          reverseMapping.Remove(Context.ConnectionId);
          connections[gameId].Remove(Context.ConnectionId);
          await SendConnectionSize(gameId);
          await Report(gameId, "User disconnected");
        }
        catch { }
      }
    }

    public void ParkAndExit(String adminKeyFromClient)
    {
      String adminKeyFileName = Path.Combine(Directory.GetParent(".").FullName, "adminKey.txt");
      if (File.Exists(adminKeyFileName))
      {
        StreamReader sr = new(adminKeyFileName);
        String adminKey = sr.ReadLine() as String;
        sr.Close();

        if (adminKeyFromClient == adminKey)
        {
          File.WriteAllText(Path.Combine(Directory.GetParent(".").FullName, "totalSessions.dat"), $"{totalSessions}\n{totalConnections}\n{totalMultiplayerGame}");
          Environment.Exit(0);
        }
      }
    }

    private async Task Report(String gameId, String message)
    {
      if (gameId.Length > 0 && message.Length > 0)
      {
        String reportMessage = $"{DateTime.Now} [{totalSessions} TS, {totalConnections} TU, {totalMultiplayerGame} MUS, {liteCollection.Query().Count()} CS, {reverseMapping.Count} CU] {gameId} ({connections[gameId].Count}) : {message,-30}{Context.ConnectionId}";
        while (serverLogsQueue.Count > 30)
          serverLogsQueue.Dequeue();
        serverLogsQueue.Enqueue(reportMessage);
      }
      await Clients.Group("AdminAdminAdmin").SendAsync("ServerLogReceived", serverLogsQueue.ToList());
    }
  }
}
