using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.Assertions;

public enum TeamRoomEnterFailedReason
{
    OK,
    LogicCondition,
    SelfVersionTooLow,
    SelfVersionTooHigh,
    RoomNotExist,
    ConnectionFailed,
    JustInsideRoom,
}

public enum RoomConditionType
{
    Level = 1,
    Star = 2,
}

public enum TeamRoomState
{
    InSearchRoom,
    InRoom,
    InBattle,
}

public class ClientBattleRoomMgr : MonoBehaviour
{
    string LastClientBattleRoomMgrState => UserId + "_RoomState";
    GameClientSocket _socket;
    int _overrideUserId;
    public int UserId{
        get{
            if(_overrideUserId != 0) return _overrideUserId;

            throw new Exception("----");
            // return (int)GameCore.Proxy.GetProxy<PlayerProxy>().Rid;
        }
    }
    static ClientBattleRoomMgr _instance = null;
    
    public RoomUser[] _userList;
    public UpdateRoomMemberList _updateRoomInfo;
    public bool IsLastBattleQuitMember{get; private set;}
    public RoomInfoMsg[] _roomMsgList;
    Dictionary<int, UpdateRoomMemberList> _dicRoomInfo = new Dictionary<int, UpdateRoomMemberList>();
    public int enterRoomId{get; private set;}
    HashSet<int> _allRequiredMsg = new HashSet<int>();
    public TeamRoomState _roomState {get; private set;}= TeamRoomState.InSearchRoom;
    public GetUserStateMsg.UserState ServerUserState{get; private set;} = GetUserStateMsg.UserState.None;
    public string _battleGUID = "";
    const int MAX_RETRY_COUNT = 2;
    int _reconnectCount = 0;
    TeamConnectParam _connectToServerParam = TeamConnectParam.None;


    public Dictionary<int, int> _dicLoadingProcess = new Dictionary<int, int>();

    public event Action<TeamRoomState, TeamRoomState> OnSwitchState;
    public event Action OnTeamInfoChange;
    public event Action<int> OnTeamRoomInfoChange;
    public event Action<JoinMessage> OnGetUserJoinInfo;
    public event Action OnQueryRoomList;
    public event Action<int> OnUserQuit;

    public Action<int> TipAction;
    public Action<int> AlertAction;

    public static ClientBattleRoomMgr Instance()
    {
        if(_instance == null)
        {
            _instance = new GameObject().AddComponent<ClientBattleRoomMgr>();
            _instance.Init();
        }

        return _instance;
    }

    public void Init()
    {
        var ip = MainNet.LockstepServerUrl;
        var port = MainNet.LockstepServerPort;
        _socket = new GameClientSocket(ip, port, 0);
        _socket.OnConnected = OnConnected;
        _socket.OnDisConnected = OnDisConnected;
        _socket.OnReceiveMsg += OnReceiveMessage;
    }

    
    float _lastSendProcessTime = 0;
    internal void SendProcess(int process)
    {
        if(_roomState != TeamRoomState.InBattle) return;

        if(Time.time - _lastSendProcessTime < 0.3f && process != 100 ) return;
        _lastSendProcessTime = Time.time;

        _socket.SendMessage(new RoomSyncLoadingProcessMsg()
        {
            percent = process
        });
    }

    private async void OnDisConnected()
    {
        // try max
        if(_reconnectCount >= MAX_RETRY_COUNT)
        {
            _connectToServerParam = TeamConnectParam.None;
            SwitchRoomState(TeamRoomState.InSearchRoom);
            return;
        }

        // check reconnect
        if(_roomState != TeamRoomState.InSearchRoom)
        {
            await Task.Delay(100);
            _reconnectCount ++;
            ReconnectToServer(TeamConnectParam.SyncInfo);
        }
    }

    private void OnConnected()
    {
        LogMessage("onConnected");

        _reconnectCount = 0;
        _socket.SendMessage(new RoomUserIdMsg(){
            userId = UserId, connectParam = _connectToServerParam
        });
        _connectToServerParam = default;
    }


