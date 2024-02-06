using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib.Utils;

public enum MsgType1 : byte
{
    FrameMsg = 2,
    ServerFrameMsg = 3,
    HashMsg = 4,
    ReadyForNextStage = 5,
    ServerReadyForNextStage = 6,
    PauseGame = 7,
    FinishCurrentStage = 9,         // 完成当前的stage小关
    ServerEnterLoading = 10,  // 完成当前的stage小关, 服务器回包
    Unsync = 11,
    ServerReConnect = 12,
    ServerMsgEnd___ = 100, // 服务器消息最后

    CreateRoom = 101,
    JoinRoom = 102,
    StartRequest = 103,
    SyncRoomMemberList = 104,
    GetAllRoomList = 105,   // 获得所有的房间列表，无连接
    SetSpeed = 107,
    RoomStartBattle = 108,
    ServerClose = 109,
    ErrorCode = 110,
    SetUserId = 111,
    KickUser = 112,
    LeaveUser = 113,
    RoomReady = 114,
    GetUserState = 115,    // 查询玩家状态。无连接
    RoomEventSync = 116, // 房间的事件通知
    RoomChangeUserPos = 117,
    RoomSyncLoadingProcess = 118,
    GetRoomState = 119,   // 获取房间状态，无连接
    GetRoomStateResponse = 119,   // 获取房间状态，无连接
    GetUserInfo = 120, 
    GetUserInfoResponse = 120, 
    UserReloadServerOK = 121, // 客户端恢复加载了。
    UpdateMemberInfo = 122,
    SyncUpdateAiHelper = 123,     // 房间ai托管同步
}

[Serializable]
public struct FrameHashItem
{
    public byte hashType;
    public int hash;

    public List<int> listValue;
    public List<short> lstParamIndex;
    public List<string> lstParam;
    public List<int> listEntity;

    public void Write(NetDataWriter writer)
    {
        writer.Put(hashType);
        writer.Put(hash);

        if (listValue == null)
        {
            writer.Put((short)-1);
            return;
        }

        writer.Put((short)listValue.Count);
        for (int i = 0; i < listValue.Count; i++)
        {
            writer.Put(listValue[i]);
        }

        writer.Put((short)listEntity.Count);
        for (int i = 0; i < listEntity.Count; i++)
        {
            writer.Put(listEntity[i]);
        }

        if(lstParamIndex == null)
        {
            writer.Put((short)-1);
            writer.Put((short)lstParam.Count);
            for (int i = 0; i < lstParam.Count; i++)
            {
                writer.Put(lstParam[i]);
            }
        }
        else
        {
            writer.Put((short)lstParamIndex.Count);
            for (int i = 0; i < lstParamIndex.Count; i++)
            {
                writer.Put(lstParamIndex[i]);
            }
        }
    }

    public void Read(NetDataReader reader)
    {
        hashType = reader.GetByte();
        hash = reader.GetInt();

        var listCount = reader.GetShort();
        if (listCount == -1) return;

        listValue = new List<int>();
        for (int i = 0; i < listCount; i++)
        {
            listValue.Add(reader.GetInt());
        }

        var listCount1 = reader.GetShort();
        listEntity = new List<int>();
        for (int i = 0; i < listCount1; i++)
        {
            listEntity.Add(reader.GetInt());
        }

        var listCount2 = reader.GetShort();
        if(listCount2 == -1)
        {
            listCount2 = reader.GetShort();
            lstParam = new List<string>();
            for (int i = 0; i < listCount2; i++)
            {
                lstParam.Add(reader.GetString());
            }
        }
        else
        {
            lstParamIndex = new List<short>();
            for (int i = 0; i < listCount2; i++)
            {
                lstParamIndex.Add(reader.GetShort());
            }
        }
    }
    public static bool operator ==(FrameHashItem item1, FrameHashItem item2)
    {
        if (item1.hash != item2.hash) return false;

        if (item1.listEntity != null && item2.listEntity != null)
        {
            if (item1.listEntity.Count != item2.listEntity.Count) return false;
            for (int i = 0; i < item1.listEntity.Count; i++)
            {
                if (item1.listEntity[i] != item1.listEntity[i]) return false;
            }
        }

        if (item1.listValue != null && item2.listValue != null)
        {
            if (item1.listValue.Count != item2.listValue.Count) return false;
            for (int i = 0; i < item1.listValue.Count; i++)
            {
                if (item1.listValue[i] != item1.listValue[i]) return false;
            }
        }

        return true;
    }

    public static bool operator !=(FrameHashItem item1, FrameHashItem item2)
    {
        return !(item1 == item2);
    }

    public string GetString(List<string> symbol)
    {
        if (listValue != null)
        {
            if(listEntity != null && lstParamIndex != null && lstParamIndex.Count > 0 && symbol != null)
            {
                return $"{(CheckSumType)hashType} Hash:{hash} \n listValue：{string.Join("!", listValue)} \n{string.Join("!", listEntity.Select(m=>(m>>16, m & 0xffff)))} \n{string.Join("\n", lstParamIndex.Select(m=>symbol[m]))}";
            }
            else if(listEntity != null && lstParam != null && lstParam.Count > 0)
            {
                return $"{(CheckSumType)hashType} Hash:{hash} \n listValue：{string.Join("!", listValue)} \n{string.Join("!", listEntity.Select(m=>(m>>16, m & 0xffff)))} \n{string.Join("\n", lstParam)}";
            }
            else if(listEntity != null)
            {
                return $"{(CheckSumType)hashType} Hash:{hash} \n listValue：{string.Join("!", listValue)} \n{string.Join("!", listEntity.Select(m=>(m>>16, m & 0xffff)))}";
            }
            else
            {
                return $"{(CheckSumType)hashType} Hash:{hash} \n listValue：{string.Join("!", listValue)} \n";
            }
        }
        else
        {
            return $"{(CheckSumType)hashType} Hash:{hash}";
        }
    }
}

