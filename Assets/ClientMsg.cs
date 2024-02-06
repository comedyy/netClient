using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib.Utils;

public struct PackageItem : INetSerializable
{
    public MessageItem messageItem;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.FrameMsg);

        MessageItem.ToWriter(writer, messageItem);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        messageItem = MessageItem.FromReader(reader);
    }
}

public struct JoinMessage : INetSerializable
{
    public string UserName;
    public uint userId;
  
    
    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put(UserName);
        writer.Put(userId);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        UserName = reader.GetString();
        userId = reader.GetUInt();
    }

    internal IntPair2[] GetConditionParam()
    {
        return null;
    }
}

public struct BattleStartMessage : INetSerializable
{
    public JoinMessage[] joins;
    public uint seed;
    public string guid;
   

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        var joinLength = joins == null ? 0 : joins.Length;
        writer.Put((byte)joinLength);
        for (int i = 0; i < joinLength; i++)
        {
            writer.Put(joins[i]);
        }
        writer.Put(seed);
        writer.Put(guid);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        joins = new JoinMessage[reader.GetByte()];
        for (int i = 0; i < joins.Length; i++)
        {
            joins[i] = reader.Get<JoinMessage>();
        }

        seed = reader.GetUInt();
        guid = reader.GetString();
    }
}

public partial struct RoomUser : IPartialStructDeserialize
{
    public ClientUserJoinShowInfo _clientShowInfo;
    public string name => _clientShowInfo.name;
    public uint HeroId => _clientShowInfo.HeroId;
    public uint heroLevel => _clientShowInfo.heroLevel;
    public uint heroStar => _clientShowInfo.heroStar;

    public void OnDeserialize()
    {
        _clientShowInfo = ClientBattleRoomMgr.ReadObj<ClientUserJoinShowInfo>(userInfo);
    }
}

public struct ClientUserJoinShowInfo : INetSerializable
{
    public string name;
    public uint HeroId;
    public uint heroLevel;
    public uint heroStar;

    public void Deserialize(NetDataReader reader)
    {
        name = reader.GetString();
        HeroId = reader.GetUInt();
        heroLevel = reader.GetUInt();
        heroStar = reader.GetUInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(name);
        writer.Put(HeroId);
        writer.Put(heroLevel);
        writer.Put(heroStar);
    }
}


interface IPartialStructDeserialize
{
    void OnDeserialize();
}

public partial struct UpdateRoomMemberList : IPartialStructDeserialize
{
    public ClientRoomShowInfo ClientRoomShowInfo;
    public int roomType => ClientRoomShowInfo.roomType;
    public int roomLevel => ClientRoomShowInfo.roomLevel;
    public int activityId => ClientRoomShowInfo.activityId;
    public string version => ClientRoomShowInfo.version;

    public void OnDeserialize()
    {
        if(roomShowInfo.Length == 0) return;

        ClientRoomShowInfo = ClientBattleRoomMgr.ReadObj<ClientRoomShowInfo>(roomShowInfo);
    }
}


public struct ClientRoomShowInfo : INetSerializable
{
    public int roomType;
    public int roomLevel;
    public int activityId;
    public string version;

    public void Deserialize(NetDataReader reader)
    {
        roomType = reader.GetInt();
        roomLevel = reader.GetInt();
        activityId = reader.GetInt();
        version = reader.GetString();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(roomType);
        writer.Put(roomLevel);
        writer.Put(activityId);
        writer.Put(version);
    }
}