
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LiteNetLib.Utils;
using UnityEngine;

enum NetGameState
{
    Replay,
    NormalBattle,
    Reconnect,
}

enum RemainCheckMsgType
{
    None,
    FinishRoom,
    ReadyRoom,
}

public class LocalFrameNetGame : LocalFrame
{
    IClientGameSocket _socket;
    private int _lastReconnectFrame;
    NetGameState _state;
    public Dictionary<int, int> _finishStageFrames = new Dictionary<int, int>();
    RemainCheckMsgType _remainCheckMsgType;

    public List<int> AllUserIds{get; private set;}

    public LocalFrameNetGame(float tick, IClientGameSocket socket, int id, BattleStartMessage message, bool reconnect) : base(tick, id)
    {
        _socket = socket;
        _socket.OnReceiveMsg += OnReceive;
        _socket.OnConnected += OnConnected;
        
        SetHelpAi(ClientBattleRoomMgr.Instance().GetHelperAiInfo()); // 掉线托管
        AllUserIds = message.joins.Select(m=>(int)m.userId).ToList();

        if(reconnect)
        {
            _state = NetGameState.Replay;
            DisablePresentation = true;
            ReGetMsgFromServer(true);
        }
        else
        {
            _state = NetGameState.NormalBattle;
        }
    }

    private void ReGetMsgFromServer(bool force)
    {
        if(_pendingReconnect && !force) return;

        _socket.SendMessage(new ServerReconnectMsg(){
            startFrame = ReceivedServerFrame
        });
        _pendingReconnect = true;
        _pendingMsg.Clear();
    }

    bool _pendingReconnect;
    Dictionary<int, ServerPackageItem> _pendingMsg = new Dictionary<int, ServerPackageItem>();
    private void OnConnected()
    {
        ReGetMsgFromServer(true);
    }

    public override void Update()
    {
        if(_state == NetGameState.Replay)
        {
            UpdateReplay();
            return;
        }
        else if(_state == NetGameState.Reconnect)
        {
            UdpateReconnect();
            return;
        }

        // 战斗结束的时候，就不需要update了。
        if(BattleEnd)
        {
            return;
        }

        StatisticsFrame();

        totalTime += Time.deltaTime;
        if(totalTime - preFrameSeconds <_tick)
        {
            return;
        }

        preFrameSeconds += _tick;

        SendMsg();
    }

    private void UdpateReconnect()
    {
        if(GameFrame == _lastReconnectFrame)
        {
            _state = NetGameState.NormalBattle;
            _socket.SendMessage(new UserReloadServerOKMsg());
        }
    }

    private void UpdateReplay()
    {
        // if(BattleEnd)
        // {
        //     _state = NetGameState.NormalBattle;
        //     return;
        // }
        
        // var targetFrame = ReceivedServerFrame;
        // // loading process
        // // GameCore.Event.Fire(this, GameEvent.GameEventContinueBattleLoadingProgress.CreateEvent((int)(1.0f * GameFrame / ReceivedServerFrame  * 100)));

        // MainNet.FastUpdateResource();

        // if(GameFrame >= ReceivedServerFrame - 10) // 切换回正常游戏。
        // {
        //     _state = NetGameState.NormalBattle;

        //     if(World.DefaultGameObjectInjectionWorld != null)
        //     {
        //         var groupPresetation = World.DefaultGameObjectInjectionWorld.GetExistingSystem<UnsortedPresentationSystemGroup>();
        //         if(groupPresetation != null)
        //         {
        //             groupPresetation.Enabled = true;
        //             DisablePresentation = false;
        //         }

        //         var userPosSytem = BattleCore.BattleControllerManager.GetBattleController<UserPositionController>();
        //         userPosSytem.UpdatePos();
        //     }

        //     GameCore.UI.CloseUIFormById(UIFormId.UIStageLoading);
        //     LocalFrameReloadClient.OnReplaySwitchToNormalBattle();
        //     _socket.SendMessage(new UserReloadServerOKMsg());
        // }
    }

    float _tLog = 0;
    int _preFrame = 0;
    int _preReceiveFrame = 0;
    private void StatisticsFrame()
    {
        if(Time.time - _tLog > 1)
        {
            _tLog = Time.time;

            FrameProcessPerSec = GameFrame - _preFrame;
            _preFrame = GameFrame;

            ReceiveFramePerSec = ReceivedServerFrame - _preReceiveFrame;
            _preReceiveFrame = ReceivedServerFrame;
        }
    }

    internal void SendReady(int stage)
    {
        // Debug.LogError("ready " + stage);
        _clientStageIndex = stage;
        _remainCheckMsgType = RemainCheckMsgType.ReadyRoom;
        _socket.SendMessage(new ReadyStageMsg(){
            stageIndex = stage
        });
    }

    public void SendMsg(uint idSendUser = 99999)
    {
        if(_messageItem != null)
        {
            if(idSendUser != 99999)
            {
                var item = _messageItem.Value;
                item.id = idSendUser;
                _messageItem = item;
            }

            // Debug.LogError("" + _messageItem.Value.id + " " + _messageItem.Value.messageBit);

            _socket.SendMessage(new PackageItem()
            {
                messageItem = _messageItem.Value
            });
        
            _messageItem = null;
        }
    }

