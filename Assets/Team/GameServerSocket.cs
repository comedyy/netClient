using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;

public class GameServerSocket : IServerGameSocket, INetEventListener, INetLogger
{
    private NetManager _netServer;
    private NetDataWriter _dataWriter;
    private int _maxUserConnected;
    private int _port;

    Dictionary<NetPeer, int> _lookupPeerToId = new Dictionary<NetPeer, int>();
    Dictionary<int, NetPeer> _lookupIdToPeer = new Dictionary<int, NetPeer>();

    public GameServerSocket(int countUser, int port)
    {
        this._maxUserConnected = countUser;
        this._port = port;
        NetDebug.Logger = this;
    }

    #region ILifeCircle
    public void Start()
    {
        _dataWriter = new NetDataWriter();
        _netServer = new NetManager(this);
        _netServer.AutoRecycle = true;
        _netServer.UnconnectedMessagesEnabled = true;
        _netServer.Start(_port);
        _netServer.UpdateTime = 15;

        Console.WriteLine($"start port:{_port}");
    }

    public void Update()
    {
        _netServer.PollEvents();
    }

    public void OnDestroy()
    {
        if (_netServer != null)
            _netServer.Stop();
    }
#endregion
    
#region IMessageSendReceive
    public Action<int, NetDataReader> OnReceiveMsg{get;set;}
    
    public void SendMessage<T>(List<int> list, T t) where T : INetSerializable
    {
        #if UNITY_EDITOR
        UnityEngine.Profiling.Profiler.BeginSample("NETBATTLE_GameServerSocket.SendMessage");
        #endif

        _dataWriter.Reset();
        _dataWriter.Put(t);

        for (int i = 0; i < list.Count; i++)
        {
            if(_lookupIdToPeer.TryGetValue(list[i], out var peer))
            {
                peer.Send(_dataWriter, DeliveryMethod.ReliableOrdered);
            }
        }

        #if UNITY_EDITOR
        UnityEngine.Profiling.Profiler.EndSample();
        #endif
    }

    public void SendMessage<T>(IEnumerable<int> list, T t) where T : INetSerializable
    {
        #if UNITY_EDITOR
        UnityEngine.Profiling.Profiler.BeginSample("NETBATTLE_GameServerSocket.SendMessage");
        #endif

        _dataWriter.Reset();
        _dataWriter.Put(t);

        foreach (var id in list)
        {
            if(_lookupIdToPeer.TryGetValue(id, out var peer))
            {
                peer.Send(_dataWriter, DeliveryMethod.ReliableOrdered);
            }
        }

        #if UNITY_EDITOR
        UnityEngine.Profiling.Profiler.EndSample();
        #endif
    }

    public void SendMessage<T>(int id, T t) where T : INetSerializable
    {
        #if UNITY_EDITOR
        UnityEngine.Profiling.Profiler.BeginSample("NETBATTLE_GameServerSocket.SendMessage");
        #endif

        if(_lookupIdToPeer.TryGetValue(id, out var peer))
        {
            _dataWriter.Reset();
            _dataWriter.Put(t);
            peer.Send(_dataWriter, DeliveryMethod.ReliableOrdered);
        }

        #if UNITY_EDITOR
        UnityEngine.Profiling.Profiler.EndSample();
        #endif
    }
#endregion

#region INetEventListener
    public void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine("[SERVER] We have new peer " + peer.EndPoint);
        _lookupPeerToId.Add(peer, 0);
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
    {
         Console.WriteLine("[SERVER] error " + socketErrorCode);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
        UnconnectedMessageType messageType)
    {
        var msgType = (MsgType1)reader.PeekByte();
        if(msgType == MsgType1.GetAllRoomList && GetAllRoomList != null)
        {
            var msg = GetAllRoomList();
            _dataWriter.Reset();
            _dataWriter.Put(msg);
            _netServer.SendUnconnectedMessage(_dataWriter, remoteEndPoint);
        }
        else if(msgType == MsgType1.GetRoomState && GetRoomState != null)
        {
            var msg = GetRoomState(reader.Get<GetRoomStateMsg>().idRoom);
            _dataWriter.Reset();
            _dataWriter.Put(msg);
            _netServer.SendUnconnectedMessage(_dataWriter, remoteEndPoint); 
        }
        else if(msgType == MsgType1.GetUserState && GetUserState != null)
        {
            var msg = GetUserState(reader.Get<GetUserStateMsg>().userId);
            _dataWriter.Reset();
            _dataWriter.Put(msg);
            _netServer.SendUnconnectedMessage(_dataWriter, remoteEndPoint);
        }
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        if(_lookupPeerToId.Count >= _maxUserConnected)
        {
            request.Reject();
            return;
        }

        request.AcceptIfKey("wsa_game");
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
         Console.WriteLine("[SERVER] peer disconnected " + peer.EndPoint + ", info: " + disconnectInfo.Reason);
        if(_lookupPeerToId.TryGetValue(peer, out var id))
        {
            _lookupIdToPeer.Remove(id);
            _lookupPeerToId.Remove(peer);
            OnPeerDisconnect?.Invoke(id);
        }
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        try
        {
            var msgType = (MsgType1)reader.PeekByte();
            if(msgType == MsgType1.SetUserId)
            {
                var msg = reader.Get<RoomUserIdMsg>();
                _lookupIdToPeer[msg.userId] = peer;
                _lookupPeerToId[peer] = msg.userId;

                OnPeerReconnected(msg.userId, msg.connectParam);
                return;
            }

            if(_lookupPeerToId.TryGetValue(peer, out var id) && id > 0)
            {
                OnReceiveMsg(id, reader);
            }
            else
            {
                #if UNITY_2017_1_OR_NEWER
                UnityEngine.Debug.LogError("收到不存在的id");
                #else
                Console.WriteLine("收到不存在的id");
                #endif
            }
        }
        catch(Exception e)
        {
            #if UNITY_2017_1_OR_NEWER
            UnityEngine.Debug.LogError(e.Message + "\n" + e.StackTrace );
            #else
            Console.WriteLine(e.Message + "\n" + e.StackTrace );
            #endif
        }
    }

    #endregion

    public int PeerCount => _lookupPeerToId.Count;
    public int UserCount => _lookupPeerToId.Count;

    public Action<int> OnPeerDisconnect{get;set;}
    public Action<int, TeamConnectParam> OnPeerReconnected{get;set;}
    public Func<RoomListMsg> GetAllRoomList{get;set;}
    public Func<int, GetUserStateMsg> GetUserState{get;set;}
    public Func<int, GetRoomStateResponse> GetRoomState{get;set;}

    public void WriteNet(NetLogLevel level, string str, params object[] args)
    {
        if(level == NetLogLevel.Error)
        {
            #if UNITY_EDITOR
            UnityEngine.Debug.LogError($"{str} {string.Join(",", args)}");
            #else
            Console.WriteLine($"{str} {string.Join(",", args)}");
            #endif
        }
        else
        {
            // ignore
        }
    }
}
