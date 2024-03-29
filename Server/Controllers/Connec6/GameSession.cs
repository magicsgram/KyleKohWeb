using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KyleKoh.Server.Hubs
{
  public class GameSession
  {
    public Int64 Id { get; set; }

    public Int32 BoardSize { get; set; } = 19; // Always odd number. Never below 11

    public String GameId { get; set; }

    public Char[][] EmptyBoard { get; set; }

    public Char[][] CurrentBoard { get; set; }

    public List<Int32> PlaysX { get; set; } = new List<Int32>();
    public List<Int32> PlaysY { get; set; } = new List<Int32>();

    public DateTime SessionUpdatedAt { get; set; }

    public GameSession(String gameId)
    {
      GameId = gameId;
      EmptyBoard = new Char[BoardSize][];
      CurrentBoard = new Char[BoardSize][];

      // Default crosses
      for (Int32 j = 0; j < BoardSize; ++j)
      {
        EmptyBoard[j] = new Char[BoardSize];
        CurrentBoard[j] = new Char[BoardSize];
        for (Int32 i = 0; i < BoardSize; ++i)
          EmptyBoard[j][i] = '5';
      }
      // Straight Borders
      for (Int32 i = 0; i < BoardSize; ++i)
      {
        EmptyBoard[0][i] = '8';
        EmptyBoard[BoardSize - 1][i] = '2';
      }
      for (Int32 j = 0; j < BoardSize; ++j)
      {
        EmptyBoard[j][0] = '4';
        EmptyBoard[j][BoardSize - 1] = '6';
      }
      // Corners
      EmptyBoard[0][0] = '7';
      EmptyBoard[0][BoardSize - 1] = '9';
      EmptyBoard[BoardSize - 1][0] = '1';
      EmptyBoard[BoardSize - 1][BoardSize - 1] = '3';
      // Left dots
      EmptyBoard[3][3] = 'c';
      EmptyBoard[BoardSize / 2][3] = 'c';
      EmptyBoard[BoardSize - 4][3] = 'c';
      // Center dots
      EmptyBoard[3][BoardSize / 2] = 'c';
      EmptyBoard[BoardSize / 2][BoardSize / 2] = 'c';
      EmptyBoard[BoardSize - 4][BoardSize / 2] = 'c';
      // Right dots
      EmptyBoard[3][BoardSize - 4] = 'c';
      EmptyBoard[BoardSize / 2][BoardSize - 4] = 'c';
      EmptyBoard[BoardSize - 4][BoardSize - 4] = 'c';

      ResetBoard();
    }

    public void ResetBoard()
    {
      PlaysX.Clear();
      PlaysY.Clear();
      for (Int32 j = 0; j < BoardSize; ++j)
        for (Int32 i = 0; i < BoardSize; ++i)
          CurrentBoard[j][i] = EmptyBoard[j][i];
      SessionUpdatedAt = DateTime.Now;
    }

    public String GetCurrentBoardAsString()
    {
      StringBuilder boardString = new();
      for (Int32 j = 0; j < BoardSize; ++j)
      {
        for (Int32 i = 0; i < BoardSize; ++i)
          boardString.Append(CurrentBoard[j][i]);
        boardString.AppendLine("");
      }
      return boardString.ToString().Trim('\r', '\n').Replace("\r", "");
    }

    public Boolean PlaceStone(Int32 x, Int32 y)
    {
      SessionUpdatedAt = DateTime.Now;
      if (CurrentBoard[y][x] != 'w' && CurrentBoard[y][x] != 'b')
      {
        CurrentBoard[y][x] = CurrentTurn();
        PlaysX.Add(x);
        PlaysY.Add(y);
        return true;
      }
      return false;
    }

    public void UndoStone()
    {
      SessionUpdatedAt = DateTime.Now;
      if (PlaysX.Count > 0)
      {
        var lastCoordinateX = PlaysX.Last();
        var lastCoordinateY = PlaysY.Last();
        CurrentBoard[lastCoordinateY][lastCoordinateX] = EmptyBoard[lastCoordinateY][lastCoordinateX];
        PlaysX.RemoveAt(PlaysX.Count - 1);
        PlaysY.RemoveAt(PlaysY.Count - 1);
      }
    }

    public Char CurrentTurn(Int32 turn)
    {
      if (turn == 0)
        return 'b';
      return (((turn - 1) / 2) % 2 == 0) ? 'w' : 'b';
    }

    public Int32 CurrentTurnRemaining(Int32 turn) => (turn + 1) % 2 == 0 ? 2 : 1;

    public Char CurrentTurn() => CurrentTurn(PlaysX.Count);

    public Int32 CurrentTurnRemaining() => CurrentTurnRemaining(PlaysX.Count);
  }
}