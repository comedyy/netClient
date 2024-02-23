
using System;
using LiteNetLib.Utils;

public interface IClientGameSocket : ILifeCircle
{
    ConnectResult connectResult { get; }
    int RoundTripTime { get; }
    void SendMessage<T>(T t) where T : INetSerializable;
    void SendMessageNotReliable<T>(T t) where T : INetSerializable;
    void SendUnConnectedMessage<T>(T t) where T : INetSerializable;
    Action<NetDataReader> OnReceiveMsg { get; set; }
    Action OnConnected { get; set; }
    Action<ConnectErrorCode> OnDisConnected { get; set; }
    void Connect(ushort version);
    void DisConnect();
}
