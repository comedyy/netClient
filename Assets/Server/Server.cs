using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib;
using LiteNetLib.Utils;

enum GameState
{
    NotBegin,
    Running,
    End,
}

struct PlayerInfo
{
    public int finishedStageValue;
    public float finishStageTime;

    public int readyStageValue;
    public float readyStageTime;
}

public class Server
{
    public ushort _frame;
    public float _totalSeconds;
    public float preFrameSeconds;
    float _tick;
    ServerSyncType _syncType;
    int _maxFrame;

    IServerGameSocket _socket;
    private List<int> _netPeers;
    HashChecker _hashChecker;
    int _pauseFrame = -1;
    
    GameState _gameState = GameState.NotBegin;
    private int _stageIndex;
    PlayerInfo[] _playerInfos;
    public Dictionary<int, int> _finishStageFrames = new Dictionary<int, int>();

    FrameMsgBuffer _frameMsgBuffer = new FrameMsgBuffer();
    public RoomStartBattleMsg _startMessage;
    float _waitFinishStageTime = 0;
    float _waitReadyStageTime = 0;
    float _roomTime;

    public Server(ServerSetting serverSetting, IServerGameSocket socket, List<int> netPeers)
    {
        _frame = 0;
        _totalSeconds = 0;
        preFrameSeconds = 0;
        _tick = serverSetting.tick;
        _socket = socket;
        _netPeers = netPeers;
        _syncType = serverSetting.syncType;
        _maxFrame = serverSetting.maxFrame == 0 ? ushort.MaxValue : serverSetting.maxFrame;

        _waitFinishStageTime = serverSetting.waitFinishStageTimeMs == 0 ? 10 : serverSetting.waitFinishStageTimeMs / 1000f;
        _waitReadyStageTime = serverSetting.waitReadyStageTimeMs == 0 ? 10 : serverSetting.waitReadyStageTimeMs / 1000f;
    }

    bool IsPause => _pauseFrame != int.MaxValue;

    public void Update(float deltaTime, float roomTime)
    {
        _roomTime = roomTime;
        if(_gameState != GameState.Running) return;

        UpdateReadyNextStageRoom();

        if(_pauseFrame <= _frame) return; // 用户手动暂停

        UpdateFinishRoom();

        if(!IsPause && deltaTime == 0) return; // // TImeScale == 0 并且未暂停，就是游戏在初始化
        
        _totalSeconds += deltaTime;
        if(preFrameSeconds + _tick > _totalSeconds)
        {
            return;
        }

        preFrameSeconds += _tick;

        _frame++;
        BroadCastMsg();

        if(_frame == _maxFrame) // timeout
        {
            _gameState = GameState.End;
            _socket.SendMessage(_netPeers, new ServerCloseMsg());
        }
    }

    public void AddMessage(int peer, NetDataReader reader)
    {
        var msgType = reader.PeekByte();
        if(msgType == (byte)MsgType1.HashMsg)
        {
            FrameHash hash = reader.Get<FrameHash>();
            string[] unsyncs = _hashChecker.AddHash(hash);
            if(unsyncs != null)
            {
                _socket.SendMessage(_netPeers, new UnSyncMsg()
                {
                    unSyncInfos = unsyncs
                });
            }

            return;
        }
        else if(msgType == (byte)MsgType1.ReadyForNextStage)
        {
            ReadyStageMsg ready = reader.Get<ReadyStageMsg>();
            var readyStageValue = ready.stageIndex;

            if(readyStageValue <= _stageIndex)
            {
                _socket.SendMessage(_netPeers, new ServerReadyForNextStage(){
                    stageIndex = readyStageValue,
                });
                return;
            }

            var index = _netPeers.FindIndex(m=>m == peer);
            if(index < 0) return;
            
            if(_playerInfos[index].readyStageValue == readyStageValue)   // 已经确认过了
            {
                return;
            }

            _playerInfos[index].readyStageValue = readyStageValue;
            _playerInfos[index].readyStageTime = _roomTime;

            UpdateReadyNextStageRoom();
            
            return;
        }
        else if(msgType == (byte)MsgType1.FinishCurrentStage)
        {
            FinishRoomMsg ready = reader.Get<FinishRoomMsg>();
            var finishedStageValue = ready.stageValue;
            if(finishedStageValue < _stageIndex)    // 断线情况
            {
                _socket.SendMessage(peer, new ServerEnterLoading(){
                    frameIndex = _finishStageFrames[finishedStageValue]
                });
                return;
            }

            var index = _netPeers.FindIndex(m=>m == peer);
            if(index < 0) return;

            if(_playerInfos[index].finishedStageValue == finishedStageValue)   // 已经确认过了
            {
                return;
            }

            _playerInfos[index].finishedStageValue = finishedStageValue;
            _playerInfos[index].finishStageTime = _roomTime;

            UpdateFinishRoom();
            
            return;
        }
        else if(msgType == (byte)MsgType1.ServerReConnect)
        {
            ServerReconnectMsg ready = reader.Get<ServerReconnectMsg>();
            _socket.SendMessage(peer, _frameMsgBuffer.GetReconnectMsg(ready.startFrame, _finishStageFrames));
            return;
        }
        else if(msgType == (byte)MsgType1.PauseGame)
        {
            PauseGameMsg pause = reader.Get<PauseGameMsg>();

            if(pause.pause)
            {
                _pauseFrame = _frame + 1;
            }
            else
            {
                _pauseFrame = int.MaxValue;
            }

            _socket.SendMessage(_netPeers, pause);
            return;
        }

        reader.GetByte(); // reader去掉msgType
        _frameMsgBuffer.AddFromReader(reader);
    }

