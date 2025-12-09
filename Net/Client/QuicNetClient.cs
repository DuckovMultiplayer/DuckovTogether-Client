using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckovNet;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net
{
    public class QuicNetClient : MonoBehaviour
    {
        public static QuicNetClient Instance { get; private set; }
        
        private QuicTransport _transport;
        private QuicPeer _serverPeer;
        
        public bool IsConnected => _serverPeer?.IsConnected ?? false;
        public bool IsConnecting { get; private set; }
        public string ConnectionStatus { get; private set; } = "";
        public string NetworkId { get; private set; } = "";
        public int Latency => _serverPeer?.Latency ?? -1;
        
        public string ServerAddress { get; set; } = "127.0.0.1";
        public int ServerPort { get; set; } = 9050;
        
        private float _reconnectTimer;
        private float _syncTimer;
        private bool _autoReconnect;
        private int _reconnectAttempts;
        private const int MAX_RECONNECT_ATTEMPTS = 5;
        private const float RECONNECT_INTERVAL = 5f;
        private const float SYNC_INTERVAL = 0.05f;
        
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnConnectionFailed;
        public event Action<byte[], DeliveryMode> OnMessageReceived;
        
        public readonly Dictionary<string, RemotePlayerState> RemotePlayers = new();
        public readonly Dictionary<int, RemoteAIState> RemoteAI = new();
        public LocalPlayerState LocalPlayer { get; private set; }
        
        private readonly Queue<Action> _mainThreadQueue = new();
        private readonly object _queueLock = new();
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            LocalPlayer = new LocalPlayerState();
            
            if (!QuicTransport.IsSupported)
            {
                Debug.LogWarning("[QuicNet] QUIC not supported on this platform, will use fallback");
            }
        }
        
        private void Update()
        {
            lock (_queueLock)
            {
                while (_mainThreadQueue.Count > 0)
                {
                    _mainThreadQueue.Dequeue()?.Invoke();
                }
            }
            
            if (IsConnected)
            {
                _syncTimer += Time.deltaTime;
                if (_syncTimer >= SYNC_INTERVAL)
                {
                    SendPlayerSync();
                    _syncTimer = 0f;
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
            
            _ = ConnectAsync(address, port);
        }
        
        private async Task ConnectAsync(string address, int port)
        {
            try
            {
                _transport?.Stop();
                _transport = new QuicTransport();
                
                _transport.OnPeerConnected += OnPeerConnectedHandler;
                _transport.OnPeerDisconnected += OnPeerDisconnectedHandler;
                _transport.OnDataReceived += OnDataReceivedHandler;
                
                Debug.Log($"[QuicNet] Connecting to {address}:{port}");
                _serverPeer = await _transport.ConnectAsync(address, port);
                
                if (_serverPeer == null)
                {
                    RunOnMainThread(() =>
                    {
                        IsConnecting = false;
                        ConnectionStatus = "Connection failed";
                        OnConnectionFailed?.Invoke(ConnectionStatus);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuicNet] Connection error: {ex}");
                RunOnMainThread(() =>
                {
                    IsConnecting = false;
                    ConnectionStatus = $"Connection error: {ex.Message}";
                    OnConnectionFailed?.Invoke(ConnectionStatus);
                });
            }
        }
        
        public void Disconnect()
        {
            _autoReconnect = false;
            _reconnectAttempts = 0;
            
            if (_serverPeer != null)
            {
                _transport?.Disconnect(_serverPeer, "client disconnect");
                _serverPeer = null;
            }
            
            _transport?.Stop();
            _transport = null;
            
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
        
        public void Send(byte[] data, DeliveryMode mode = DeliveryMode.Reliable)
        {
            if (_serverPeer != null && IsConnected)
            {
                _transport?.Send(_serverPeer, data, mode);
            }
        }
        
        public void SendUnreliable(byte[] data)
        {
            Send(data, DeliveryMode.Unreliable);
        }
        
        public void SendReliable(byte[] data)
        {
            Send(data, DeliveryMode.Reliable);
        }
        
        private void OnPeerConnectedHandler(QuicPeer peer)
        {
            RunOnMainThread(() =>
            {
                _serverPeer = peer;
                IsConnecting = false;
                _reconnectAttempts = 0;
                ConnectionStatus = $"Connected to {peer.EndPoint}";
                
                Debug.Log($"[QuicNet] Connected to server: {peer.EndPoint}");
                Debug.Log($"[QuicNet] Protocol: {QuicTransport.PROTOCOL_VERSION}");
                OnConnected?.Invoke();
            });
        }
        
        private void OnPeerDisconnectedHandler(QuicPeer peer, string reason)
        {
            RunOnMainThread(() =>
            {
                ConnectionStatus = $"Disconnected: {reason}";
                _serverPeer = null;
                IsConnecting = false;
                
                Debug.Log($"[QuicNet] Disconnected: {reason}");
                OnDisconnected?.Invoke(reason);
                
                RemotePlayers.Clear();
                RemoteAI.Clear();
            });
        }
        
        private void OnDataReceivedHandler(QuicPeer peer, byte[] data, DeliveryMode mode)
        {
            RunOnMainThread(() =>
            {
                OnMessageReceived?.Invoke(data, mode);
                ProcessMessage(data);
            });
        }
        
        private void ProcessMessage(byte[] data)
        {
            if (data == null || data.Length < 1) return;
        }
        
        private void SendPlayerSync()
        {
            if (!IsConnected || LocalPlayer == null) return;
        }
        
        private void RunOnMainThread(Action action)
        {
            lock (_queueLock)
            {
                _mainThreadQueue.Enqueue(action);
            }
        }
    }
}
