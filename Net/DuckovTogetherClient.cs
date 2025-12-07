using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;
using Newtonsoft.Json;

namespace EscapeFromDuckovCoopMod.Net;

public class DuckovTogetherClient : MonoBehaviour, INetEventListener
{
    public static DuckovTogetherClient Instance { get; private set; }
    
    public NetManager NetManager { get; private set; }
    public NetPeer ServerPeer { get; private set; }
    public NetDataWriter Writer { get; private set; }
    
    public bool IsConnected => ServerPeer != null && ServerPeer.ConnectionState == ConnectionState.Connected;
    public bool IsConnecting { get; private set; }
    public string ConnectionStatus { get; private set; } = "";
    public string NetworkId { get; private set; } = "";
    
    public string ServerAddress { get; set; } = "127.0.0.1";
    public int ServerPort { get; set; } = 9050;
    
    private float _reconnectTimer;
    private float _syncTimer;
    private float _statusTimer;
    private bool _autoReconnect;
    private int _reconnectAttempts;
    private const int MAX_RECONNECT_ATTEMPTS = 5;
    private const float RECONNECT_INTERVAL = 5f;
    private const float SYNC_INTERVAL = 0.05f;
    private const float STATUS_INTERVAL = 1f;
    
    public event Action OnConnected;
    public event Action<string> OnDisconnected;
    public event Action<string> OnConnectionFailed;
    public event Action<string, NetDataReader> OnMessageReceived;
    
    public readonly Dictionary<string, RemotePlayerState> RemotePlayers = new();
    public readonly Dictionary<int, RemoteAIState> RemoteAI = new();
    public LocalPlayerState LocalPlayer { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Writer = new NetDataWriter();
        LocalPlayer = new LocalPlayerState();
    }
    