    private void OnReceiveMessage(NetDataReader reader)
    {
        var msgType = (MsgType1)reader.PeekByte();
        if(msgType < MsgType1.ServerMsgEnd___)
        {
            return;
        }

        LogMessage("<<<<<<<<<<<===== " + msgType);

        if(msgType == MsgType1.GetAllRoomList)
        {
            _roomMsgList = reader.Get<RoomListMsg>().roomList;

            foreach(var x in _roomMsgList)
            {
                _dicRoomInfo[x.updateRoomMemberList.roomId] = x.updateRoomMemberList;
            }
            _allRequiredMsg.Remove((int)MsgType1.GetAllRoomList);

            OnQueryRoomList?.Invoke();
        }
        else if(msgType == MsgType1.GetRoomStateResponse)
        {
            var msg = reader.Get<GetRoomStateResponse>();
            _dicRoomInfo[msg.roomId] = msg.infoMsg.updateRoomMemberList;

            OnTeamRoomInfoChange?.Invoke(msg.roomId);
        }
        else if(msgType == MsgType1.GetUserInfoResponse)
        {
            var msg = reader.Get<GetUserJoinInfoResponse>();
            OnGetUserJoinInfo?.Invoke(ReadObj<JoinMessage>(msg.join));
        }
        else if(msgType == MsgType1.SyncRoomMemberList)
        {
            var msg = reader.Get<UpdateRoomMemberList>();
            _userList = msg.userList;
            _updateRoomInfo = msg;
            enterRoomId = msg.roomId;

            _dicRoomInfo[enterRoomId] = msg;

            UpdateAiHelp();

            if(_userList.Length == 0)
            {
                SwitchRoomState(TeamRoomState.InSearchRoom);
            }
            else if(_roomState == TeamRoomState.InSearchRoom)
            {
                SwitchRoomState(TeamRoomState.InRoom);
            }

            OnTeamInfoChange?.Invoke();
        }
        else if(msgType == MsgType1.RoomEventSync)
        {
            var msg = reader.Get<SyncRoomOptMsg>();
            if(msg.param == UserId && msg.state == SyncRoomOptMsg.RoomOpt.Kick)
            {
                UnityEngine.Debug.Log("kicked");
                // Tip.CreateTip(590038).Show();
            }

            var onlyNotice = (msg.state == SyncRoomOptMsg.RoomOpt.Leave || msg.state == SyncRoomOptMsg.RoomOpt.Kick || msg.state == SyncRoomOptMsg.RoomOpt.Join) && msg.param != UserId;
            if(onlyNotice)
            {
                var param = msg.param;
                var user = _userList.FirstOrDefault(m=>m.userId == param);
                if(user.userId != 0)
                {
                    UnityEngine.Debug.Log("join " + user.name);
                    // Tip.CreateTip(msg.state == SyncRoomOptMsg.RoomOpt.Join ? 590014 : 590015, user.name).Show();
                }

                if(msg.state == SyncRoomOptMsg.RoomOpt.Leave || msg.state == SyncRoomOptMsg.RoomOpt.Kick)
                {
                    OnUserQuit?.Invoke(msg.param);
                }
                return;
            }

            if(msg.state == SyncRoomOptMsg.RoomOpt.Leave || msg.state == SyncRoomOptMsg.RoomOpt.Kick || msg.state == SyncRoomOptMsg.RoomOpt.MasterLeaveRoomEnd)
            {
                var teamMaster = isTeamMaster;
                _userList = null;
                _updateRoomInfo = default;
                enterRoomId = 0;

                if(msg.state == SyncRoomOptMsg.RoomOpt.MasterLeaveRoomEnd && !teamMaster)
                {
                    // netGame.OnTeamRoomEnd(590016);
                    UnityEngine.Debug.Log("master leave");
                }

                SwitchRoomState(TeamRoomState.InSearchRoom, false);
            }
        }
        else if(msgType == MsgType1.ErrorCode)
        {
            var msg = reader.Get<RoomErrorCode>();
            LogMessage(msg.roomError.ToString());

            {
                UnityEngine.Debug.LogError(msg.roomError.ToString());
            }

            if(msg.roomError == RoomError.RoomFull || msg.roomError == RoomError.RoomNotExist)
            {
                QueryRoomList();
            }
        }
        else if(msgType == MsgType1.GetUserState)
        {
            var msg = reader.Get<GetUserStateMsg>();
            LogMessage(msg.state.ToString());
            ServerUserState = msg.state;

            if(ServerUserState == GetUserStateMsg.UserState.None)
            {
                PlayerPrefs.SetInt(LastClientBattleRoomMgrState, 0);  // state
                PlayerPrefs.Save();
            }
        }
        else if(msgType == MsgType1.RoomStartBattle)
        {
            // var roomStartBattle = reader.Get<RoomStartBattleMsg>();
            // BattleStartMessage startMessage = ReadObj<BattleStartMessage>(roomStartBattle.StartMsg);
            // if(_roomState == TeamRoomState.InBattle && _battleGUID == startMessage.guid)
            // {
            //     // 战斗已经开始
            //     return;
            // }

            // IsLastBattleQuitMember = false;

            // startMessage.joins = new JoinMessage[roomStartBattle.joinMessages.Count];
            // for(int i = 0; i < roomStartBattle.joinMessages.Count; i++)
            // {
            //     startMessage.joins[i] = ReadObj<JoinMessage>(roomStartBattle.joinMessages[i]);
            // }

            // var lockStepProxy = GameCore.Proxy.GetProxy<LockStepMessageProxy>();
            // lockStepProxy.SetBattleMessage(startMessage, MessageBattleType.OnlineBattle, _socket);
            // lockStepProxy.netReconnectBattle = roomStartBattle.isReconnect;
            // lockStepProxy.netBattleUserId = UserId;
            // SwitchRoomState(TeamRoomState.InBattle);

            // MainNet.StartBattle(startMessage);
            // _battleGUID = startMessage.guid;
            // BattleCore.OnBattleEnd = OnBattleEnd;
        }
        else if(msgType == MsgType1.RoomSyncLoadingProcess)
        {
            var process = reader.Get<RoomSyncLoadingProcessMsg>();
            var peer = process.id;

            _dicLoadingProcess[peer] = process.percent;
        }
    }

