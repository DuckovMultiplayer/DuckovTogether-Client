















namespace EscapeFromDuckovCoopMod;




public static class NetDataWriterPool
{
    private static readonly Stack<NetDataWriter> _pool = new();
    private const int MAX_POOL_SIZE = 10;
    private static readonly object _lock = new();

    
    
    
    public static NetDataWriter Get()
    {
        lock (_lock)
        {
            if (_pool.Count > 0)
            {
                var writer = _pool.Pop();
                writer.Reset();
                return writer;
            }
        }

        return new NetDataWriter();
    }

    
    
    
    public static void Return(NetDataWriter writer)
    {
        if (writer == null) return;

        lock (_lock)
        {
            if (_pool.Count < MAX_POOL_SIZE)
            {
                writer.Reset();
                _pool.Push(writer);
            }
        }
    }

    
    
    
    public static void Clear()
    {
        lock (_lock)
        {
            _pool.Clear();
        }
    }

    
    
    
    public static int Count
    {
        get
        {
            lock (_lock)
            {
                return _pool.Count;
            }
        }
    }
}