    private void Update()
    {
        NetManager?.PollEvents();
        
        if (IsConnected)
        {
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= SYNC_INTERVAL)
            {
                SendPlayerSync();
                _syncTimer = 0f;
            }
            
            _statusTimer += Time.deltaTime;
            if (_statusTimer >= STATUS_INTERVAL)
            {
                SendClientStatus();
                _statusTimer = 0f;
            }
        }
        else if (_autoReconnect && !IsConnecting)
        {
            _reconnectTimer += Time.deltaTime;
            if (_reconnectTimer >= RECONNECT_INTERVAL && _reconnectAttempts < MAX_RECONNECT_ATTEMPTS)
            {
                _reconnectTimer = 0f;
                _reconnectAttempts++;
                Connect(ServerAddress, ServerPort);
            }
        }
    }
    
    private void OnDestroy()
    {
        Disconnect();
        if (Instance == this) Instance = null;
    }
    
    public void Connect(string address, int port)
    {
        if (IsConnecting || IsConnected) return;
        
        ServerAddress = address;
        ServerPort = port;
        IsConnecting = true;
        ConnectionStatus = $"Connecting to {address}:{port}...";
        
        try
        {
            if (NetManager != null)
            {
                NetManager.Stop();
            }
            
            NetManager = new NetManager(this)
            {
                AutoRecycle = true,
                EnableStatistics = true,
                UpdateTime = 15,
                DisconnectTimeout = 10000,
                ReconnectDelay = 500,
                MaxConnectAttempts = 5
            };
            
            if (!NetManager.Start())
            {
                IsConnecting = false;
                ConnectionStatus = "Failed to start network";
                OnConnectionFailed?.Invoke(ConnectionStatus);
                return;
            }
            
            NetManager.Connect(address, port, "DuckovTogether_v2");
            Debug.Log($"[DuckovClient] Connecting to {address}:{port}");
        }
        catch (Exception ex)
        {
            IsConnecting = false;
            ConnectionStatus = $"Connection error: {ex.Message}";
            OnConnectionFailed?.Invoke(ConnectionStatus);
            Debug.LogError($"[DuckovClient] Connection error: {ex}");
        }
    }
    
    public void Disconnect()
    {
        _autoReconnect = false;
        _reconnectAttempts = 0;
        
        if (ServerPeer != null)
        {
            NetManager?.DisconnectPeer(ServerPeer);
            ServerPeer = null;
        }
        
        NetManager?.Stop();
        NetManager = null;
        
        IsConnecting = false;
        ConnectionStatus = "Disconnected";
        NetworkId = "";
        
        RemotePlayers.Clear();
        RemoteAI.Clear();
    }
    
    public void EnableAutoReconnect(bool enable)
    {
        _autoReconnect = enable;
        if (!enable)
        {
            _reconnectAttempts = 0;
            _reconnectTimer = 0f;
        }
    }
    
    public void OnPeerConnected(NetPeer peer)
    {
        ServerPeer = peer;
        IsConnecting = false;
        _reconnectAttempts = 0;
        ConnectionStatus = $"Connected to {peer.EndPoint}";
        
        Debug.Log($"[DuckovClient] Connected to server: {peer.EndPoint}");
        OnConnected?.Invoke();
        
        SendClientStatus();
        RequestFullSync();
    }
    
    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        var reason = disconnectInfo.Reason.ToString();
        ConnectionStatus = $"Disconnected: {reason}";
        ServerPeer = null;
        IsConnecting = false;
        
        Debug.Log($"[DuckovClient] Disconnected: {reason}");
        OnDisconnected?.Invoke(reason);
        
        RemotePlayers.Clear();
        RemoteAI.Clear();
    }
    
    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        Debug.LogError($"[DuckovClient] Network error: {socketError}");
        ConnectionStatus = $"Network error: {socketError}";
    }
    
    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        try
        {
            if (reader.AvailableBytes < 1) return;
            
            var msgType = reader.GetByte();
            ProcessMessage(msgType, reader);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DuckovClient] Message processing error: {ex}");
        }
    }
    
    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
    }
    
    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        LocalPlayer.Latency = latency;
    }
    
    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.Reject();
    }
    
    private void ProcessMessage(byte msgType, NetPacketReader reader)
    {
        switch (msgType)
        {
            case 9:
                ProcessJsonMessage(reader);
                break;
            default:
                OnMessageReceived?.Invoke(msgType.ToString(), reader);
                break;
        }
    }
    
    private void ProcessJsonMessage(NetPacketReader reader)
    {
        var json = reader.GetString();
        
        try
        {
            var baseMsg = JsonConvert.DeserializeObject<BaseMessage>(json);
            if (baseMsg == null) return;
            
            switch (baseMsg.type)
            {
                case "setId":
                    HandleSetId(json);
                    break;
                case "playerList":
                    HandlePlayerList(json);
                    break;
                case "player_transform_snapshot":
                    HandlePlayerTransforms(json);
                    break;
                case "player_anim_snapshot":
                    HandlePlayerAnimations(json);
                    break;
                case "player_equipment_snapshot":
                    HandlePlayerEquipment(json);
                    break;
                case "playerHealth":
                    HandlePlayerHealth(json);
                    break;
                case "playerDeath":
                    HandlePlayerDeath(json);
                    break;
                case "playerRespawn":
                    HandlePlayerRespawn(json);
                    break;
                case "playerDisconnect":
                    HandlePlayerDisconnect(json);
                    break;
                case "ai_transform_snapshot":
                    HandleAITransforms(json);
                    break;
                case "ai_anim_snapshot":
                    HandleAIAnimations(json);
                    break;
                case "ai_health_sync":
                    HandleAIHealth(json);
                    break;
                case "weaponFire":
                    HandleWeaponFire(json);
                    break;
                case "playerDamage":
                case "aiDamage":
                    HandleDamage(json);
                    break;
                case "grenadeThrow":
                    HandleGrenadeThrow(json);
                    break;
                case "grenadeExplode":
                    HandleGrenadeExplode(json);
                    break;
                case "itemPickup":
                    HandleItemPickup(json);
                    break;
                case "itemDrop":
                    HandleItemDrop(json);
                    break;
                case "lootFullSync":
                    HandleLootFullSync(json);
                    break;
                case "containerContents":
                    HandleContainerContents(json);
                    break;
                case "sceneLoad":
                    HandleSceneLoad(json);
                    break;
                case "forceSceneLoad":
                    HandleForceSceneLoad(json);
                    break;
                case "worldState":
                    HandleWorldState(json);
                    break;
                case "timeSync":
                    HandleTimeSync(json);
                    break;
                case "weatherSync":
                    HandleWeatherSync(json);
                    break;
                case "doorInteract":
                    HandleDoorInteract(json);
                    break;
                case "kick":
                    HandleKick(json);
                    break;
                default:
                    ClientMessageRouter.Instance?.RouteMessage(baseMsg.type, json);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DuckovClient] JSON parse error: {ex.Message}");
        }
    }
    
    private void HandleSetId(string json)
    {
        var msg = JsonConvert.DeserializeObject<SetIdMsg>(json);
        if (msg != null)
        {
            NetworkId = msg.networkId;
            Debug.Log($"[DuckovClient] Assigned network ID: {NetworkId}");
        }
    }
    
    private void HandlePlayerList(string json)
    {
        var msg = JsonConvert.DeserializeObject<PlayerListMsg>(json);
        if (msg?.players == null) return;
        
        foreach (var player in msg.players)
        {
            if (player.endPoint == NetworkId) continue;
            
            if (!RemotePlayers.TryGetValue(player.endPoint, out var state))
            {
                state = new RemotePlayerState { NetworkId = player.endPoint };
                RemotePlayers[player.endPoint] = state;
            }
            
            state.PlayerName = player.playerName;
            state.IsInGame = player.isInGame;
            state.SceneId = player.sceneId;
            state.Latency = player.latency;
        }
    }
    
    private void HandlePlayerTransforms(string json)
    {
        var msg = JsonConvert.DeserializeObject<PlayerTransformSnapshotMsg>(json);
        if (msg?.transforms == null) return;
        
        foreach (var t in msg.transforms)
        {
            var key = t.peerId.ToString();
            if (key == NetworkId) continue;
            
            if (RemotePlayers.TryGetValue(key, out var state))
            {
                state.Position = new Vector3(t.position.x, t.position.y, t.position.z);
                state.Rotation = new Vector3(t.rotation.x, t.rotation.y, t.rotation.z);
                state.Velocity = new Vector3(t.velocity.x, t.velocity.y, t.velocity.z);
                state.LastUpdate = Time.time;
            }
        }
    }
    
    private void HandlePlayerAnimations(string json)
    {
        var msg = JsonConvert.DeserializeObject<PlayerAnimSnapshotMsg>(json);
        if (msg?.anims == null) return;
        
        foreach (var a in msg.anims)
        {
            var key = a.peerId.ToString();
            if (RemotePlayers.TryGetValue(key, out var state))
            {
                state.Speed = a.speed;
                state.DirX = a.dirX;
                state.DirY = a.dirY;
                state.HandState = a.hand;
                state.GunReady = a.gunReady;
                state.Dashing = a.dashing;
                state.Reloading = a.reloading;
            }
        }
    }
    
    private void HandlePlayerEquipment(string json)
    {
        var msg = JsonConvert.DeserializeObject<PlayerEquipmentSnapshotMsg>(json);
        if (msg?.equipment == null) return;
        
        foreach (var e in msg.equipment)
        {
            var key = e.peerId.ToString();
            if (RemotePlayers.TryGetValue(key, out var state))
            {
                state.WeaponId = e.weaponId;
                state.ArmorId = e.armorId;
                state.HelmetId = e.helmetId;
                state.Hotbar = e.hotbar?.ToList() ?? new List<int>();
            }
        }
    }
    
    private void HandlePlayerHealth(string json)
    {
        var msg = JsonConvert.DeserializeObject<PlayerHealthMsg>(json);
        if (msg == null) return;
        
        var key = msg.peerId.ToString();
        if (RemotePlayers.TryGetValue(key, out var state))
        {
            state.CurrentHealth = msg.currentHealth;
            state.MaxHealth = msg.maxHealth;
        }
    }
    
    private void HandlePlayerDeath(string json)
    {
        var msg = JsonConvert.DeserializeObject<PlayerDeathMsg>(json);
        if (msg == null) return;
        
        var key = msg.peerId.ToString();
        if (RemotePlayers.TryGetValue(key, out var state))
        {
            state.IsDead = true;
            state.CurrentHealth = 0;
        }
        
        ClientEvents.OnPlayerDeath?.Invoke(msg.peerId, msg.killerId, msg.cause);
    }
    
    private void HandlePlayerRespawn(string json)
    {
        var msg = JsonConvert.DeserializeObject<PlayerRespawnMsg>(json);
        if (msg == null) return;
        
        var key = msg.peerId.ToString();
        if (RemotePlayers.TryGetValue(key, out var state))
        {
            state.IsDead = false;
            state.Position = new Vector3(msg.position.x, msg.position.y, msg.position.z);
        }
        
        ClientEvents.OnPlayerRespawn?.Invoke(msg.peerId, new Vector3(msg.position.x, msg.position.y, msg.position.z));
    }
    
    private void HandlePlayerDisconnect(string json)
    {
        var msg = JsonConvert.DeserializeObject<PlayerDisconnectMsg>(json);
        if (msg == null) return;
        
        RemotePlayers.Remove(msg.peerId.ToString());
        ClientEvents.OnPlayerDisconnect?.Invoke(msg.peerId);
    }
    
    private void HandleAITransforms(string json)
    {
        var msg = JsonConvert.DeserializeObject<AITransformSnapshotMsg>(json);
        if (msg?.transforms == null) return;
        
        foreach (var t in msg.transforms)
        {
            if (!RemoteAI.TryGetValue(t.aiId, out var state))
            {
                state = new RemoteAIState { AIId = t.aiId };
                RemoteAI[t.aiId] = state;
            }
            
            state.Position = new Vector3(t.position.x, t.position.y, t.position.z);
            state.Forward = new Vector3(t.forward.x, t.forward.y, t.forward.z);
            state.LastUpdate = Time.time;
        }
    }
    
    private void HandleAIAnimations(string json)
    {
        var msg = JsonConvert.DeserializeObject<AIAnimSnapshotMsg>(json);
        if (msg?.anims == null) return;
        
        foreach (var a in msg.anims)
        {
            if (RemoteAI.TryGetValue(a.aiId, out var state))
            {
                state.Speed = a.speed;
                state.DirX = a.dirX;
                state.DirY = a.dirY;
                state.HandState = a.hand;
                state.GunReady = a.gunReady;
                state.Dashing = a.dashing;
            }
        }
    }
    
    private void HandleAIHealth(string json)
    {
        var msg = JsonConvert.DeserializeObject<AIHealthMsg>(json);
        if (msg == null) return;
        
        if (RemoteAI.TryGetValue(msg.aiId, out var state))
        {
            state.CurrentHealth = msg.currentHealth;
            state.MaxHealth = msg.maxHealth;
            state.IsDead = msg.currentHealth <= 0;
        }
        
        ClientEvents.OnAIHealthChanged?.Invoke(msg.aiId, msg.currentHealth, msg.maxHealth);
    }
    
    private void HandleWeaponFire(string json)
    {
        var msg = JsonConvert.DeserializeObject<WeaponFireMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnRemoteWeaponFire?.Invoke(
            msg.shooterId,
            msg.weaponId,
            new Vector3(msg.origin.x, msg.origin.y, msg.origin.z),
            new Vector3(msg.direction.x, msg.direction.y, msg.direction.z)
        );
    }
    
    private void HandleDamage(string json)
    {
        var msg = JsonConvert.DeserializeObject<DamageMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnDamageReceived?.Invoke(
            msg.targetId,
            msg.attackerId,
            msg.damage,
            msg.damageType,
            new Vector3(msg.hitPoint.x, msg.hitPoint.y, msg.hitPoint.z)
        );
    }
    
    private void HandleGrenadeThrow(string json)
    {
        var msg = JsonConvert.DeserializeObject<GrenadeThrowMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnGrenadeThrow?.Invoke(
            msg.throwerId,
            msg.grenadeType,
            new Vector3(msg.origin.x, msg.origin.y, msg.origin.z),
            new Vector3(msg.velocity.x, msg.velocity.y, msg.velocity.z)
        );
    }
    
    private void HandleGrenadeExplode(string json)
    {
        var msg = JsonConvert.DeserializeObject<GrenadeExplodeMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnGrenadeExplode?.Invoke(
            msg.grenadeId,
            new Vector3(msg.position.x, msg.position.y, msg.position.z),
            msg.radius,
            msg.damage
        );
    }
    
    private void HandleItemPickup(string json)
    {
        var msg = JsonConvert.DeserializeObject<ItemPickupMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnItemPickup?.Invoke(msg.playerId, msg.containerId, msg.slotIndex, msg.itemTypeId, msg.count);
    }
    
    private void HandleItemDrop(string json)
    {
        var msg = JsonConvert.DeserializeObject<ItemDropMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnItemDrop?.Invoke(
            msg.dropId,
            msg.playerId,
            msg.itemTypeId,
            msg.count,
            new Vector3(msg.position.x, msg.position.y, msg.position.z)
        );
    }
    
    private void HandleLootFullSync(string json)
    {
        ClientEvents.OnLootFullSync?.Invoke(json);
    }
    
    private void HandleContainerContents(string json)
    {
        ClientEvents.OnContainerContents?.Invoke(json);
    }
    
    private void HandleSceneLoad(string json)
    {
        var msg = JsonConvert.DeserializeObject<SceneLoadMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnSceneLoad?.Invoke(msg.sceneId, msg.timeOfDay, msg.weather);
    }
    
    private void HandleForceSceneLoad(string json)
    {
        var msg = JsonConvert.DeserializeObject<ForceSceneLoadMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnForceSceneLoad?.Invoke(msg.sceneId);
    }
    
    private void HandleWorldState(string json)
    {
        var msg = JsonConvert.DeserializeObject<WorldStateMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnWorldState?.Invoke(msg.sceneId, msg.timeOfDay, msg.weather, msg.weatherIntensity);
    }
    
    private void HandleTimeSync(string json)
    {
        var msg = JsonConvert.DeserializeObject<TimeSyncMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnTimeSync?.Invoke(msg.timeOfDay, msg.serverTime);
    }
    
    private void HandleWeatherSync(string json)
    {
        var msg = JsonConvert.DeserializeObject<WeatherSyncMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnWeatherSync?.Invoke(msg.weather, msg.intensity);
    }
    
    private void HandleDoorInteract(string json)
    {
        var msg = JsonConvert.DeserializeObject<DoorInteractMsg>(json);
        if (msg == null) return;
        
        ClientEvents.OnDoorInteract?.Invoke(msg.doorId, msg.isOpen, msg.playerId);
    }
    
    private void HandleKick(string json)
    {
        var msg = JsonConvert.DeserializeObject<KickMsg>(json);
        if (msg == null) return;
        
        Debug.LogWarning($"[DuckovClient] Kicked: {msg.reason}");
        ConnectionStatus = $"Kicked: {msg.reason}";
        Disconnect();
        
        ClientEvents.OnKicked?.Invoke(msg.reason);
    }
    
    public void SendPlayerSync()
    {
        if (!IsConnected) return;
        
        Writer.Reset();
        Writer.Put((byte)2);
        Writer.Put(LocalPlayer.Position.x);
        Writer.Put(LocalPlayer.Position.y);
        Writer.Put(LocalPlayer.Position.z);
        Writer.Put(LocalPlayer.Rotation.x);
        Writer.Put(LocalPlayer.Rotation.y);
        Writer.Put(LocalPlayer.Rotation.z);
        Writer.Put(LocalPlayer.Velocity.x);
        Writer.Put(LocalPlayer.Velocity.y);
        Writer.Put(LocalPlayer.Velocity.z);
        
        ServerPeer.Send(Writer, DeliveryMethod.Sequenced);
        
        Writer.Reset();
        Writer.Put((byte)3);
        Writer.Put(LocalPlayer.Speed);
        Writer.Put(LocalPlayer.DirX);
        Writer.Put(LocalPlayer.DirY);
        Writer.Put(LocalPlayer.HandState);
        Writer.Put(LocalPlayer.GunReady);
        Writer.Put(LocalPlayer.Dashing);
        Writer.Put(LocalPlayer.Reloading);
        
        ServerPeer.Send(Writer, DeliveryMethod.Sequenced);
    }
    
    public void SendClientStatus()
    {
        if (!IsConnected) return;
        
        var status = new
        {
            type = "clientStatus",
            playerName = LocalPlayer.PlayerName,
            isInGame = LocalPlayer.IsInGame,
            sceneId = LocalPlayer.SceneId
        };
        
        SendJson(status);
    }
    
    public void RequestFullSync()
    {
        if (!IsConnected) return;
        
        SendJson(new { type = "requestFullSync" });
    }
    
    public void SendWeaponFire(int weaponId, Vector3 origin, Vector3 direction, int ammoType)
    {
        if (!IsConnected) return;
        
        Writer.Reset();
        Writer.Put((byte)30);
        Writer.Put(weaponId);
        Writer.Put(origin.x);
        Writer.Put(origin.y);
        Writer.Put(origin.z);
        Writer.Put(direction.x);
        Writer.Put(direction.y);
        Writer.Put(direction.z);
        Writer.Put(ammoType);
        
        ServerPeer.Send(Writer, DeliveryMethod.ReliableOrdered);
    }
    
    public void SendDamage(int targetType, int targetId, float damage, string damageType, Vector3 hitPoint)
    {
        if (!IsConnected) return;
        
        Writer.Reset();
        Writer.Put((byte)40);
        Writer.Put(targetType);
        Writer.Put(targetId);
        Writer.Put(damage);
        Writer.Put(damageType);
        Writer.Put(hitPoint.x);
        Writer.Put(hitPoint.y);
        Writer.Put(hitPoint.z);
        
        ServerPeer.Send(Writer, DeliveryMethod.ReliableOrdered);
    }
    
    public void SendItemPickup(int containerId, int slotIndex, int itemTypeId, int count)
    {
        if (!IsConnected) return;
        
        Writer.Reset();
        Writer.Put((byte)21);
        Writer.Put(containerId);
        Writer.Put(slotIndex);
        Writer.Put(itemTypeId);
        Writer.Put(count);
        
        ServerPeer.Send(Writer, DeliveryMethod.ReliableOrdered);
    }
    
    public void SendItemDrop(int itemTypeId, int count, Vector3 position)
    {
        if (!IsConnected) return;
        
        Writer.Reset();
        Writer.Put((byte)22);
        Writer.Put(itemTypeId);
        Writer.Put(count);
        Writer.Put(position.x);
        Writer.Put(position.y);
        Writer.Put(position.z);
        
        ServerPeer.Send(Writer, DeliveryMethod.ReliableOrdered);
    }
    
    public void SendDoorInteract(int doorId, bool isOpen)
    {
        if (!IsConnected) return;
        
        Writer.Reset();
        Writer.Put((byte)50);
        Writer.Put(doorId);
        Writer.Put(isOpen);
        
        ServerPeer.Send(Writer, DeliveryMethod.ReliableOrdered);
    }
    
    public void SendEquipmentUpdate()
    {
        if (!IsConnected) return;
        
        Writer.Reset();
        Writer.Put((byte)6);
        Writer.Put(LocalPlayer.WeaponId);
        Writer.Put(LocalPlayer.ArmorId);
        Writer.Put(LocalPlayer.HelmetId);
        Writer.Put(LocalPlayer.Hotbar.Count);
        foreach (var item in LocalPlayer.Hotbar)
        {
            Writer.Put(item);
        }
        
        ServerPeer.Send(Writer, DeliveryMethod.ReliableOrdered);
    }
    
    public void SendJson(object data)
    {
        if (!IsConnected) return;
        
        var json = JsonConvert.SerializeObject(data);
        Writer.Reset();
        Writer.Put((byte)9);
        Writer.Put(json);
        
        ServerPeer.Send(Writer, DeliveryMethod.ReliableOrdered);
    }
}

