using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib.Utils;
using UnityEngine;

public class GameLogicGUI : MonoBehaviour
{
    uint _userId;
    public void Init(RoomGUI gui, uint userId)
    {
        gui.GetStartMessage = GetStartMessage;
        gui.GetJoinMessage = GetJoinMessage;
        gui.GetStartRoomCondition = GetStartRoomCondition;
        _userId = userId;
    }

    private IntPair2[] GetStartRoomCondition()
    {
        return null;
    }

    private (JoinMessage, ClientUserJoinShowInfo) GetJoinMessage(int roomId)
    {
        return (default, default);
    }

    private (BattleStartMessage, ClientRoomShowInfo) GetStartMessage()
    {
        return (default, default);
    }

    void OnGUI()
    {
         if(ClientBattleRoomMgr.Instance()._roomState == TeamRoomState.InSearchRoom)
        {
            DrawOutsideRoom();
        }
        else if(ClientBattleRoomMgr.Instance()._roomState == TeamRoomState.InRoom)
        {
            DrawInsideRoom();
        }
        else if(ClientBattleRoomMgr.Instance()._roomState == TeamRoomState.InBattle)
        {
            DrawInBattle();
        }
    }

    private void DrawOutsideRoom()
    {
    }

    private void DrawInBattle()
    {
    }

    private void DrawInsideRoom()
    {
    }

    void Start()
    {
        DontDestroyOnLoad(gameObject);
    }
}