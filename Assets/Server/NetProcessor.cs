
using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib;
using LiteNetLib.Utils;

public enum RoomEndReason
{
    None,
    RoomMasterLeave,
    BattleEnd,
    AllPeerLeave,
    AllPeerOffLine,
}

public class NetProcessor 
{
    Dictionary<int, ServerBattleRoom> _allUserRooms = new Dictionary<int, ServerBattleRoom>();
    Dictionary<int, ServerBattleRoom> _allRooms = new Dictionary<int, ServerBattleRoom>();
    public int RoomId { get; private set; }
    float _serverTime;
    IServerGameSocket _serverSocket;
    public NetProcessor(IServerGameSocket socket, int initRoomId)
    {
        RoomId = initRoomId;
        _serverSocket = socket;
        _serverSocket.Start();
        _serverSocket.OnReceiveMsg = OnReceiveMsg;
        _serverSocket.OnPeerDisconnect = OnDisconnect;
        _serverSocket.OnPeerReconnected = OnReconnect;
        _serverSocket.GetAllRoomList = GetRoomListMsg;
        _serverSocket.GetUserState = GetUserState;
        _serverSocket.GetRoomState = GetRoomState;
    }

    private void OnReceiveMsg(int peer, NetDataReader reader)
    {
        var msgType = (MsgType1)reader.PeekByte();
        switch(msgType)
        {
            case MsgType1.CreateRoom: CreateRoom(peer, reader.Get<CreateRoomMsg>()); break;
            case MsgType1.JoinRoom: JoinRoom(peer, reader.Get<JoinRoomMsg>()); break;
            case MsgType1.UpdateMemberInfo: UpdateMemberInfo(peer, reader.Get<UpdateMemberInfoMsg>()); break;
            case MsgType1.KickUser: KickUser(peer, reader.Get<KickUserMsg>()); break;
            case MsgType1.LeaveUser: LeaveUser(peer); break;
            case MsgType1.RoomReady: SetIsReady(peer, reader.Get<RoomReadyMsg>()); break;
            case MsgType1.StartRequest : StartBattle(peer, reader.Get<StartBattleRequest>()); break;
            case MsgType1.SetSpeed: SetRoomSpeed(peer, reader.Get<SetServerSpeedMsg>()); break;
            case MsgType1.RoomChangeUserPos: ChangeUserPos(peer, reader.Get<RoomChangeUserPosMsg>()); break;
            case MsgType1.RoomSyncLoadingProcess: SyncLoadingProcess(peer, reader.Get<RoomSyncLoadingProcessMsg>()); break;
            case MsgType1.GetUserInfo: GetUserJoinInfoResponse(peer, reader.Get<GetUserJoinInfoMsg>().userId); break;
            case MsgType1.UserReloadServerOK: UserReloadServerOKMsgProcess(peer); break;
            case MsgType1.GetRoomState: 
            case MsgType1.GetAllRoomList:
                break;
            default:
                if(_allUserRooms.TryGetValue(peer, out var room))
                {
                    room.OnReceiveMsg(peer, reader);
                }
                break;
        }
    }

    private void UpdateMemberInfo(int peer, UpdateMemberInfoMsg updateMemberInfoMsg)
    {
        if(_allUserRooms.TryGetValue(peer, out var room1))  // 已经有房间
        {
            room1.UpdateInfo(peer, updateMemberInfoMsg.joinMessage, updateMemberInfoMsg.joinShowInfo);
        }
        else
        {
            _serverSocket.SendMessage(peer, new RoomErrorCode(){
                roomError = RoomError.RoomNotExist
            });
        }
    }