    public void OnMemberLeaveBattle()
    {
        SwitchRoomState(TeamRoomState.InSearchRoom, false, false);
        enterRoomId = 0;
        _updateRoomInfo = default;
        IsLastBattleQuitMember = true;
    }

    void SwitchRoomState(TeamRoomState state, bool notifyRoomEnd = true, bool updateRoomState = true)
    {
        if(_roomState == state) return;

        var fromState = _roomState;
        _roomState = state;
        if(_roomState == TeamRoomState.InSearchRoom)
        {
            // 断开连接
            _socket.DisConnect();

            if(notifyRoomEnd/* && LocalFrame.Instance is LocalFrameNetGame netGame*/)
            {
                // netGame.OnTeamRoomEnd(590041);
                UnityEngine.Debug.LogError("team end");
            }

            // 请求roomList
            QueryRoomList();
        }
        else if(_roomState == TeamRoomState.InBattle)
        {
            _dicLoadingProcess.Clear();
        }
        else if(_roomState == TeamRoomState.InRoom)
        {
        }

        OnSwitchState?.Invoke(fromState, _roomState);

        if(updateRoomState)
        {
            PlayerPrefs.SetInt(LastClientBattleRoomMgrState, (int)state);  // state
            PlayerPrefs.Save();
        }
    }

    public bool EverInRoom{
        get
        {
            var x = PlayerPrefs.GetInt(LastClientBattleRoomMgrState);
            return x != (int)TeamRoomState.InSearchRoom;
        }
    } 

    void Update()
    {
        _socket.Update();
    }

    void OnDestroy()
    {
        _socket?.OnDestroy();
    }

    void OnBattleEnd(string guid)
    {
        if(_battleGUID != guid) return;
        if(IsLastBattleQuitMember) return;

        if(_roomState != TeamRoomState.InSearchRoom)
        {
            LeaveRoom();
        }
    }

    public bool GetRoomInfo(int id, out UpdateRoomMemberList msg)
    {
        return _dicRoomInfo.TryGetValue(id, out msg);
    }

    public void QueryRoomInfo(int id)
    {
        if(_dicRoomInfo.TryGetValue(id, out var roomInfo) && roomInfo.roomId == 0)
        {
            OnTeamRoomInfoChange?.Invoke(id);
            return; // 房间已经解散
        }

        _socket.SendUnConnectedMessage(new GetRoomStateMsg(){idRoom = id});
    }

    public void QueryUserJoinInfo(int id)
    {
        _socket.SendMessage(new GetUserJoinInfoMsg(){userId = id});
    }

    public void QueryRoomList()
    {
        _socket.SendUnConnectedMessage(new RoomListMsgRequest());
    }


