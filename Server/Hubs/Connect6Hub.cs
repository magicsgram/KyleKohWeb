﻿using System;
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

    static Dictionary<String, HashSet<String>> connections;
    static Dictionary<String, String> reverseMapping;
    static Queue<String> serverLogsQueue;
    static LiteDB.LiteDatabase liteDatabase;
    static LiteDB.ILiteCollection<GameSession> gameSessionCollection;
    static LiteDB.ILiteCollection<SessionStat> sessionStatsCollection;
    static SessionStat sessionStat;

    public Connect6Hub() : base()
    {
      if (!initialized)
      {
        connections = new();
        reverseMapping = new();
        serverLogsQueue = new();

        String dbPath = Path.Combine(Directory.GetParent(".").FullName, "gamedb.db");
        liteDatabase = new LiteDB.LiteDatabase($"Filename = {dbPath};");

        sessionStatsCollection = liteDatabase.GetCollection<SessionStat>("SessionStats");
        if (sessionStatsCollection.Count() == 0)
        {
          sessionStat = new();
          sessionStatsCollection.Insert(sessionStat);
        }
        else
        {
          sessionStat = sessionStatsCollection.FindAll().First();
        }

        gameSessionCollection = liteDatabase.GetCollection<GameSession>("GameSessions");
        gameSessionCollection.EnsureIndex(x => x.GameId);

        CleanDB();
        initialized = true;
      }
    }

    public async Task CreateNewGame()
    {
      DateTime cutoffTime = DateTime.Now - TimeSpan.FromDays(365);
      List<GameSession> oldGameSessions = gameSessionCollection.Find(x => x.SessionUpdatedAt < cutoffTime).ToList();
      HashSet<String> gameIdsToRemove = oldGameSessions.Select(x => x.GameId).ToHashSet();
      foreach (var zeroConnection in connections.Where(x => x.Value.Count == 0))
        if (!gameIdsToRemove.Contains(zeroConnection.Key))
          gameIdsToRemove.Add(zeroConnection.Key);

      foreach (String gameIdToRemove in gameIdsToRemove)
        RemoveGameIdFromConnection(gameIdToRemove);
      gameSessionCollection.DeleteMany(x => gameIdsToRemove.Contains(x.GameId));

      // Do some db cleanup
      if ((DateTime.Now - sessionStat.LastDbCleaningAt) > TimeSpan.FromDays(7))
        CleanDB();

      String newGameId = "";
      do
      {
        Guid g = Guid.NewGuid();
        newGameId = g.ToString().Substring(0, 8);
      } while (gameSessionCollection.FindOne(x => x.GameId == newGameId) != null);

      GameSession newGameSession = new(newGameId);
      gameSessionCollection.Insert(newGameSession);
      connections.Add(newGameId, new HashSet<String>());
      ++sessionStat.TotalSessions;
      sessionStatsCollection.Update(sessionStat);

      await Clients.Caller.SendAsync("NewGameIdReceived", newGameId);
      await Report(newGameId, "New game made");
    }

    private static void RemoveGameIdFromConnection(string gameIdToRemove)
    {
      connections.Remove(gameIdToRemove);
      foreach (var keyValuePair in reverseMapping.ToList())
        if (keyValuePair.Value == gameIdToRemove)
          reverseMapping.Remove(keyValuePair.Key);
    }

    private static void CleanDB()
    {
      liteDatabase.Checkpoint();
      liteDatabase.Rebuild();
      sessionStat.LastDbCleaningAt = DateTime.Now;
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
      ++sessionStat.TotalConnections;
      if (connections[currentGameSession.GameId].Count == 2)
        ++sessionStat.TotalMultiplayerGame;
      sessionStatsCollection.Update(sessionStat);
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
        gameSessionCollection.Update(currentGameSession);
      await SendCurrentStateAsync(currentGameSession, result ? "placeStone" : "");
      await Report(currentGameSession.GameId, $"User placed stone ({x:D2}, {y:D2})");
    }

    public async Task<GameSession> FindGameAndHandleNoGameFound(String gameId)
    {
      GameSession currentGameSession = gameSessionCollection.FindOne(x => x.GameId == gameId);
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
      gameSessionCollection.Update(currentGameSession);
      await SendCurrentStateAsync(currentGameSession);
      await Report(currentGameSession.GameId, "User undid");
    }

    public async Task NewGame(String gameId)
    {
      GameSession currentGameSession = await FindGameAndHandleNoGameFound(gameId);
      if (currentGameSession == null)
        return;
      currentGameSession.ResetBoard();
      gameSessionCollection.Update(currentGameSession);
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
          if (connections[gameId].Count == 0)
            connections.Remove(gameId);

          await SendConnectionSize(gameId);
          await Report(gameId, "User disconnected");
        }
        catch { }
      }
    }

    private async Task Report(String gameId, String message)
    {
      if (gameId.Length > 0 && message.Length > 0)
      {
        String reportMessage = $"{DateTime.Now} [{sessionStat.TotalSessions} TS, {sessionStat.TotalConnections} TU, {sessionStat.TotalMultiplayerGame} MUS, {gameSessionCollection.Query().Count()} CS, {reverseMapping.Count} CU] {gameId} ({connections[gameId].Count}) : {message,-30}{Context.ConnectionId}";
        while (serverLogsQueue.Count > 30)
          serverLogsQueue.Dequeue();
        serverLogsQueue.Enqueue(reportMessage);
      }
      await Clients.Group("AdminAdminAdmin").SendAsync("ServerLogReceived", serverLogsQueue.ToList());
    }
  }
}
