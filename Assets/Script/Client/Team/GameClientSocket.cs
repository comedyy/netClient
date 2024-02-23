using System.Net;
using System.Net.Sockets;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System;

public class GameClientSocket : IClientGameSocket, INetEventListener, INetLogger
{
    private NetManager _netClient;
    private NetDataWriter _dataWriter;

    public int RoundTripTime => _netClient.FirstPeer == null ? -1 : _netClient.FirstPeer.RoundTripTime;

    int _debugLatency = 0;
    IPEndPoint _endPoint;

    public void SetIp(string ip, int port)
    {
        var isEndPointNull = _endPoint == null;
        var endPointChanged = false;

        if(!isEndPointNull)
        {
            endPointChanged = !_endPoint.Address.Equals(IPAddress.Parse(ip)) || _endPoint.Port != port;
        }

        if(isEndPointNull || endPointChanged)
        {
            _endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }
    }

    public GameClientSocket(string targetIp, int port, int delay)
    {
        NetDebug.Logger = this;
        
        if(string.IsNullOrEmpty(targetIp))
        {
            throw new Exception("targetip 为空");
        }

        _debugLatency = delay;
        SetIp(targetIp, port);

        _netClient = new NetManager(this);
        _dataWriter = new NetDataWriter();
        _netClient.UnconnectedMessagesEnabled = true;
        _netClient.AutoRecycle = true;
        _netClient.UpdateTime = 15;
        _netClient.SimulateLatency = false;
        _netClient.Start();
    }

#region ILifeCircle
    public void Start()
    {
    }

    public void Connect(ushort msgVersionId)
    {
        if(connectResult == ConnectResult.Connecting || connectResult == ConnectResult.Connnected)
        {
            return;
        }

        connectResult = ConnectResult.Connecting;
        NetDataWriter writer = new NetDataWriter();
        writer.Put("wsa_game");
        writer.Put(msgVersionId);
        _netClient.Connect(_endPoint, writer);

        ClientBattleRoomMgr.Instance().LogMessage("connect " + _endPoint);
    }

    public void DisConnect()
    {
        _netClient.DisconnectAll();
    }

    public void Update(float deltaTime)
    {
        _netClient.PollEvents();
    }

    public void OnDestroy()
    {
        if (_netClient != null)
        {
            _netClient.Stop();
            _netClient = null;
        }
    }
#endregion

#region IMessageSendReceive
    public Action<NetDataReader> OnReceiveMsg{get;set;}

    public ConnectResult connectResult{get; private set;} = ConnectResult.NotConnect;
    public Action OnConnected { get;  set; }
    public Action<ConnectErrorCode> OnDisConnected { get;  set; }

    public void SendMessage<T>(T t) where T : INetSerializable
    {
        // Debug.LogError("===>>>>>>> " + typeof(T));
        UnityEngine.Profiling.Profiler.BeginSample("NETBATTLE_GameClientSocket.SendMessage");

        var peer = _netClient.FirstPeer;
        if (peer != null && peer.ConnectionState == ConnectionState.Connected)
        {
            _dataWriter.Reset();
            _dataWriter.Put(t);
            peer.Send(_dataWriter, DeliveryMethod.ReliableOrdered);
        }

        UnityEngine.Profiling.Profiler.EndSample();
    }

    public void SendMessageNotReliable<T>(T t) where T : INetSerializable
    {
        UnityEngine.Profiling.Profiler.BeginSample("NETBATTLE_GameClientSocket.SendMessageNotReliable");
        var peer = _netClient.FirstPeer;
        if (peer != null && peer.ConnectionState == ConnectionState.Connected)
        {
            _dataWriter.Reset();
            _dataWriter.Put(t);
            peer.Send(_dataWriter, DeliveryMethod.Unreliable);
        }
        UnityEngine.Profiling.Profiler.EndSample();
    }

    public void SendUnConnectedMessage<T>(T t) where T : INetSerializable
    {
        _dataWriter.Reset();
        _dataWriter.Put(t);
        _netClient.SendUnconnectedMessage(_dataWriter, _endPoint);
    }
#endregion

#region INetEventListener
    public void OnPeerConnected(NetPeer peer)
    {
        // Debug.LogError("[CLIENT] We connected to " + peer.EndPoint);
        connectResult = ConnectResult.Connnected;

        OnConnected?.Invoke();
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
    {
        Debug.LogError("[CLIENT] We received error " + socketErrorCode);
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        OnReceiveMsg(reader);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        if(!_endPoint.Address.Equals(remoteEndPoint.Address) || _endPoint.Port != remoteEndPoint.Port) return;

        OnReceiveMsg(reader);
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        Debug.LogError("OnConnectionRequest 不应该走到");
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        connectResult = ConnectResult.Disconnect;

        ConnectErrorCode code = ConnectErrorCode.None;
        if(disconnectInfo.AdditionalData != null && disconnectInfo.AdditionalData.AvailableBytes >= 1)
        {
            code = (ConnectErrorCode)disconnectInfo.AdditionalData.GetByte();
        }

        ClientBattleRoomMgr.Instance().LogMessage($"[CLIENT] We disconnected because {disconnectInfo.Reason} {code}");

        OnDisConnected?.Invoke(code);
    }
    #endregion

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
