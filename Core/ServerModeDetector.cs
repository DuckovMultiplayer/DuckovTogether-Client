using EscapeFromDuckovCoopMod.Net;

namespace EscapeFromDuckovCoopMod.Core;

public static class ServerModeDetector
{
    private static bool _isConnectedToDedicatedServer;
    
    public static bool IsConnectedToDedicatedServer
    {
        get => _isConnectedToDedicatedServer;
        set
        {
            _isConnectedToDedicatedServer = value;
            Debug.Log($"[ServerMode] Connected to dedicated server: {value}");
        }
    }
    
    public static bool IsConnected => CoopNetClient.Instance != null && CoopNetClient.Instance.IsConnected;
    
    public static void OnConnectedToServer(bool isDedicated)
    {
        IsConnectedToDedicatedServer = isDedicated;
        if (isDedicated)
        {
            Debug.Log("[ServerMode] Client mode: All game logic handled by server");
        }
    }
    
    public static void OnDisconnected()
    {
        IsConnectedToDedicatedServer = false;
    }
    
    public static bool ShouldRunLocalLogic()
    {
        if (IsConnectedToDedicatedServer) return false;
        if (!IsConnected) return true;
        return false;
    }
    
    public static bool ShouldProcessAI() => ShouldRunLocalLogic();
    public static bool ShouldSpawnAI() => ShouldRunLocalLogic();
    public static bool ShouldProcessLoot() => ShouldRunLocalLogic();
    public static bool ShouldSpawnLoot() => ShouldRunLocalLogic();
    public static bool ShouldProcessDamage() => ShouldRunLocalLogic();
    public static bool ShouldBroadcastState() => false;
}