    int GetMaxReadyStageValue()
    {
        int max = -1;
        for(int i = 0; i < _playerInfos.Length; i++)
        {
            max = Math.Max(max, _playerInfos[i].readyStageValue);
        }
        return max;
    }
    private void UpdateReadyNextStageRoom()
    {
        var maxReadyStageValue = GetMaxReadyStageValue();
        if(maxReadyStageValue <= _stageIndex) return;// 都在当前stage

        bool timeout = false;       // 有一个人完成了，倒计时10秒也要进入
        for(int i = 0; i < _playerInfos.Length; i++)
        {
            if(_playerInfos[i].readyStageValue == maxReadyStageValue)
            {
                var diff = _roomTime - _playerInfos[i].readyStageTime;
                var isOK = diff > _waitReadyStageTime;
                timeout |= isOK;
            }
        }

        var condition = timeout || _playerInfos.Min(m=>m.readyStageValue) == maxReadyStageValue;
        if(condition)
        {
            _stageIndex = maxReadyStageValue;
            _socket.SendMessage(_netPeers, new ServerReadyForNextStage(){
                stageIndex = _stageIndex,
            });

            _pauseFrame = int.MaxValue;
        }
    }

    int GetMaxFinishedStageValue()
    {
        int max = -1;
        for(int i = 0; i < _playerInfos.Length; i++)
        {
            max = Math.Max(max, _playerInfos[i].finishedStageValue);
        }
        return max;
    }

    private void UpdateFinishRoom()
    {
        var maxFinishedStageValue = GetMaxFinishedStageValue();
        if(maxFinishedStageValue < _stageIndex) return; // 都在当前stage

        bool timeout = false;       // 有一个人完成了，倒计时10秒也要进入
        for(int i = 0; i < _playerInfos.Length; i++)
        {
            if(_playerInfos[i].finishedStageValue == maxFinishedStageValue)
            {
                var diff = _roomTime - _playerInfos[i].finishStageTime;
                var isOK = diff > _waitFinishStageTime;
                timeout |= isOK;
            }
        }

        var condition = timeout || _playerInfos.Min(m=>m.finishedStageValue) == maxFinishedStageValue;
        if(condition)
        {
            _pauseFrame = _frame;

            if(maxFinishedStageValue == 999)
            {
                // End battle
                _gameState = GameState.End;
                _socket.SendMessage(_netPeers, new ServerCloseMsg());
            }
            else
            {
                var stopFrame = _frame + 1;
                _socket.SendMessage(_netPeers, new ServerEnterLoading(){
                    frameIndex = stopFrame,
                    stage = maxFinishedStageValue
                });
                _finishStageFrames[maxFinishedStageValue] = stopFrame;
            }
        }
    }

    private void BroadCastMsg()
    {
        if(_syncType == ServerSyncType.SyncMsgOnlyHasMsg && _frameMsgBuffer.Count == 0)
        {
            return;
        }

        _socket.SendMessage(_netPeers, new ServerPackageItem(){
            frame = (ushort)_frame, clientFrameMsgList = _frameMsgBuffer
        });
    }

    public void StartBattle(RoomStartBattleMsg startMessage)
    {
        _hashChecker = new HashChecker(_netPeers.Count);
        _playerInfos = new PlayerInfo[_netPeers.Count];

        _gameState = GameState.Running;
        
        _socket.SendMessage(_netPeers, startMessage);
        _startMessage = startMessage;
    }

    public void Destroy()
    {
    }

    public bool IsBattleEnd => _gameState == GameState.End;
}