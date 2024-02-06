using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib.Utils;

public class FrameMsgBuffer
{
    const int TOTAL_LENGTH = 4096;
    byte[] _frameBuffer = new byte[TOTAL_LENGTH]; // 接收buffer
    ushort _position;
    byte _msgCount;
    List<byte[]> _allMessage = new List<byte[]>();

    public void AddFromReader(NetDataReader reader)
    {
        var segment = reader.GetRemainingBytesSegment();
        var length = segment.Count;
        var remain = TOTAL_LENGTH - _position;

        if(remain < length)
        {
            Console.WriteLine($"remain buffer < incomingFrameMsg {remain} {length}");
            return;
        }

        Buffer.BlockCopy(segment.Array, segment.Offset, _frameBuffer, _position, length);
        _position += (ushort)length;
        _msgCount++;
    }

    public byte Count => _msgCount;
    public void WriterToWriter(NetDataWriter writer, int frame)
    {
        writer.Put(_frameBuffer, 0, _position);
        _position = 0;
        _msgCount = 0;

        _allMessage.Add(writer.CopyData());
    }

    internal ServerReconnectMsgResponse GetReconnectMsg(int clientCurrentFrame, Dictionary<int, int> finishedStageFrames)
    {
        List<byte[]> list = new List<byte[]>();
        list.AddRange(_allMessage.GetRange(clientCurrentFrame, _allMessage.Count - clientCurrentFrame));

        ServerReconnectMsgResponse response = new ServerReconnectMsgResponse(){
            startFrame = clientCurrentFrame,
            bytes = list, 
            stageFinishedFrames = finishedStageFrames.Select(m=>new IntPair2(){Item1 = m.Key, Item2 = m.Value}).ToArray()
        };

        return response;
    }
}