public class LocalPlayerState
{
    public string PlayerName { get; set; } = "Player";
    public bool IsInGame { get; set; }
    public string SceneId { get; set; } = "";
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public Vector3 Velocity { get; set; }
    public float Speed { get; set; }
    public float DirX { get; set; }
    public float DirY { get; set; }
    public int HandState { get; set; }
    public bool GunReady { get; set; }
    public bool Dashing { get; set; }
    public bool Reloading { get; set; }
    public float CurrentHealth { get; set; } = 100f;
    public float MaxHealth { get; set; } = 100f;
    public int WeaponId { get; set; }
    public int ArmorId { get; set; }
    public int HelmetId { get; set; }
    public List<int> Hotbar { get; set; } = new();
    public int Latency { get; set; }
}

public class RemotePlayerState
{
    public string NetworkId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public bool IsInGame { get; set; }
    public string SceneId { get; set; } = "";
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public Vector3 Velocity { get; set; }
    public float Speed { get; set; }
    public float DirX { get; set; }
    public float DirY { get; set; }
    public int HandState { get; set; }
    public bool GunReady { get; set; }
    public bool Dashing { get; set; }
    public bool Reloading { get; set; }
    public float CurrentHealth { get; set; } = 100f;
    public float MaxHealth { get; set; } = 100f;
    public bool IsDead { get; set; }
    public int WeaponId { get; set; }
    public int ArmorId { get; set; }
    public int HelmetId { get; set; }
    public List<int> Hotbar { get; set; } = new();
    public int Latency { get; set; }
    public float LastUpdate { get; set; }
    public GameObject CharacterObject { get; set; }
}

