using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class DumpBattleController : MonoBehaviour
{
    static DumpBattleController _instance;
    public static DumpBattleController Instance
    {
        get
        {
            if(_instance == null)
            {
                _instance = new GameObject("xxx").AddComponent<DumpBattleController>();
            }

            return _instance;
        }
    }

    public Action<string> OnBattleEnd { get; internal set; }

    bool _battleStart;
    LocalFrameNetGame _localFrame;
    string _guid;
    float _startTime = 0;
    public void StartBattle(int userId, BattleStartMessage message, IClientGameSocket socket)
    {
        _battleStart = true;
        var index = Array.FindIndex(message.joins, m=>m.userId == userId);
        Assert.IsTrue(index >= 0);

        _localFrame= new LocalFrameNetGame(0.02f, socket, index, message, false);
        _guid = message.guid;
        _startTime = Time.time;
        _localFrame.SendReady(1);
    }

    private void Update() {
        if(!_battleStart) return;

        ProcessUserOpt();
        _localFrame.Update();

        if(_localFrame.GameFrame < _localFrame.ReceivedServerFrame)
        {
            _localFrame.GameFrame ++;
            DoFrame();
        }

        if(Time.time > _startTime + 10)
        {
            _battleStart = false;
            OnBattleEnd?.Invoke(_guid);
            _localFrame.OnBattleDestroy();
            _localFrame = null;
        }
    }

    // 一秒发送一个位置把。
    float _lastSendProcessTime;
    private void ProcessUserOpt()
    {
        if(Time.time - _lastSendProcessTime < 1) return;

        _lastSendProcessTime = Time.time;
        _localFrame.SetPos(new MessagePosItem(){
            posX = UnityEngine.Random.Range(0, 1000000)
        });
    }

    private void DoFrame()
    {
        List<MessageItem> _list = new List<MessageItem>();
        _localFrame.GetFrameInput(_localFrame.GameFrame,  _list);

        foreach(var x in _list)
        {
            if(x.messageBit == MessageBit.Pos)
            {
                Debug.LogError($"{x.id} {x.posItem.posX}");
            }
        }
    }
}
