using DuckovNet;

namespace EscapeFromDuckovCoopMod;

public partial class ModBehaviourF
{
    public bool IsSelfId(string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return false;
        var myId = localPlayerStatus?.EndPoint;
        var client = Net.CoopNetClient.Instance;
        var networkId = client?.NetworkId ?? "";
        
        UnityEngine.Debug.Log($"[IsSelfId] Check: playerId='{playerId}', myId='{myId}', networkId='{networkId}'");
        
        if (!string.IsNullOrEmpty(myId) && playerId == myId) return true;
        if (!string.IsNullOrEmpty(networkId) && playerId == networkId) return true;
        return false;
    }

    public string GetPlayerId(NetPeer peer)
    {
        if (peer == null)
            return localPlayerStatus?.EndPoint ?? Net.CoopNetClient.Instance?.NetworkId ?? "";
        return peer.EndPoint?.ToString() ?? "";
    }

    public void StartNetwork(bool asServer)
    {
        var client = Net.CoopNetClient.Instance;
        if (client == null) return;
        if (!asServer)
            client.Connect(manualIP, port);
    }

    public void StopNetwork()
    {
        Net.CoopNetClient.Instance?.Disconnect();
    }

    public void ConnectToHost(string ip, int targetPort)
    {
        var client = Net.CoopNetClient.Instance;
        if (client == null) return;
        manualIP = ip;
        port = targetPort;
        client.ServerAddress = ip;
        client.ServerPort = targetPort;
        client.Connect(ip, targetPort);
    }

    public void ConfigureLobbyOptions(int maxPlayers) { }
    public LobbyOptions LobbyOptions { get; } = new();

    public void SendVoiceData(Net.HybridNet.VoiceDataMessage message) { }
    public void SendVoiceState(Net.HybridNet.VoiceStateMessage message) { }
    public void MarkPlayerJoinedSuccessfully(NetPeer peer) { }
    public void SetTransportMode(int mode) { }
    public void TryAutoReconnect() { }
    public void SetRelayRoomId(string roomId) { _relayRoomId = roomId; }
    public void CreateVirtualPeerForRelay() { }
    public NetworkTransportMode TransportMode => NetworkTransportMode.Direct;
    public string _relayRoomId { get; set; } = "";
    
    public void SendRaw(NetDataWriter writer)
    {
        var client = Net.CoopNetClient.Instance;
        if (client == null || !client.IsConnected) return;
        client.SendRaw(writer);
    }
}

public class LobbyOptions
{
    public int MaxPlayers { get; set; } = 4;
    public string LobbyName { get; set; } = "";
    public string Password { get; set; } = "";
    public int Visibility { get; set; } = 0;
}

public enum NetworkTransportMode
{
    Direct = 0,
    SteamP2P = 1
}

public static class DedicatedServerMode
{
    public static bool ShouldBroadcastState() => false;
    public static bool ShouldRunHostLogic() => false;
}

public class DuckovTogetherClient : UnityEngine.MonoBehaviour
{
    public static DuckovTogetherClient Instance { get; set; }
    private void Awake() { Instance = this; }
    public bool IsConnected => Net.CoopNetClient.Instance?.IsConnected ?? false;
    public string NetworkId => Net.CoopNetClient.Instance?.NetworkId ?? "";
    public string ConnectionStatus => Net.CoopNetClient.Instance?.ConnectionStatus ?? "";
    public Net.LocalPlayerState LocalPlayer => Net.CoopNetClient.Instance?.LocalPlayer;
    public System.Collections.Generic.Dictionary<string, Net.RemotePlayerState> RemotePlayers 
        => Net.CoopNetClient.Instance?.RemotePlayers;
    public System.Collections.Generic.Dictionary<int, Net.RemoteAIState> RemoteAI { get; } = new();
    public void Connect(string ip, int port) => Net.CoopNetClient.Instance?.Connect(ip, port);
    public void Disconnect() => Net.CoopNetClient.Instance?.Disconnect();
    public bool IsLocal(string id) => ModBehaviourF.Instance?.IsSelfId(id) ?? false;
    public string MyId => Net.CoopNetClient.Instance?.NetworkId ?? "";
    public void SendJson<T>(T data) { }
    public void SendItemPickup(int containerId, int slotIndex, int itemTypeId, int count) { }
    public void SendItemDrop(int itemTypeId, int count, UnityEngine.Vector3 position) { }
    public void SendDoorInteract(int doorId, bool isOpen) { }
    public void SendWeaponFire(int weaponId, UnityEngine.Vector3 origin, UnityEngine.Vector3 direction, int ammoType) { }
    public void SendDamage(int targetType, int targetId, float damage, string damageType, UnityEngine.Vector3 hitPoint) { }
    public event System.Action OnConnected;
    public event System.Action<string> OnDisconnected;
}

public static class ClientEvents
{
    public static void RegisterHandler<T>(System.Action<T> handler) { }
    public static void UnregisterHandler<T>(System.Action<T> handler) { }
    public static event System.Action<object> OnItemPickup;
    public static event System.Action<object> OnItemDrop;
    public static event System.Action<object> OnLootFullSync;
    public static event System.Action<object> OnContainerContents;
    public static event System.Action<object> OnRemoteWeaponFire;
    public static event System.Action<object> OnDamageReceived;
    public static event System.Action<object> OnGrenadeThrow;
    public static event System.Action<object> OnGrenadeExplode;
}

public class ClientMessageRouter
{
    public static ClientMessageRouter Instance { get; } = new();
    public void Init() { }
}