    public async Task<RoomInfoMsg[]> QueryRoomListAsync()
    {
        _socket.SendUnConnectedMessage(new RoomListMsgRequest());
        var sendTime = Time.time;
        var intSendTime = Time.time;
        _allRequiredMsg.Add((int)MsgType1.GetAllRoomList);

        while(true)
        {
            await Task.Delay(500);

            if(!_allRequiredMsg.Contains((int)MsgType1.GetAllRoomList))
            {
                return _roomMsgList;
            }
            else if(Time.time - sendTime > 3)
            {
                _socket.SendUnConnectedMessage(new RoomListMsgRequest());
                sendTime = Time.time;
            }
            else if(Time.time - intSendTime > 10)
            {
                return default;
            }
        }
    }
    
    public async Task<TeamRoomEnterFailedReason> UpdateMemberInfo(JoinMessage message, ClientUserJoinShowInfo info)
    {
        var reason = CheckHeroCondition(_updateRoomInfo.conditions, message.GetConditionParam());
        if(reason != TeamRoomEnterFailedReason.OK)
        {
            return reason;
        }

        if(!await ConnectToServerInner())
        {
            return TeamRoomEnterFailedReason.ConnectionFailed;
        }

        _socket.SendMessage(new UpdateMemberInfoMsg(){
            joinMessage = GetBytes(message),
            joinShowInfo = GetBytes(info)
        });

        return TeamRoomEnterFailedReason.OK;
    }

    public async Task<TeamRoomEnterFailedReason> JoinRoom(int enterRoomId, JoinMessage message, ClientUserJoinShowInfo showInfo)
    {
        var reason = CheckJoinCondition(enterRoomId, message.GetConditionParam());
        if(reason != TeamRoomEnterFailedReason.OK)
        {
            return reason;
        }

        if(!await ConnectToServerInner())
        {
            return TeamRoomEnterFailedReason.ConnectionFailed;
        }

        _socket.SendMessage(new JoinRoomMsg(){
            roomId = enterRoomId, 
            joinMessage = GetBytes(message),
            joinShowInfo = GetBytes(showInfo)
        });

        return TeamRoomEnterFailedReason.OK;
    }

    public TeamRoomEnterFailedReason CheckJoinCondition(int enterRoomId, IntPair2[] checkCondition)
    {
        if(enterRoomId == _updateRoomInfo.roomId)
        {
            return TeamRoomEnterFailedReason.JustInsideRoom;
        }

        if(!_dicRoomInfo.TryGetValue(enterRoomId, out var room) || room.roomId == 0)
        {
            return TeamRoomEnterFailedReason.RoomNotExist;
        }

        var conditions = room.conditions;
        return CheckHeroCondition(conditions, checkCondition);
    }

    TeamRoomEnterFailedReason CheckHeroCondition(IntPair2[] conditions, IntPair2[] checkCondition)
    {
        if(conditions != null)
        {
            for(int i = 0; i < conditions.Length; i++)
            {
                if(conditions[i].Item2 > checkCondition[i].Item2) return TeamRoomEnterFailedReason.LogicCondition;
            }
        }

        return TeamRoomEnterFailedReason.OK;
    }

    public async void ReconnectToServer(TeamConnectParam syncRoomInfo)
    {
        if(_socket.connectResult == ConnectResult.Connnected)
        {
            _socket.SendMessage(new RoomUserIdMsg(){
                userId = UserId, connectParam = syncRoomInfo
            });
            return;
        }

        _connectToServerParam = syncRoomInfo;
        await ConnectToServerInner();
    }

    private async Task<bool> ConnectToServerInner()
    {
        _socket.Connect();

        while(_socket.connectResult == ConnectResult.Connecting)
        {
            await Task.Delay(100);
        }

        return _socket.connectResult == ConnectResult.Connnected;
    }

    public async void CreateRoom( BattleStartMessage startBytes, JoinMessage joins, ClientUserJoinShowInfo joinShowInfo, ClientRoomShowInfo roomShowInfo, IntPair2[] conditions)
    {
        if(!await ConnectToServerInner())
        {
            return;
        }

        var setting = GetServerSetting(conditions);

        _socket.SendMessage(new CreateRoomMsg()
        {
            startBattleMsg = GetBytes(startBytes),
            join = GetBytes(joins),
            joinShowInfo = GetBytes(joinShowInfo),
            roomShowInfo = GetBytes(roomShowInfo),
            setting = setting,
        });
    }

    public async void LeaveRoom()
    {
        if(!await ConnectToServerInner())
        {
            return;
        }

        _socket.SendMessage(new UserLeaveRoomMsg());
    }