    private void UserReloadServerOKMsgProcess(int peer)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.UserReloadServerOKMsgProcess(peer);
        }
    }

    private void GetUserJoinInfoResponse(int peer, int userId)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.GetUserJoinInfoResponse(peer, userId);
        }
    }

    private void SyncLoadingProcess(int peer, RoomSyncLoadingProcessMsg roomSyncLoadingProcessMsg)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.UpdateLoadingProcess(peer, roomSyncLoadingProcessMsg);
        }
    }

    private void ChangeUserPos(int peer, RoomChangeUserPosMsg setServerSpeedMsg)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.ChangeUserPos(peer, setServerSpeedMsg.fromIndex, setServerSpeedMsg.toIndex);
        }
    }

    private void KickUser(int peer, KickUserMsg kickUserMsg)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            if(room.KickUser(peer, kickUserMsg.userId))
            {
                _allUserRooms.Remove(kickUserMsg.userId);
            }
        }
    }

    private void SetRoomSpeed(int peer, SetServerSpeedMsg setServerSpeedMsg)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetRoomSpeed(peer, setServerSpeedMsg.speed);
        }
    }

    private RoomListMsg GetRoomListMsg()
    {
        var roomList = _allRooms.Values.Where(m=>!m.HasBattle).Select(m=>m.GetRoomInfoMsg());

        return new RoomListMsg(){
            roomList = roomList.ToArray()
        };
    }

    private GetUserStateMsg GetUserState(int peerId)
    {
        GetUserStateMsg.UserState state = GetUserStateMsg.UserState.None;
        if(_allUserRooms.TryGetValue(peerId, out var room))
        {
            state = room.HasBattle ? GetUserStateMsg.UserState.HasBattle : GetUserStateMsg.UserState.HasRoom;
        }

        return new GetUserStateMsg(){ userId = peerId, state = state};
    }

    
    private GetRoomStateResponse GetRoomState(int roomId)
    {
        if(_allRooms.TryGetValue(roomId, out var room) && !room.HasBattle)
        {
            return new GetRoomStateResponse(){
                roomId = roomId, infoMsg = room.GetRoomInfoMsg()
            };
        }

        return new GetRoomStateResponse(){
            roomId = roomId, infoMsg = default
        };;
    }

    private void StartBattle(int peer, StartBattleRequest startBattleRequest)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.StartBattle(peer);
        }
    }

    
    private void OnReconnect(int peer, TeamConnectParam teamParam)
    {
        if(teamParam == TeamConnectParam.None) return;

        if(!_allUserRooms.TryGetValue(peer, out var room))
        {
            _serverSocket.SendMessage(peer, new RoomErrorCode(){roomError = RoomError.RoomNotExist});
            return;
        }

        room.SetUserOnLineState(peer, true, _serverTime);
        room.SendReconnectBattleMsg(peer);
    }


    void OnDisconnect(int peer)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetUserOnLineState(peer, false, _serverTime);
        }
    }

    void SetIsReady(int peer, RoomReadyMsg ready)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetIsReady(peer, ready.isReady);
        }
    }

    void LeaveUser(int peer)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetUserOnLineState(peer, false, _serverTime);
            var master = room.Master;
            if(master == peer && room.CheckMasterLeaveShouldDestroyRoom())
            {
                RemoveRoom(room, RoomEndReason.RoomMasterLeave);
            }
            else
            {
                if(room.RemovePeer(peer, SyncRoomOptMsg.RoomOpt.Leave))
                {
                    _allUserRooms.Remove(peer);
                }
            }
        }
    }

    private void JoinRoom(int peer, JoinRoomMsg joinRoomMsg)
    {
        if(_allRooms.TryGetValue(joinRoomMsg.roomId, out var room))
        {
            if(_allUserRooms.TryGetValue(peer, out var room1))  // 已经有房间
            {
                if(room1 != room)
                {
                    room.Error(peer, RoomError.JoinRoomErrorHasRoom);
                    return;
                }
                else
                {
                    room.Error(peer, RoomError.JoinRoomErrorInsideRoom);
                    return;
                }
            }

            if(room.AddPeer(peer, joinRoomMsg.joinMessage, joinRoomMsg.joinShowInfo))
            {
                _allUserRooms[peer] = room;
            }
        }
        else
        {
            _serverSocket.SendMessage(peer, new RoomErrorCode(){
                roomError = RoomError.RoomNotExist
            });
        }
    }

    void CreateRoom(int peer, CreateRoomMsg msg)
    {
        if(_allUserRooms.ContainsKey(peer))
        {
            _serverSocket.SendMessage(peer, new RoomErrorCode(){
                roomError = RoomError.CreateRoomErrorHasRoom
            });
            return;
        }

        var roomId = ++RoomId;
        var room = new ServerBattleRoom(roomId, msg.roomShowInfo, msg.startBattleMsg,  _serverSocket, msg.setting);
        _allRooms.Add(roomId, room);

        JoinRoom(peer, new JoinRoomMsg(){
            roomId = roomId, joinMessage = msg.join, joinShowInfo = msg.joinShowInfo 
        });

        Console.WriteLine($"CreateRoom:{roomId}");
    }

    List<(ServerBattleRoom, RoomEndReason)> _removeRooms = new List<(ServerBattleRoom, RoomEndReason)>();
    public void OnUpdate(float deltaTime)
    {
        _serverTime += deltaTime;
        _serverSocket.Update();

        foreach(var x in _allRooms.Values)
        {
            x.Update(deltaTime, _serverTime);
        }

        CheckClearRoom();
    }
    float _lastClearRoomTime = 0;

    private void CheckClearRoom()
    {
        if(_serverTime - _lastClearRoomTime < 1)
        {
            return;
        }

        _lastClearRoomTime = _serverTime;

        _removeRooms.Clear();
        foreach(var x in _allRooms.Values)
        {
            var removeRoomReason = x.NeedDestroy(_serverTime);
            if(removeRoomReason != RoomEndReason.None)
            {
                _removeRooms.Add((x, removeRoomReason));
            }
        }

        foreach(var room in _removeRooms)
        {
            RemoveRoom(room.Item1, room.Item2);
        }
    }

    void RemoveRoom(ServerBattleRoom room, RoomEndReason roomEndReason)
    {
        var allPeers = room.AllPeers.ToArray();
        room.ForceClose(roomEndReason == RoomEndReason.RoomMasterLeave ? SyncRoomOptMsg.RoomOpt.MasterLeaveRoomEnd : SyncRoomOptMsg.RoomOpt.RoomEnd);
        _allRooms.Remove(room.RoomId);

        foreach(var x in allPeers)
        {
            _allUserRooms.Remove(x);
        }

        Console.WriteLine($"RemoveRoom:{room.RoomId} {roomEndReason}");
    }
    
    internal string GetStatus()
    {
        return $"房间{_allRooms.Count}个, user{_allUserRooms.Count}个 Total内存:{GC.GetTotalMemory(false) / (1024f * 1024):0.00}M";
    }

    public void Destroy()
    {
        _serverSocket.OnDestroy();
    }
}