    internal void OnReceive(NetDataReader reader)
    {
        // var diff = Time.time - _preGetMessageTime;
        // if(diff > 0.15f && _preGetMessageTime != 0)
        // {
        //     Debug.LogError($"收到消息卡顿 diff{diff} deltaTime:{Time.deltaTime} ping:{SocketRoundTripTime}");
        // }
        // _preGetMessageTime = Time.time;

        if(reader.AvailableBytes == 0) return; // 被room处理了

        var msgType = (MsgType1)reader.PeekByte();

        if(msgType > MsgType1.ServerMsgEnd___)// room msg
        {
            return;
        }

        if(msgType == MsgType1.Unsync)
        {
            // unsync
            // Alert.CreateAlert("出现不同步")
            //     .SetRightButton(()=>{UI_IF_Battle_Result.ReturnToLobbyTab(LobbyTab.LEVEL);}).Show();
            // var x = reader.Get<UnSyncMsg>().unSyncInfos;
            // for(int i = 0; i < x.Length; i++)
            // {
            //     PlaybackReader.SaveLogError(x[i], $"unsyncNetGame_{i}.log");
            // }
            return;
        }
        else if(msgType == MsgType1.ServerReadyForNextStage)
        {
            ServerReadyForNextStage msg = reader.Get<ServerReadyForNextStage>();
            _remainCheckMsgType = RemainCheckMsgType.None;
            // 可以移除其实
            return;
        }
        else if(msgType == MsgType1.ServerEnterLoading)
        {
            ServerEnterLoading msg = reader.Get<ServerEnterLoading>();

            _finishStageFrames[msg.stage] = msg.frameIndex;
            _remainCheckMsgType = RemainCheckMsgType.None;
            return;
        }
        else if(msgType == MsgType1.PauseGame)
        {
            PauseGameMsg msg = reader.Get<PauseGameMsg>();      // 暂时未处理
            IsPaused = msg.pause;

            if(!IsPaused)
            {
                Time.timeScale = 1;
            }
            return;
        }
        else if(msgType == MsgType1.ServerClose)
        {
            Debug.LogError("serverFrameEnd");
            return;
        }
        else if(msgType == MsgType1.ServerReConnect)        // 断线重连
        {
            var msg = reader.Get<ServerReconnectMsgResponse>();

            foreach(var x in msg.stageFinishedFrames)
            {
                _finishStageFrames[x.Item1] = x.Item2;
            }

            foreach(var x in msg.bytes)
            {
                var getPackageItem = ReadObj<ServerPackageItem>(x);
                _pendingMsg[getPackageItem.frame] = getPackageItem;
            }

            var startIndex = ReceivedServerFrame + 1;
            for(int i = 0; i < _pendingMsg.Count; i++)
            {
                var frameIndex = startIndex + i;

                if(_pendingMsg.TryGetValue(frameIndex, out var x))
                {
                    ProcessPackageItem(_pendingMsg[ frameIndex ]);
                }
                else
                {
                    Debug.LogError("发现问题：frame不存在， " + frameIndex);
                    Debug.LogError(string.Join(",",  _pendingMsg.Keys));
                }

                _lastReconnectFrame = frameIndex;
            }

            if(_state != NetGameState.Replay)
                _state = NetGameState.Reconnect;
                
            _pendingMsg.Clear();
            _pendingReconnect = false;
            return;
        }

        ServerPackageItem item = reader.Get<ServerPackageItem>();
        if(_pendingReconnect)           // 断线重连
        {
            _pendingMsg.Add(item.frame, item);
            return;
        }

        ProcessPackageItem(item);
    }

    private void ProcessPackageItem(ServerPackageItem item)
    {
        int frame = item.frame;
        if(frame <= 0) return;

        var list = item.list;
        if(frame <= ReceivedServerFrame)
        {
            Debug.LogError($"frame <= frameServer {frame} {ReceivedServerFrame}");
            return;
        }

        if(ReceivedServerFrame != frame - 1)
        {
            Debug.LogError($"frame 不连续 {frame}  {ReceivedServerFrame}");
            ReGetMsgFromServer(false);
            return;
        }

        if(_allMessage.ContainsKey(frame))
        {
            Debug.LogError("_allMessage.ContainsKey(frame)");
            return;
        }

        if(list != null && list.Count > 0)
        {
            _allMessage.Add(frame, list);
        }

        ReceivedServerFrame = frame;
    }

    public override bool IsClientBattle => false;
    public override bool IsNetBattle => true;
    public override bool IsPlayback => _state == NetGameState.Replay;
    public override bool IsClientNormalBattle => false;
    public override bool IsPendingSkillChooseWhenRoundEnd => !(IsInReconnectStage || IsStageEndFrame);
    public override bool IsInPreloadState => _state == NetGameState.Replay;
    public override int LastCanExecuteFrame
    {
        get
        {
            if(_finishStageFrames.TryGetValue(_clientStageIndex, out var frame))
            {
                return frame;
            }

            return ReceivedServerFrame;
        }
    }


    public bool IsStageEndFrame => _finishStageFrames.ContainsKey(_clientStageIndex) ? _finishStageFrames[_clientStageIndex] == GameFrame : false;
    public override bool IsCanLoading
    {
        get{
            return IsStageEndFrame;
        }
    }

    internal override void OnBattleDestroy()
    {
        _socket.OnReceiveMsg -= OnReceive;
        _socket.OnConnected -= OnConnected;
    }

    #region sendMsg
    
    #endregion

    public int SocketRoundTripTime => _socket.RoundTripTime;
    public int FrameProcessPerSec{get; private set;}
    public int ReceiveFramePerSec{get; private set;}
    public bool IsInReconnectStage => _lastReconnectFrame > GameFrame || _state == NetGameState.Replay;

    static NetDataReader _reader = new NetDataReader();

    public static T ReadObj<T>(byte[] bytes) where T : struct, INetSerializable
    {
        _reader.SetSource(bytes);
        return _reader.Get<T>();
    }

    public override bool CanEnterGame{
        get{
            return ReceivedServerFrame > 0;
        }
    }
}