    public async void KickUser(int kickedUser)
    {
        if(!await ConnectToServerInner())
        {
            return;
        }

        _socket.SendMessage(new KickUserMsg(){
            userId = kickedUser
        });
    }

    public async void ReadyRoom(bool isReady)
    {
        if(!await ConnectToServerInner())
        {
            return;
        }

        _socket.SendMessage(new RoomReadyMsg(){ isReady = isReady});
    }

    public async void StartRoom()
    {
        if(!await ConnectToServerInner())
        {
            return;
        }

        _socket.SendMessage(new StartBattleRequest());
    }

    public async Task<GetUserStateMsg.UserState> CheckRoomState()
    {
        if(_roomState == TeamRoomState.InBattle) return GetUserStateMsg.UserState.HasBattle;
        if(_roomState == TeamRoomState.InRoom) return GetUserStateMsg.UserState.HasRoom;

        ServerUserState = GetUserStateMsg.UserState.Querying;
        _socket.SendUnConnectedMessage(new GetUserStateMsg(){userId = UserId});
        for(int i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            if(ServerUserState != GetUserStateMsg.UserState.Querying){
                return ServerUserState;
            }
        }

        return ServerUserState;
    }

    public void DEBUG_Disconnect()
    {
        if(_socket != null)
        {
            _socket.DisConnect();
        }
    }

    public void ChangeIp(string ip, int port)
    {
        _socket.SetIp(ip, port);
    }

    public async void ChangeUserPos(int i, int k)
    {
        if(!await ConnectToServerInner())
        {
            return;
        }

        _socket.SendMessage(new RoomChangeUserPosMsg(){
            fromIndex = (byte)i, toIndex = (byte)k
        });
    }

    public void SetUserId(uint userId)
    {
        _overrideUserId = (int)userId;
    }
    
    public static uint GetMinCondition(IntPair2[] conditions, RoomConditionType conditionType)
    {
        if(conditions == null) return 0;
        foreach(var x in conditions)
        {
            if(x.Item1 == (int)conditionType) return (uint)x.Item2;
        }

        return 0;
    }

    internal bool enableLog{
        get{
            return PlayerPrefs.GetInt("enableRoomLog", 0) != 0;
        }
        set
        {
            PlayerPrefs.SetInt("enableRoomLog", value ? 1 : 0);
        }
    }

    public bool isTeamMaster
    {
        get
        {
            return enterRoomId > 0 && _userList[0].userId == UserId;
        }
    }

    public bool AllReady { 
        get
        {
            for(int i = 1; i < _userList.Length; i++)
            {
                if(!_userList[i].isReady) return false;
            }

            return true;
        }
    }

    private ServerSetting GetServerSetting(IntPair2[] pairs)
    {
        return new ServerSetting() // 目前设置最高10分钟
        {
            tick = 0.05f, maxFrame = 20 * 60 * 10, syncType = ServerSyncType.SyncMsgEventFrame, masterLeaveOpt = RoomMasterLeaveOpt.RemoveRoomAndBattle, maxCount = 4, 
            Conditions = pairs
        };
    }

    public void LogMessage(string context)
    {
        if(enableLog)
        {
            Debug.LogError(context);
        }
    }
    
    private void UpdateAiHelp()
    {
        // if(LocalFrame.Instance is LocalFrameNetGame netGame)
        // {
        //     netGame.SetHelpAi(GetHelperAiInfo());
        // }
    }

    internal IEnumerable<int> GetHelperAiInfo()
    {
        if(_updateRoomInfo.AIHelperIndex < 0 || _updateRoomInfo.AIHelperIndex >= _updateRoomInfo.userList.Length)
        {
            return null;
        }

        if(_updateRoomInfo.userList[_updateRoomInfo.AIHelperIndex].userId != UserId)
        {
            return null;
        }

        List<int> list = new List<int>();
        for(int i = 0; i < _updateRoomInfo.userList.Length; i++)
        {
            if(_updateRoomInfo.userList[i].needAiHelp)
            {
                list.Add(i);
            }
        }
        return list;
    }

#region Util    
    static NetDataWriter _writer = new NetDataWriter();
    public static byte[] GetBytes(INetSerializable netSerializable)
    {
        _writer.Reset();
        _writer.Put(netSerializable);
        return _writer.CopyData();
    }

    static NetDataReader _reader = new NetDataReader();
    public static T ReadObj<T>(byte[] bytes) where T : struct, INetSerializable
    {
        _reader.SetSource(bytes);
        return _reader.Get<T>();
    }
#endregion

}