public class RemoteAIState
{
    public int AIId { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Forward { get; set; }
    public float Speed { get; set; }
    public float DirX { get; set; }
    public float DirY { get; set; }
    public int HandState { get; set; }
    public bool GunReady { get; set; }
    public bool Dashing { get; set; }
    public float CurrentHealth { get; set; } = 100f;
    public float MaxHealth { get; set; } = 100f;
    public bool IsDead { get; set; }
    public float LastUpdate { get; set; }
    public GameObject AIObject { get; set; }
}

public static class ClientEvents
{
    public static Action<int, int, string> OnPlayerDeath;
    public static Action<int, Vector3> OnPlayerRespawn;
    public static Action<int> OnPlayerDisconnect;
    public static Action<int, float, float> OnAIHealthChanged;
    public static Action<int, int, Vector3, Vector3> OnRemoteWeaponFire;
    public static Action<int, int, float, string, Vector3> OnDamageReceived;
    public static Action<int, int, Vector3, Vector3> OnGrenadeThrow;
    public static Action<int, Vector3, float, float> OnGrenadeExplode;
    public static Action<int, int, int, int, int> OnItemPickup;
    public static Action<int, int, int, int, Vector3> OnItemDrop;
    public static Action<string> OnLootFullSync;
    public static Action<string> OnContainerContents;
    public static Action<string, float, int> OnSceneLoad;
    public static Action<string> OnForceSceneLoad;
    public static Action<string, float, int, float> OnWorldState;
    public static Action<float, long> OnTimeSync;
    public static Action<int, float> OnWeatherSync;
    public static Action<int, bool, int> OnDoorInteract;
    public static Action<string> OnKicked;
}

public class ClientMessageRouter
{
    public static ClientMessageRouter Instance { get; set; }
    
