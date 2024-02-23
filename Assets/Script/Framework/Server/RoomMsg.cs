using System.Collections.Generic;
using LiteNetLib.Utils;

public class RoomMsgVersion
{
    public const int version = 4;
}

public enum TeamConnectParam
{
    None,
    SyncInfo,
}

public enum ServerSyncType
{
    SyncMsgEventFrame,
    SyncMsgOnlyHasMsg,
}

public enum RoomMasterLeaveOpt
{
    RemoveRoomAndBattle,
    OnlyRemoveRoomBeforeBattle,
    ChangeRoomMater,
}


public struct ServerSetting : INetSerializable
{
    public ServerSyncType syncType;
    public float tick;
    public ushort maxFrame;
    public RoomMasterLeaveOpt masterLeaveOpt;
    public byte maxCount;
    internal byte waitReadyStageTimeMs;
    internal byte waitFinishStageTimeMs;
    public IntPair2[] Conditions;
    public bool keepRoomAfterBattle;
    public byte syncFrameCount;     // 服务器多少帧同步一次数据

    public void Deserialize(NetDataReader reader)
    {
        syncType = (ServerSyncType)reader.GetByte();
        tick = reader.GetFloat();
        maxFrame = reader.GetUShort();
        masterLeaveOpt = (RoomMasterLeaveOpt)reader.GetByte();
        maxCount = reader.GetByte();

        waitReadyStageTimeMs = reader.GetByte();
        waitFinishStageTimeMs = reader.GetByte();
        Conditions = IntPair2.DeserializeArray(reader);
        keepRoomAfterBattle = reader.GetBool();
        syncFrameCount = reader.GetByte();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)syncType);
        writer.Put(tick);
        writer.Put(maxFrame);
        writer.Put((byte)masterLeaveOpt);
        writer.Put(maxCount);
        writer.Put(waitReadyStageTimeMs);
        writer.Put(waitFinishStageTimeMs);
        IntPair2.SerializeArray(writer, Conditions);
        writer.Put(keepRoomAfterBattle);
        writer.Put(syncFrameCount);
    }
}


public struct CreateRoomMsg : INetSerializable
{
    public byte[] startBattleMsg;
    public byte[] join;
    public ServerSetting setting;
    public byte[] joinShowInfo;
    public byte[] roomShowInfo;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte(); // msgHeader

        startBattleMsg = reader.GetBytesWithLength();
        join = reader.GetBytesWithLength();
        
        setting = reader.Get<ServerSetting>();

        joinShowInfo = reader.GetBytesWithLength();
        roomShowInfo = reader.GetBytesWithLength();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.CreateRoom);
        writer.PutBytesWithLength(startBattleMsg);
        writer.PutBytesWithLength(join);

        writer.Put(setting);

        writer.PutBytesWithLength(joinShowInfo);
        writer.PutBytesWithLength(roomShowInfo);
    }
}


public struct JoinRoomMsg : INetSerializable
{
    public int roomId;
    public byte[] joinMessage;
    public byte[] joinShowInfo;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        roomId = reader.GetInt();
        joinMessage = reader.GetBytesWithLength();
        joinShowInfo = reader.GetBytesWithLength();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.JoinRoom);
        writer.Put(roomId);
        writer.PutBytesWithLength(joinMessage);
        writer.PutBytesWithLength(joinShowInfo);
    }
}


public struct UpdateMemberInfoMsg : INetSerializable
{
    public byte[] joinMessage;
    public byte[] joinShowInfo;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        joinMessage = reader.GetBytesWithLength();
        joinShowInfo = reader.GetBytesWithLength();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.UpdateMemberInfo);
        writer.PutBytesWithLength(joinMessage);
        writer.PutBytesWithLength(joinShowInfo);
    }
}

public struct StartBattleRequest : INetSerializable
{
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.StartRequest);
    }
}

public struct RoomStartBattleMsg : INetSerializable
{
    public byte[] StartMsg;
    public List<byte[]> joinMessages;
    public bool isReconnect;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();

