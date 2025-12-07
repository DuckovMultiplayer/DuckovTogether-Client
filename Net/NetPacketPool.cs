















using LiteNetLib.Utils;
using System.Collections.Concurrent;

namespace EscapeFromDuckovCoopMod.Net;





public static class NetPacketPool
{
    private static readonly ConcurrentBag<NetDataWriter> _writerPool = new();
    private const int MAX_POOL_SIZE = 100; 

    
    
    
    public static NetDataWriter GetWriter()
    {
        if (_writerPool.TryTake(out var writer))
        {
            writer.Reset();
            return writer;
        }

        return new NetDataWriter();
    }

    
    
    
    public static void ReturnWriter(NetDataWriter writer)
    {
        if (writer == null) return;

        
        if (_writerPool.Count < MAX_POOL_SIZE)
        {
            writer.Reset();
            _writerPool.Add(writer);
        }
    }

    
    
    
    public static PoolStats GetStats()
    {
        return new PoolStats
        {
            AvailableCount = _writerPool.Count,
            MaxSize = MAX_POOL_SIZE
        };
    }

    
    
    
    public static void Clear()
    {
        while (_writerPool.TryTake(out _))
        {
            
        }
    }

    public struct PoolStats
    {
        public int AvailableCount;
        public int MaxSize;

        public float UtilizationRate => MaxSize > 0 ? (float)(MaxSize - AvailableCount) / MaxSize : 0f;

        public override string ToString()
        {
            return $"Pool: {AvailableCount}/{MaxSize} available ({UtilizationRate:P1} in use)";
        }
    }
}

