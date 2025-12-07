using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;
using Newtonsoft.Json;

namespace EscapeFromDuckovCoopMod.Net;

public class CoopNetClient : MonoBehaviour, INetEventListener
{
    public static CoopNetClient Instance { get; private set; }
    
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
            Debug.Log($"[CoopNet] Connecting to {address}:{port}");
        }
        catch (Exception ex)
        {
            IsConnecting = false;
            ConnectionStatus = $"Connection error: {ex.Message}";
            OnConnectionFailed?.Invoke(ConnectionStatus);
            Debug.LogError($"[CoopNet] Connection error: {ex}");
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
        
        Debug.Log($"[CoopNet] Connected to server: {peer.EndPoint}");
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
        
        Debug.Log($"[CoopNet] Disconnected: {reason}");
        OnDisconnected?.Invoke(reason);
        
        RemotePlayers.Clear();
        RemoteAI.Clear();
    }
    
    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        Debug.LogWarning($"[CoopNet] Network error: {socketError} from {endPoint}");
    }
    
    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        if (reader.AvailableBytes < 1) return;
        
        var msgType = reader.PeekByte();
        
        try
        {
            ProcessMessage(msgType, reader);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CoopNet] Error processing message type {msgType}: {ex.Message}");
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
        reader.GetByte();
        
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
            var baseMsg = JsonConvert.DeserializeObject<BaseJsonMessage>(json);
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
                    HandlePlayerTransformSnapshot(json);
                    break;
                case "player_anim_snapshot":
                    HandlePlayerAnimSnapshot(json);
                    break;
                case "playerDisconnect":
                    HandlePlayerDisconnect(json);
                    break;
                case "delta_sync":
                    HandleDeltaSync(json);
                    break;
                default:
                    OnMessageReceived?.Invoke(baseMsg.type, null);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CoopNet] JSON parse error: {ex.Message}");
        }
    }
    
    private void HandleSetId(string json)
    {
        var data = JsonConvert.DeserializeObject<SetIdData>(json);
        if (data != null)
        {
            NetworkId = data.networkId;
            Debug.Log($"[CoopNet] Assigned network ID: {NetworkId}");
        }
    }
    
    private void HandlePlayerList(string json)
    {
        var data = JsonConvert.DeserializeObject<PlayerListData>(json);
        if (data?.players == null) return;
        
        var currentIds = new HashSet<string>();
        foreach (var p in data.players)
        {
            var id = p.endPoint;
            currentIds.Add(id);
            
            if (id == NetworkId) continue;
            
            if (!RemotePlayers.TryGetValue(id, out var state))
            {
                state = new RemotePlayerState();
                RemotePlayers[id] = state;
            }
            
            state.NetworkId = id;
            state.PlayerName = p.playerName;
            state.IsInGame = p.isInGame;
            state.SceneId = p.sceneId;
            state.Latency = p.latency;
        }
        
        var toRemove = new List<string>();
        foreach (var id in RemotePlayers.Keys)
        {
            if (!currentIds.Contains(id))
                toRemove.Add(id);
        }
        foreach (var id in toRemove)
        {
            RemotePlayers.Remove(id);
        }
    }
    
    private void HandlePlayerTransformSnapshot(string json)
    {
        var data = JsonConvert.DeserializeObject<PlayerTransformSnapshot>(json);
        if (data?.transforms == null) return;
        
        foreach (var t in data.transforms)
        {
            var id = t.peerId.ToString();
            if (RemotePlayers.TryGetValue(id, out var state))
            {
                state.Position = new Vector3(t.position.x, t.position.y, t.position.z);
                state.Rotation = new Vector3(t.rotation.x, t.rotation.y, t.rotation.z);
                state.Velocity = new Vector3(t.velocity.x, t.velocity.y, t.velocity.z);
                state.LastUpdateTime = Time.time;
            }
        }
    }
    
    private void HandlePlayerAnimSnapshot(string json)
    {
        var data = JsonConvert.DeserializeObject<PlayerAnimSnapshot>(json);
        if (data?.anims == null) return;
        
        foreach (var a in data.anims)
        {
            var id = a.peerId.ToString();
            if (RemotePlayers.TryGetValue(id, out var state))
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
    
    private void HandlePlayerDisconnect(string json)
    {
        var data = JsonConvert.DeserializeObject<PlayerDisconnectData>(json);
        if (data != null)
        {
            RemotePlayers.Remove(data.peerId.ToString());
        }
    }
    
    private void HandleDeltaSync(string json)
    {
        var data = JsonConvert.DeserializeObject<DeltaSyncData>(json);
        if (data == null) return;
        
        if (data.players != null)
        {
            foreach (var p in data.players)
            {
                var id = p.peerId.ToString();
                if (!RemotePlayers.TryGetValue(id, out var state)) continue;
                
                if (p.hasPosition && p.position != null)
                    state.Position = new Vector3(p.position.x, p.position.y, p.position.z);
                if (p.hasRotation && p.rotation != null)
                    state.Rotation = new Vector3(p.rotation.x, p.rotation.y, p.rotation.z);
                if (p.hasVelocity && p.velocity != null)
                    state.Velocity = new Vector3(p.velocity.x, p.velocity.y, p.velocity.z);
                if (p.hasHealth)
                    state.Health = p.health;
                if (p.hasAnim)
                {
                    state.Speed = p.speed;
                    state.DirX = p.dirX;
                    state.DirY = p.dirY;
                    state.HandState = p.handState;
                    state.GunReady = p.gunReady;
                    state.Dashing = p.dashing;
                    state.Reloading = p.reloading;
                }
                state.LastUpdateTime = Time.time;
            }
        }
    }
    
    public void SendPlayerSync()
    {
        if (!IsConnected) return;
        
        var mainChar = GetLocalCharacter();
        if (mainChar == null) return;
        
        var pos = mainChar.transform.position;
        var rot = mainChar.transform.eulerAngles;
        
        Writer.Reset();
        Writer.Put((byte)2);
        Writer.Put(pos.x);
        Writer.Put(pos.y);
        Writer.Put(pos.z);
        Writer.Put(rot.x);
        Writer.Put(rot.y);
        Writer.Put(rot.z);
        
        ServerPeer.Send(Writer, DeliveryMethod.Unreliable);
        
        LocalPlayer.Position = pos;
        LocalPlayer.Rotation = rot;
    }
    
    public void SendClientStatus()
    {
        if (!IsConnected) return;
        
        var statusData = new ClientStatusData
        {
            type = "clientStatus",
            playerName = LocalPlayer.PlayerName,
            isInGame = LocalPlayer.IsInGame,
            sceneId = LocalPlayer.SceneId
        };
        
        var json = JsonConvert.SerializeObject(statusData);
        Writer.Reset();
        Writer.Put((byte)9);
        Writer.Put(json);
        
        ServerPeer.Send(Writer, DeliveryMethod.ReliableOrdered);
    }
    
    public void RequestFullSync()
    {
        if (!IsConnected) return;
        
        var request = new { type = "fullSyncRequest" };
        var json = JsonConvert.SerializeObject(request);
        
        Writer.Reset();
        Writer.Put((byte)9);
        Writer.Put(json);
        
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
    
    private GameObject GetLocalCharacter()
    {
        var mainControlType = Type.GetType("CharacterMainControl, Assembly-CSharp");
        if (mainControlType != null)
        {
            var mainProp = mainControlType.GetProperty("Main", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (mainProp != null)
            {
                var mainObj = mainProp.GetValue(null) as MonoBehaviour;
                if (mainObj != null)
                    return mainObj.gameObject;
            }
        }
        return null;
    }
}

public class LocalPlayerState
{
    public string PlayerName { get; set; } = "Player";
    public bool IsInGame { get; set; }
    public string SceneId { get; set; } = "";
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
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
    public float Health { get; set; } = 100f;
    public float Speed { get; set; }
    public float DirX { get; set; }
    public float DirY { get; set; }
    public int HandState { get; set; }
    public bool GunReady { get; set; }
    public bool Dashing { get; set; }
    public bool Reloading { get; set; }
    public int Latency { get; set; }
    public float LastUpdateTime { get; set; }
    public GameObject CharacterObject { get; set; }
}

public class RemoteAIState
{
    public int EntityId { get; set; }
    public string TypeName { get; set; } = "";
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public int State { get; set; }
    public float LastUpdateTime { get; set; }
    public GameObject AIObject { get; set; }
}

[Serializable]
public class BaseJsonMessage
{
    public string type { get; set; } = "";
}

[Serializable]
public class SetIdData
{
    public string type { get; set; } = "setId";
    public string networkId { get; set; } = "";
}

[Serializable]
public class PlayerListData
{
    public string type { get; set; } = "playerList";
    public List<PlayerListItem> players { get; set; } = new();
}

[Serializable]
public class PlayerListItem
{
    public int peerId { get; set; }
    public string endPoint { get; set; } = "";
    public string playerName { get; set; } = "";
    public bool isInGame { get; set; }
    public string sceneId { get; set; } = "";
    public int latency { get; set; }
}

[Serializable]
public class PlayerTransformSnapshot
{
    public string type { get; set; } = "player_transform_snapshot";
    public List<PlayerTransformEntry> transforms { get; set; } = new();
}

[Serializable]
public class PlayerTransformEntry
{
    public int peerId { get; set; }
    public Vec3 position { get; set; }
    public Vec3 rotation { get; set; }
    public Vec3 velocity { get; set; }
}

[Serializable]
public class Vec3
{
    public float x { get; set; }
    public float y { get; set; }
    public float z { get; set; }
}

[Serializable]
public class PlayerAnimSnapshot
{
    public string type { get; set; } = "player_anim_snapshot";
    public List<PlayerAnimEntry> anims { get; set; } = new();
}

[Serializable]
public class PlayerAnimEntry
{
    public int peerId { get; set; }
    public float speed { get; set; }
    public float dirX { get; set; }
    public float dirY { get; set; }
    public int hand { get; set; }
    public bool gunReady { get; set; }
    public bool dashing { get; set; }
    public bool reloading { get; set; }
}

[Serializable]
public class PlayerDisconnectData
{
    public string type { get; set; } = "playerDisconnect";
    public int peerId { get; set; }
}

[Serializable]
public class ClientStatusData
{
    public string type { get; set; } = "clientStatus";
    public string playerName { get; set; } = "";
    public bool isInGame { get; set; }
    public string sceneId { get; set; } = "";
}

[Serializable]
public class DeltaSyncData
{
    public string type { get; set; } = "delta_sync";
    public List<PlayerDeltaEntry> players { get; set; }
    public List<AIDeltaEntry> ai { get; set; }
}

[Serializable]
public class PlayerDeltaEntry
{
    public int peerId { get; set; }
    public bool hasPosition { get; set; }
    public Vec3 position { get; set; }
    public bool hasRotation { get; set; }
    public Vec3 rotation { get; set; }
    public bool hasVelocity { get; set; }
    public Vec3 velocity { get; set; }
    public bool hasHealth { get; set; }
    public float health { get; set; }
    public bool hasAnim { get; set; }
    public float speed { get; set; }
    public float dirX { get; set; }
    public float dirY { get; set; }
    public int handState { get; set; }
    public bool gunReady { get; set; }
    public bool dashing { get; set; }
    public bool reloading { get; set; }
}

[Serializable]
public class AIDeltaEntry
{
    public int entityId { get; set; }
    public bool hasPosition { get; set; }
    public Vec3 position { get; set; }
    public bool hasRotation { get; set; }
    public Vec3 rotation { get; set; }
    public bool hasHealth { get; set; }
    public float health { get; set; }
    public bool hasState { get; set; }
    public int state { get; set; }
}
