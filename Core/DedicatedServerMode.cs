namespace EscapeFromDuckovCoopMod;

public static class DedicatedServerMode
{
    private static bool _isConnectedToDedicatedServer;
    
    public static bool IsConnectedToDedicatedServer
    {
        get => _isConnectedToDedicatedServer;
        set
        {
            _isConnectedToDedicatedServer = value;
            Debug.Log($"[DedicatedServerMode] Connected to dedicated server: {value}");
        }
    }
    
    public static void OnConnectedToServer(bool isDedicated)
    {
        IsConnectedToDedicatedServer = isDedicated;
        if (isDedicated)
        {
            Debug.Log("[DedicatedServerMode] Client mode: All game logic handled by server");
        }
    }
    
    public static void OnDisconnected()
    {
        IsConnectedToDedicatedServer = false;
    }
    
    public static bool ShouldRunHostLogic()
    {
        if (IsConnectedToDedicatedServer) return false;
        
        var service = NetService.Instance;
        if (service == null || !service.networkStarted) return true;
        
        return service.IsServer;
    }
    
    public static bool ShouldProcessAI() => ShouldRunHostLogic();
    public static bool ShouldSpawnAI() => ShouldRunHostLogic();
    public static bool ShouldProcessLoot() => ShouldRunHostLogic();
    public static bool ShouldSpawnLoot() => ShouldRunHostLogic();
    public static bool ShouldProcessDamage() => ShouldRunHostLogic();
    public static bool ShouldBroadcastState() => ShouldRunHostLogic();
}
