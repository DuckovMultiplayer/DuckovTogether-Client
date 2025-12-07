using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net;

public class DuckovTogetherBootstrap : MonoBehaviour
{
    private static DuckovTogetherBootstrap _instance;
    private static bool _initialized;
    
    public static void Initialize()
    {
        if (_initialized) return;
        
        var go = new GameObject("DuckovTogetherClient");
        DontDestroyOnLoad(go);
        
        go.AddComponent<DuckovTogetherClient>();
        go.AddComponent<RemotePlayerManager>();
        go.AddComponent<RemoteAIManager>();
        go.AddComponent<ClientCombatManager>();
        go.AddComponent<ClientItemManager>();
        go.AddComponent<ClientWorldManager>();
        
        _instance = go.AddComponent<DuckovTogetherBootstrap>();
        _initialized = true;
        
        ClientMessageRouter.Instance = new ClientMessageRouter();
        
        Debug.Log("[DuckovTogether] Client systems initialized");
    }
    
    public static void Connect(string address, int port, string playerName = "Player")
    {
        if (!_initialized) Initialize();
        
        var client = DuckovTogetherClient.Instance;
        if (client == null)
        {
            Debug.LogError("[DuckovTogether] Client not initialized");
            return;
        }
        
        client.LocalPlayer.PlayerName = playerName;
        client.Connect(address, port);
    }
    
    public static void Disconnect()
    {
        DuckovTogetherClient.Instance?.Disconnect();
        RemotePlayerManager.Instance?.RemoveAllRemotePlayers();
        RemoteAIManager.Instance?.RemoveAllAI();
        ClientItemManager.Instance?.ClearDroppedItems();
    }
    
    public static bool IsConnected => DuckovTogetherClient.Instance?.IsConnected ?? false;
    
    public static string GetConnectionStatus()
    {
        return DuckovTogetherClient.Instance?.ConnectionStatus ?? "Not initialized";
    }
    
    public static int GetPing()
    {
        return DuckovTogetherClient.Instance?.LocalPlayer?.Latency ?? 0;
    }
    
    public static int GetRemotePlayerCount()
    {
        return RemotePlayerManager.Instance?.GetRemotePlayerCount() ?? 0;
    }
    
    private void OnApplicationQuit()
    {
        Disconnect();
    }
}

public static class DuckovTogetherAPI
{
    public static void Init() => DuckovTogetherBootstrap.Initialize();
    
    public static void Connect(string ip, int port, string playerName = "Player")
        => DuckovTogetherBootstrap.Connect(ip, port, playerName);
    
    public static void Disconnect() => DuckovTogetherBootstrap.Disconnect();
    
    public static bool IsConnected => DuckovTogetherBootstrap.IsConnected;
    
    public static string Status => DuckovTogetherBootstrap.GetConnectionStatus();
    
    public static int Ping => DuckovTogetherBootstrap.GetPing();
    
    public static int PlayerCount => DuckovTogetherBootstrap.GetRemotePlayerCount();
    
    public static void SendChat(string message)
    {
        DuckovTogetherClient.Instance?.SendJson(new { type = "chat", message = message });
    }
    
    public static void SetPlayerName(string name)
    {
        var client = DuckovTogetherClient.Instance;
        if (client != null)
        {
            client.LocalPlayer.PlayerName = name;
        }
    }
    
    public static event Action OnConnected
    {
        add { if (DuckovTogetherClient.Instance != null) DuckovTogetherClient.Instance.OnConnected += value; }
        remove { if (DuckovTogetherClient.Instance != null) DuckovTogetherClient.Instance.OnConnected -= value; }
    }
    
    public static event Action<string> OnDisconnected
    {
        add { if (DuckovTogetherClient.Instance != null) DuckovTogetherClient.Instance.OnDisconnected += value; }
        remove { if (DuckovTogetherClient.Instance != null) DuckovTogetherClient.Instance.OnDisconnected -= value; }
    }
}