[Serializable]
public struct FrameHash : INetSerializable
{
    public static Queue<FrameHashItem[]> Pool = new Queue<FrameHashItem[]>();
    public int frame;
    public int id;
    public int hash;
    public FrameHashItem[] allHashItems;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.HashMsg);
        writer.Put(frame);
        writer.Put(id);
        writer.Put(hash);

        int count = allHashItems == null ? 0 : allHashItems.Length;
        writer.Put(count);
        for (int i = 0; i < count; i++)
        {
            allHashItems[i].Write(writer);
        }
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        frame = reader.GetInt();
        id = reader.GetInt();
        hash = reader.GetInt();

        var count = reader.GetInt();
        if (count > 0)
        {
            allHashItems = Pool.Count > 0 ? Pool.Dequeue() : new FrameHashItem[count];
            for (int i = 0; i < count; i++)
            {
                allHashItems[i] = default;
                allHashItems[i].Read(reader);
            }
        }
    }
}


public struct FinishRoomMsg : INetSerializable
{
    public int stageValue;
    internal int id;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.FinishCurrentStage);
        writer.Put(stageValue);
        writer.Put(id);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        stageValue = reader.GetInt();
        id = reader.GetInt();
    }
}


public struct ReadyStageMsg : INetSerializable
{
    public int stageIndex;
    internal int id;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ReadyForNextStage);
        writer.Put(stageIndex);
        writer.Put(id);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        stageIndex = reader.GetInt();
        id = reader.GetInt();
    }
}


public struct PauseGameMsg : INetSerializable
{
    public bool pause;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.PauseGame);
        writer.Put(pause);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        pause = reader.GetBool();
    }
}

public struct ServerReadyForNextStage : INetSerializable
{
    public int stageIndex;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ServerReadyForNextStage);
        writer.Put(stageIndex);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        stageIndex = reader.GetInt();
    }
}


public struct ServerEnterLoading : INetSerializable
{
    public int frameIndex;
    public int stage;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ServerEnterLoading);
        writer.Put(frameIndex);
        writer.Put(stage);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        frameIndex = reader.GetInt();
        stage = reader.GetInt();
    }
}


public struct ServerPackageItem : INetSerializable
{
    public ushort frame;
    public List<MessageItem> list;

    // server write 
    public FrameMsgBuffer clientFrameMsgList;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ServerFrameMsg);
        writer.Put(frame);
        var count = clientFrameMsgList.Count;
        writer.Put((byte)count);
        clientFrameMsgList.WriterToWriter(writer, frame);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        frame = reader.GetUShort();
        var count = reader.GetByte();
        if (count > 0)
        {
            list = new List<MessageItem>();
            for (int i = 0; i < count; i++)
            {
                list.Add(MessageItem.FromReader(reader));
            }
        }
    }
}


public struct ServerCloseMsg : INetSerializable
{
    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ServerClose);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
    }
}

public struct ServerReconnectMsg : INetSerializable
{
    public int startFrame;
    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ServerReConnect);
        writer.Put(startFrame);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        startFrame = reader.GetInt();
    }
}


public struct ServerReconnectMsgResponse : INetSerializable
{
    public int startFrame;
    public List<byte[]> bytes;
    public IntPair2[] stageFinishedFrames;
    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ServerReConnect);
        writer.Put(startFrame);
        writer.Put(bytes.Count);
        for(int i = 0; i < bytes.Count; i++)
        {
            writer.PutBytesWithLength(bytes[i]);
        }

        IntPair2.SerializeArray(writer, stageFinishedFrames);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        startFrame = reader.GetInt();

        var size = reader.GetInt();
        bytes = new List<byte[]>();
        for(int i = 0; i < size; i++)
        {
            bytes.Add(reader.GetBytesWithLength());
        }

        stageFinishedFrames = IntPair2.DeserializeArray(reader);
    }
}

public struct IntPair2 : INetSerializable
{
    public int Item1;
    public int Item2;

    public void Deserialize(NetDataReader reader)
    {
        Item1 = reader.GetInt();
        Item2 = reader.GetInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Item1);
        writer.Put(Item2);
    }

    public static void SerializeArray(NetDataWriter writer, IntPair2[] pairs)
    {
        ushort count = pairs == null ? (ushort)0 : (ushort)pairs.Length;
        writer.Put(count);
        for(int i = 0; i < count; i++)
        {
            writer.Put(pairs[i]);
        }
    }

    public static IntPair2[] DeserializeArray(NetDataReader reader)
    {
        ushort count = reader.GetUShort();
        IntPair2[] intPair2s = new IntPair2[count];
        for(int i = 0; i < count; i++)
        {
            intPair2s[i] = reader.Get<IntPair2>();
        }

        return intPair2s;
    }
}