        StartMsg = reader.GetBytesWithLength();
        var MemberCount = reader.GetByte();
        joinMessages = new List<byte[]>();
        for(int i = 0; i < MemberCount; i++)
        {
            joinMessages.Add(reader.GetBytesWithLength());
        }
        isReconnect = reader.GetBool();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.RoomStartBattle);

        writer.PutBytesWithLength(StartMsg);
        writer.Put((byte)joinMessages.Count);
        for(int i = 0; i < joinMessages.Count; i++)
        {
            writer.PutBytesWithLength(joinMessages[i]);
        }
        writer.Put(isReconnect);
    }
}

public partial struct RoomUser : INetSerializable
{
    public byte[] userInfo;
    public bool isOnLine;
    public bool isReady;
    public uint userId;
    public bool needAiHelp;
    
    public void Deserialize(NetDataReader reader)
    {
        isOnLine = reader.GetBool();
        isReady = reader.GetBool();
        userId = reader.GetUInt();
        userInfo = reader.GetBytesWithLength();
        needAiHelp = reader.GetBool();

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX
        OnDeserialize(reader);
#endif
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(isOnLine);
        writer.Put(isReady);
        writer.Put(userId);
        writer.PutBytesWithLength(userInfo);
        writer.Put(needAiHelp);
    }
}

public partial struct UpdateRoomMemberList : INetSerializable
{
    public RoomUser[] userList;
    public int roomId;
    public IntPair2[] conditions;
    public byte[] roomShowInfo;
    public byte AIHelperIndex;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        roomId = reader.GetInt();
        var count = reader.GetInt();
        userList = new RoomUser[count];
        for(int i = 0; i < count; i++)
        {
            userList[i] = reader.Get<RoomUser>();
        }

        conditions = IntPair2.DeserializeArray(reader);
        roomShowInfo = reader.GetBytesWithLength();
        AIHelperIndex = reader.GetByte();

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX
        OnDeserialize(reader);
#endif
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.SyncRoomMemberList);

        writer.Put(roomId);
        var size = userList == null ? 0 : userList.Length;
        writer.Put(size);
        for(int i = 0; i < size; i++)
        {
            writer.Put(userList[i]);
        }

        IntPair2.SerializeArray(writer, conditions);
        writer.PutBytesWithLength(roomShowInfo);
        writer.Put(AIHelperIndex);
    }
}


public struct RoomInfoMsg : INetSerializable
{
    public UpdateRoomMemberList updateRoomMemberList;

    public void Deserialize(NetDataReader reader)
    {
        updateRoomMemberList = reader.Get<UpdateRoomMemberList>();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(updateRoomMemberList);
    }
}


public struct RoomListMsgRequest : INetSerializable
{
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.GetAllRoomList);
    }
}


public struct RoomListMsg : INetSerializable
{
    public RoomInfoMsg[] roomList;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        var count = reader.GetInt();
        roomList = new RoomInfoMsg[count];
        for(int i = 0; i < count; i++)
        {
            roomList[i] = reader.Get<RoomInfoMsg>();
        }
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.GetAllRoomList);

        writer.Put(roomList.Length);
        for(int i = 0; i < roomList.Length; i++)
        {
            writer.Put(roomList[i]);
        }
    }
}

public struct UnSyncMsg : INetSerializable
{
    public string[] unSyncInfos;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();

        var count = reader.GetInt();
        unSyncInfos = new string[count];
        for(int i = 0; i < count; i++)
        {
            var subCount = reader.GetInt();
            List<string> lst = new List<string>();
            for(int j = 0; j < subCount; j++)
            {
                lst.Add(reader.GetString());
            }

            unSyncInfos[i] = string.Join("", lst);
        }
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.Unsync);

        writer.Put(unSyncInfos.Length);
        for(int i = 0; i < unSyncInfos.Length; i++)
        {
            var str = unSyncInfos[i];
            var strLength = str.Length;
            var maxSize = NetDataWriter.StringBufferMaxLength - 10;

            if(strLength >= maxSize)
            {
                // 拆分。
                var count = strLength / maxSize;
                if(maxSize * count != strLength)
                {
                    count ++;
                }

                for(int j = 0; j < count; j++)
                {
                    var isLastOne = i == count - 1;
                    var lastOneSize = strLength - maxSize * (count - 1);

                    writer.Put(str.Substring(j * count, isLastOne ? lastOneSize : maxSize));
                }
            }
            else
            {
                writer.Put(1);
                writer.Put(unSyncInfos[i]);
            }
        }
    }
}
public struct SetServerSpeedMsg : INetSerializable
{
    public int speed;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        speed = reader.GetInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.SetSpeed);
        writer.Put(speed);
    }
}

