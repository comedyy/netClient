
using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

public enum ConnectResult
{
    NotConnect,
    Connecting,
    Refuse,
    Connnected,
    Disconnect,
}

public interface IServerGameSocket : ILifeCircle
{
    int PeerCount{get;}
    int UserCount{get;}
    Action<int> OnPeerDisconnect { get; set; }
    Action<int, TeamConnectParam> OnPeerReconnected { get; set; }
    void SendMessage<T>(IEnumerable<int> peers, T t) where T : INetSerializable;
    void SendMessage<T>(List<int> peers, T t) where T : INetSerializable;
    void SendMessage<T>(int peers, T t) where T : INetSerializable;
    Action<int, NetDataReader> OnReceiveMsg{get;set;}
    Func<RoomListMsg> GetAllRoomList{get;set;}
    Func<int, GetUserStateMsg> GetUserState{get;set;}
    Func<int, GetRoomStateResponse> GetRoomState{get;set;}
}

public interface ILifeCircle
{
    void Start();
    void Update();
    void OnDestroy();
}


struct SendItem
{
    public float addTime;
    public byte[] bytes;
}

struct ReceiveItem
{
    public float addTime;
    public byte[] bytes;
}

interface IConnectionCount
{
    int Count{get;}
}