    private readonly Dictionary<string, Action<string>> _handlers = new();
    
    public void RegisterHandler(string messageType, Action<string> handler)
    {
        _handlers[messageType] = handler;
    }
    
    public void RouteMessage(string messageType, string json)
    {
        if (_handlers.TryGetValue(messageType, out var handler))
        {
            handler(json);
        }
    }
}

public class BaseMessage { public string type { get; set; } }
public class SetIdMsg { public string networkId { get; set; } }
public class PlayerListMsg { public List<PlayerListItem> players { get; set; } }
public class PlayerListItem { public int peerId { get; set; } public string endPoint { get; set; } public string playerName { get; set; } public bool isInGame { get; set; } public string sceneId { get; set; } public int latency { get; set; } }
public class Vec3Msg { public float x { get; set; } public float y { get; set; } public float z { get; set; } }
public class PlayerTransformSnapshotMsg { public List<PlayerTransformEntryMsg> transforms { get; set; } }
public class PlayerTransformEntryMsg { public int peerId { get; set; } public Vec3Msg position { get; set; } public Vec3Msg rotation { get; set; } public Vec3Msg velocity { get; set; } }
public class PlayerAnimSnapshotMsg { public List<PlayerAnimEntryMsg> anims { get; set; } }
public class PlayerAnimEntryMsg { public int peerId { get; set; } public float speed { get; set; } public float dirX { get; set; } public float dirY { get; set; } public int hand { get; set; } public bool gunReady { get; set; } public bool dashing { get; set; } public bool reloading { get; set; } }
public class PlayerEquipmentSnapshotMsg { public List<PlayerEquipmentEntryMsg> equipment { get; set; } }
public class PlayerEquipmentEntryMsg { public int peerId { get; set; } public int weaponId { get; set; } public int armorId { get; set; } public int helmetId { get; set; } public int[] hotbar { get; set; } }
public class PlayerHealthMsg { public int peerId { get; set; } public float currentHealth { get; set; } public float maxHealth { get; set; } }
public class PlayerDeathMsg { public int peerId { get; set; } public int killerId { get; set; } public string cause { get; set; } }
public class PlayerRespawnMsg { public int peerId { get; set; } public Vec3Msg position { get; set; } }
public class PlayerDisconnectMsg { public int peerId { get; set; } }
public class AITransformSnapshotMsg { public List<AITransformEntryMsg> transforms { get; set; } }
public class AITransformEntryMsg { public int aiId { get; set; } public Vec3Msg position { get; set; } public Vec3Msg forward { get; set; } }
public class AIAnimSnapshotMsg { public List<AIAnimEntryMsg> anims { get; set; } }
public class AIAnimEntryMsg { public int aiId { get; set; } public float speed { get; set; } public float dirX { get; set; } public float dirY { get; set; } public int hand { get; set; } public bool gunReady { get; set; } public bool dashing { get; set; } }
public class AIHealthMsg { public int aiId { get; set; } public float maxHealth { get; set; } public float currentHealth { get; set; } }
public class WeaponFireMsg { public int shooterId { get; set; } public int weaponId { get; set; } public Vec3Msg origin { get; set; } public Vec3Msg direction { get; set; } }
public class DamageMsg { public int targetId { get; set; } public int attackerId { get; set; } public float damage { get; set; } public string damageType { get; set; } public Vec3Msg hitPoint { get; set; } }
public class GrenadeThrowMsg { public int throwerId { get; set; } public int grenadeType { get; set; } public Vec3Msg origin { get; set; } public Vec3Msg velocity { get; set; } }
public class GrenadeExplodeMsg { public int grenadeId { get; set; } public Vec3Msg position { get; set; } public float radius { get; set; } public float damage { get; set; } }
public class ItemPickupMsg { public int playerId { get; set; } public int containerId { get; set; } public int slotIndex { get; set; } public int itemTypeId { get; set; } public int count { get; set; } }
public class ItemDropMsg { public int dropId { get; set; } public int playerId { get; set; } public int itemTypeId { get; set; } public int count { get; set; } public Vec3Msg position { get; set; } }
public class SceneLoadMsg { public string sceneId { get; set; } public float timeOfDay { get; set; } public int weather { get; set; } }
public class ForceSceneLoadMsg { public string sceneId { get; set; } }
public class WorldStateMsg { public string sceneId { get; set; } public float timeOfDay { get; set; } public int weather { get; set; } public float weatherIntensity { get; set; } }
public class TimeSyncMsg { public float timeOfDay { get; set; } public long serverTime { get; set; } }
public class WeatherSyncMsg { public int weather { get; set; } public float intensity { get; set; } }
public class DoorInteractMsg { public int doorId { get; set; } public bool isOpen { get; set; } public int playerId { get; set; } }
public class KickMsg { public string reason { get; set; } }