public enum RoomError : byte
{
    RoomFull = 1,
    JoinRoomErrorHasRoom = 2,
    CreateRoomErrorHasRoom = 3,
    BattleNotExit = 4,
    AuthError = 5,
    ChangeErrorOutOfIndex = 6,
    RoomNotExist = 7,
    EnterRoomButBattleExist = 8,
    LeaveErrorInBattle = 9,
    JoinRoomErrorInsideRoom = 10,
    UpdatFailedMemberNotExist = 11,
}

public struct RoomErrorCode : INetSerializable
{
    public RoomError roomError;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        roomError = (RoomError)reader.GetByte();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ErrorCode);
        writer.Put((byte)roomError);
    }
}

public struct RoomUserIdMsg : INetSerializable
{
    public int userId;
    public TeamConnectParam connectParam;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        userId = reader.GetInt();
        connectParam = (TeamConnectParam)reader.GetByte();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.SetUserId);
        writer.Put(userId);
        writer.Put((byte)connectParam);
    }
}

public struct KickUserMsg : INetSerializable
{
    public int userId;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        userId = reader.GetInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.KickUser);
        writer.Put(userId);
    }
}

public struct UserLeaveRoomMsg : INetSerializable
{

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.LeaveUser);
    }
}


public struct RoomReadyMsg : INetSerializable
{
    public bool isReady;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        isReady = reader.GetBool();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.RoomReady);
        writer.Put(isReady);
    }
}

public struct GetUserStateMsg : INetSerializable
{
    public enum UserState
    {
        None, HasRoom, HasBattle, Querying,
    }

    public UserState state;
    public int userId;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        state = (UserState)reader.GetByte();
        userId = reader.GetInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.GetUserState);
        writer.Put((byte)state);
        writer.Put(userId);
    }
}

public struct SyncRoomOptMsg : INetSerializable
{
    public enum RoomOpt
    {
        None, Kick, Leave, MasterLeaveRoomEnd, RoomEnd, Join
    }

    public RoomOpt state;
    public int param;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        state = (RoomOpt)reader.GetByte();
        param = reader.GetInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.RoomEventSync);
        writer.Put((byte)state);
        writer.Put(param);
    }
}


public struct RoomChangeUserPosMsg : INetSerializable
{
    public byte fromIndex;
    public byte toIndex;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        fromIndex = reader.GetByte();
        toIndex = reader.GetByte();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.RoomChangeUserPos);
        writer.Put(fromIndex);
        writer.Put(toIndex);
    }
}

public struct RoomSyncLoadingProcessMsg : INetSerializable
{
    public int percent;
    public int id;
    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.RoomSyncLoadingProcess);
        writer.Put(percent);
        writer.Put(id);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        percent = reader.GetInt();
        id = reader.GetInt();
    }
}

public struct GetRoomStateMsg : INetSerializable
{
    public int idRoom;
    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.GetRoomState);
        writer.Put(idRoom);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        idRoom = reader.GetInt();
    }
}

public struct GetRoomStateResponse : INetSerializable
{
    public RoomInfoMsg infoMsg;
    public int roomId;
    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.GetRoomStateResponse);
        writer.Put(roomId);
        writer.Put(infoMsg);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        roomId = reader.GetInt();
        infoMsg = reader.Get<RoomInfoMsg>();
    }
}

public struct UserReloadServerOKMsg : INetSerializable
{
    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.UserReloadServerOK);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
    }
}

public struct CreateAutoJoinRobertMsg : INetSerializable
{
    public JoinRoomMsg joinRoomMsg;
    internal int idRobert;
    public void Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        joinRoomMsg = reader.Get<JoinRoomMsg>();
        idRobert = reader.GetInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.CreateAutoJoinRobert);
        writer.Put(joinRoomMsg);
        writer.Put(idRobert);
    }
}

public struct CreateAutoCreateRoomRobertMsg : INetSerializable
{
    public CreateRoomMsg createRoomMsg;
    internal int idRobert;
    public void Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        createRoomMsg = reader.Get<CreateRoomMsg>();
        idRobert = reader.GetInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.CreateAutoCreateRoomRobert);
        writer.Put(createRoomMsg);
        writer.Put(idRobert);
    }
}

