using System;
using System.Collections.Generic;

public class Connect6Lang
{
  public static Dictionary<String, Dictionary<String, String>> GetStringResources()
  {
    var stringResource = new Dictionary<String, Dictionary<String, String>>();
    stringResource.Add("en-us", new Dictionary<String, String>());
    stringResource.Add("ko-kr", new Dictionary<String, String>());

    stringResource["en-us"].Add("GameTitle", "Connect6");
    stringResource["ko-kr"].Add("GameTitle", "육목");

    stringResource["en-us"].Add("DisconnectRefresh", "Service interrupted. We'll try to reconnect. Refresh the page if it doesn't work after a few seconds.");
    stringResource["ko-kr"].Add("DisconnectRefresh", "서비스 연결이 끊겼습니다. 자동으로 재접속 합니다. 몇 초 후에도 바둑판이 보이지 않으면 새로고침을 해주세요.");

    stringResource["en-us"].Add("CopyLinkInstruction", "Copy the link below and share with others to play together.");
    stringResource["ko-kr"].Add("CopyLinkInstruction", "아래 링크를 복사해 다른 사람과 공유하면 함께 플레이 하실 수 있습니다.");

    stringResource["en-us"].Add("NewGameButton", "Play new game with current users");
    stringResource["ko-kr"].Add("NewGameButton", "현 유저들과 새 게임 시작하기");

    stringResource["en-us"].Add("UndoButton", "Undo");
    stringResource["ko-kr"].Add("UndoButton", "되감기");

    stringResource["en-us"].Add("ExpirationMessage", "Game session will expire if no activities for 30 minutes.");
    stringResource["ko-kr"].Add("ExpirationMessage", "30분동안 게임 진행이 없으면 세션이 종료됩니다.");

    stringResource["en-us"].Add("NoGameFound", "No game found. Check the address.");
    stringResource["ko-kr"].Add("NoGameFound", "진행중인 게임이 없습니다. 주소를 확인해보세요.");

    stringResource["en-us"].Add("TurnMessage", "{0}'s turn. Remaining Moves: {1}");
    stringResource["ko-kr"].Add("TurnMessage", "{0} 차례. 남은 횟수: {1}");

    stringResource["en-us"].Add("VictoryMessage", "{0} has won.");
    stringResource["ko-kr"].Add("VictoryMessage", "{0}이 승리하였습니다.");

    stringResource["en-us"].Add("CurrentSessionUsersMessage", "{0} person(s) currently connected to this session.");
    stringResource["ko-kr"].Add("CurrentSessionUsersMessage", "현재 {0}명이 이 게임 세션에 참여하고 있습니다.");

    stringResource["en-us"].Add("Black", "Black Stone");
    stringResource["ko-kr"].Add("Black", "흑돌");

    stringResource["en-us"].Add("White", "White Stone");
    stringResource["ko-kr"].Add("White", "백돌");

    stringResource["en-us"].Add("PlayStoneSound", "Play stone sound");
    stringResource["ko-kr"].Add("PlayStoneSound", "돌 소리 재생");

    return stringResource;
  }
}