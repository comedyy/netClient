using System;
using LiteNetLib.Utils;

public enum MessageBit : ushort
{
    Pos = 1 << 0,
}

[Serializable]
public struct MessageItem
{
    public uint id;
    public MessageBit messageBit;
    public MessagePosItem posItem;
    
    public static void ToWriter(NetDataWriter writer, MessageItem messageItem)
    {
        writer.Put((ushort)messageItem.messageBit);
        writer.Put((byte)messageItem.id);

        if ((messageItem.messageBit & MessageBit.Pos) > 0)
        {
            writer.Put(messageItem.posItem.posX);
            writer.Put(messageItem.posItem.posY);
            writer.Put(messageItem.posItem.endMoving);
        }
    }

    public static MessageItem FromReader(NetDataReader reader)
    {
        MessageBit messageBit = (MessageBit)reader.GetUShort();
        var messageItem = new MessageItem()
        {
            id = reader.GetByte(),
            messageBit = messageBit
        };

        if ((messageBit & MessageBit.Pos) > 0)
        {
            messageItem.posItem = new MessagePosItem()
            {
                posX = reader.GetInt(),
                posY = reader.GetInt(),
                endMoving = reader.GetBool(),
            };
        }

        return messageItem;
    }

}

[Serializable]
public struct MessagePosItem
{
    public int posX;
    public int posY;
    public bool endMoving;

    public static bool operator==(MessagePosItem item1, MessagePosItem item2)
    {
        return item1.posX == item2.posX
            && item1.posY == item2.posY
             && (item1.endMoving == item2.endMoving);
    }
    public static bool operator!=(MessagePosItem item1, MessagePosItem item2)
    {
        return !(item1 == item2);
    }
}

