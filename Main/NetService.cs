















using Steamworks;
using System.Net;
using System.Net.Sockets;
using EscapeFromDuckovCoopMod.Net;

namespace EscapeFromDuckovCoopMod;

public enum NetworkTransportMode
{
    Direct,
    SteamP2P
}

public class NetService : MonoBehaviour, INetEventListener
{
    public static NetService Instance;
    public int port = 9050;
    public List<string> hostList = new();
    public bool isConnecting;
    public string status = "";
    public string manualIP = "192.168.123.1";
    public string manualPort = "9050"; 
    public bool networkStarted;
    public float broadcastTimer;
    public float broadcastInterval = 5f;
    public float syncTimer;
    public float syncInterval = 0.015f; 

    public readonly HashSet<int> _dedupeShotFrame = new(); 

    
    private float _isInGameSyncTimer = 0f;
    private const float IS_IN_GAME_SYNC_INTERVAL = 1.0f; 

    
    
    public string cachedConnectedIP = "";
    public int cachedConnectedPort = 0;
    public bool hasSuccessfulConnection = false;

    
    private float lastReconnectTime = 0f;
    private const float RECONNECT_COOLDOWN = 10f; 

    
    private bool isManualConnection = false; 

    public string _relayRoomId = "";

    
    public readonly Dictionary<string, PlayerStatus> clientPlayerStatuses = new();
    public readonly Dictionary<string, GameObject> clientRemoteCharacters = new();

    
    public readonly Dictionary<NetPeer, PlayerStatus> playerStatuses = new();
    public readonly Dictionary<NetPeer, GameObject> remoteCharacters = new();
    public NetPeer connectedPeer;
    public HashSet<string> hostSet = new();

    
    private readonly Dictionary<NetPeer, float> _peerConnectionTime = new();
    private const float JOIN_TIMEOUT_SECONDS = 10f;

    
    public PlayerStatus localPlayerStatus;

    public NetManager netManager;
    public NetDataWriter writer;
    public bool IsServer { get; private set; }
    public NetworkTransportMode TransportMode { get; private set; } = NetworkTransportMode.Direct;
    public SteamLobbyOptions LobbyOptions { get; private set; } = SteamLobbyOptions.CreateDefault();

    public void OnEnable()
    {
        Instance = this;
        if (SteamP2PLoader.Instance != null)
        {
            SteamP2PLoader.Instance.UseSteamP2P = TransportMode == NetworkTransportMode.SteamP2P;
        }
    }

    public void Update()
    {
        
        _isInGameSyncTimer += Time.deltaTime;
        if (_isInGameSyncTimer >= IS_IN_GAME_SYNC_INTERVAL)
        {
            _isInGameSyncTimer = 0f;
            SyncIsInGameStatusToDatabase();
        }
    }

    public void SetTransportMode(NetworkTransportMode mode)
    {
        if (TransportMode == mode)
            return;

        TransportMode = mode;

        if (SteamP2PLoader.Instance != null)
        {
            SteamP2PLoader.Instance.UseSteamP2P = mode == NetworkTransportMode.SteamP2P;
        }

        if (mode != NetworkTransportMode.SteamP2P && SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.LeaveLobby();
        }

        if (networkStarted)
        {
            StopNetwork();
        }
    }

    public void ConfigureLobbyOptions(SteamLobbyOptions? options)
    {
        LobbyOptions = options ?? SteamLobbyOptions.CreateDefault();

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.UpdateLobbySettings(LobbyOptions);
        }
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Debug.Log(CoopLocalization.Get("net.connectionSuccess", peer.EndPoint.ToString()));
        connectedPeer = peer;

        if (!IsServer)
        {
            status = CoopLocalization.Get("net.connectedTo", peer.EndPoint.ToString());
            isConnecting = false;
            Send_ClientStatus.Instance.SendClientStatusUpdate();

            
            Net.ClientStatusMessage.Client_SendStatusUpdate();

            

            
            if (isManualConnection && peer.EndPoint is IPEndPoint ipEndPoint)
            {
                cachedConnectedIP = ipEndPoint.Address.ToString();
                cachedConnectedPort = ipEndPoint.Port;
                hasSuccessfulConnection = true;
                isManualConnection = false; 
                Debug.Log($"[AUTO_RECONNECT] 缓存连接信息 - IP: {cachedConnectedIP}, Port: {cachedConnectedPort}");
            }

            
            UpdateLocalPlayerToDatabase();
        }
        else
        {
            
            SetIdMessage.SendSetIdToPeer(peer);

            
            _peerConnectionTime[peer] = Time.time;
            Debug.Log($"[JOIN_TIMEOUT] 玩家 {peer.EndPoint} 开始加入，超时时限: {JOIN_TIMEOUT_SECONDS}秒");

            
            UpdateLocalPlayerToDatabase();
        }

        if (!playerStatuses.ContainsKey(peer))
            playerStatuses[peer] = new PlayerStatus
            {
                EndPoint = peer.EndPoint.ToString(),
                PlayerName = IsServer ? $"Player_{peer.Id}" : "Host",
                Latency = peer.Ping,
                IsInGame = false,
                LastIsInGame = false,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                CustomFaceJson = null
            };

        if (IsServer) SendLocalPlayerStatus.Instance.SendPlayerStatusUpdate();

        if (IsServer)
        {
            
            var hostMain = CharacterMainControl.Main;
            var hostH = hostMain ? hostMain.GetComponentInChildren<Health>(true) : null;
            if (hostH)
            {
                var msg = new Net.HybridNet.PlayerHealthAuthRemoteMessage
                {
                    PlayerId = GetPlayerId(null),
                    MaxHealth = hostH.MaxHealth,
                    CurrentHealth = hostH.CurrentHealth
                };
                Net.HybridNet.HybridNetCore.Send(msg, peer);
            }

            if (remoteCharacters != null)
                foreach (var kv in remoteCharacters)
                {
                    var owner = kv.Key;
                    var go = kv.Value;

                    if (owner == null || go == null) continue;

                    var h = go.GetComponentInChildren<Health>(true);
                    if (!h) continue;

                    var msg = new Net.HybridNet.PlayerHealthAuthRemoteMessage
                    {
                        PlayerId = GetPlayerId(owner),
                        MaxHealth = h.MaxHealth,
                        CurrentHealth = h.CurrentHealth
                    };
                    Net.HybridNet.HybridNetCore.Send(msg, peer);
                }
        }

        if (IsServer)
        {
            Net.ClientStatusMessage.SendPlayerInfoUpdateToClients();
        }
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Debug.Log(CoopLocalization.Get("net.disconnected", peer.EndPoint.ToString(), disconnectInfo.Reason.ToString()));
        if (!IsServer)
        {
            status = CoopLocalization.Get("net.connectionLost");
            isConnecting = false;
        }

        if (connectedPeer == peer) connectedPeer = null;

        
        if (IsServer && _peerConnectionTime.ContainsKey(peer))
        {
            _peerConnectionTime.Remove(peer);
        }

        if (IsServer && AIRequest.Instance != null)
        {
            AIRequest.Instance.ClearSentSeedsForPeer(peer);
        }

        
        if (playerStatuses.ContainsKey(peer))
        {
            var _st = playerStatuses[peer];
            if (_st != null && !string.IsNullOrEmpty(_st.EndPoint))
            {
                UpdatePlayerLastSeenInDatabase(_st.EndPoint);
                SceneNet.Instance._cliLastSceneIdByPlayer.Remove(_st.EndPoint);
            }
            playerStatuses.Remove(peer);
        }

        if (remoteCharacters.ContainsKey(peer) && remoteCharacters[peer] != null)
        {
            Destroy(remoteCharacters[peer]);
            remoteCharacters.Remove(peer);
        }

        if (!SteamP2PLoader.Instance.UseSteamP2P || SteamP2PManager.Instance == null)
            return;
        try
        {
            Debug.Log($"[Patch_OnPeerDisconnected] LiteNetLib断开: {peer.EndPoint}, 原因: {disconnectInfo.Reason}");
            if (SteamEndPointMapper.Instance != null &&
                SteamEndPointMapper.Instance.TryGetSteamID(peer.EndPoint, out CSteamID remoteSteamID))
            {
                Debug.Log($"[Patch_OnPeerDisconnected] 关闭Steam P2P会话: {remoteSteamID}");
                if (SteamNetworking.CloseP2PSessionWithUser(remoteSteamID))
                {
                    Debug.Log($"[Patch_OnPeerDisconnected] ✓ 成功关闭P2P会话");
                }
                SteamEndPointMapper.Instance.UnregisterSteamID(remoteSteamID);
                Debug.Log($"[Patch_OnPeerDisconnected] ✓ 已清理映射");
                if (SteamP2PManager.Instance != null)
                {
                    SteamP2PManager.Instance.ClearAcceptedSession(remoteSteamID);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Patch_OnPeerDisconnected] 异常: {ex}");
        }



    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        Debug.LogError(CoopLocalization.Get("net.networkError", socketError, endPoint.ToString()));
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        ModBehaviourF.Instance.OnNetworkReceive(peer, reader, channelNumber, deliveryMethod);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        var msg = reader.GetString();

        if (IsServer && msg == "DISCOVER_REQUEST")
        {
            writer.Reset();
            writer.Put("DISCOVER_RESPONSE");
            netManager.SendUnconnectedMessage(writer, remoteEndPoint);
        }
        else if (!IsServer && msg == "DISCOVER_RESPONSE")
        {
            var hostInfo = remoteEndPoint.Address + ":" + port;
            if (!hostSet.Contains(hostInfo))
            {
                hostSet.Add(hostInfo);
                hostList.Add(hostInfo);
                Debug.Log(CoopLocalization.Get("net.hostDiscovered", hostInfo));
            }
        }
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        if (playerStatuses.ContainsKey(peer))
        {
            playerStatuses[peer].Latency = latency;

            
            SyncLatencyToDatabase(peer, latency);
        }
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        if (IsServer)
        {
            if (request.Data != null && request.Data.GetString() == "gameKey") request.Accept();
            else request.Reject();
        }
        else
        {
            request.Reject();
        }
    }

    public void StartNetwork(bool isServer, bool keepSteamLobby = false)
    {
        StopNetwork(!keepSteamLobby);
        COOPManager.AIHandle.freezeAI = !isServer;
        IsServer = isServer;
        writer = new NetDataWriter();
        netManager = new NetManager(this)
        {
            BroadcastReceiveEnabled = true,
            
            
            
            
            
            ChannelsCount = 4
        };


        if (IsServer)
        {
            var started = netManager.Start(port);
            if (started)
            {
                Debug.Log(CoopLocalization.Get("net.serverStarted", port));
            }
            else
            {
                Debug.LogError(CoopLocalization.Get("net.serverStartFailed"));
            }
        }
        else
        {
            var started = netManager.Start();
            if (started)
            {
                Debug.Log(CoopLocalization.Get("net.clientStarted"));
                if (TransportMode == NetworkTransportMode.Direct)
                {
                    CoopTool.SendBroadcastDiscovery();
                }
            }
            else
            {
                Debug.LogError(CoopLocalization.Get("net.clientStartFailed"));
            }
        }

        networkStarted = true;
        status = CoopLocalization.Get("net.networkStarted");
        hostList.Clear();
        hostSet.Clear();
        isConnecting = false;
        connectedPeer = null;

        playerStatuses.Clear();
        remoteCharacters.Clear();
        clientPlayerStatuses.Clear();
        clientRemoteCharacters.Clear();

        LocalPlayerManager.Instance.InitializeLocalPlayer();
        if (IsServer)
        {
            ItemAgent_Gun.OnMainCharacterShootEvent -= COOPManager.WeaponHandle.Host_OnMainCharacterShoot;
            ItemAgent_Gun.OnMainCharacterShootEvent += COOPManager.WeaponHandle.Host_OnMainCharacterShoot;

            
            UpdateLocalPlayerToDatabase();
        }


        
        bool wantsP2P = TransportMode == NetworkTransportMode.SteamP2P;
        bool p2pAvailable =
            wantsP2P &&
            SteamP2PLoader.Instance != null &&
            SteamManager.Initialized &&
            SteamP2PManager.Instance != null &&   
            SteamP2PLoader.Instance.UseSteamP2P;

        Debug.Log($"[StartNetwork] WantsP2P={wantsP2P}, P2P可用={p2pAvailable}, UseSteamP2P={SteamP2PLoader.Instance?.UseSteamP2P}, " +
                  $"SteamInit={SteamManager.Initialized}, IsServer={IsServer}, NetRunning={netManager?.IsRunning}");

        if (p2pAvailable)
        {
            Debug.Log("[StartNetwork] 联机Mod已启动，初始化Steam P2P组件"); 

            if (netManager != null)
            {
                
                netManager.UseNativeSockets = false;
                Debug.Log("[StartNetwork] ✓ UseNativeSockets=false（P2P 模式）");
            }

            
            if (SteamEndPointMapper.Instance == null)
                DontDestroyOnLoad(new GameObject("SteamEndPointMapper").AddComponent<SteamEndPointMapper>());
            if (SteamLobbyManager.Instance == null)
                DontDestroyOnLoad(new GameObject("SteamLobbyManager").AddComponent<SteamLobbyManager>());

            
            if (!keepSteamLobby && IsServer && SteamLobbyManager.Instance != null && !SteamLobbyManager.Instance.IsInLobby)
            {
                SteamLobbyManager.Instance.CreateLobby(LobbyOptions);
            }
        }
        else
        {
            
            if (netManager != null)
            {
                netManager.UseNativeSockets = true;
                if (wantsP2P)
                {
                    Debug.LogWarning("[StartNetwork] Steam P2P 不可用，回退 UDP（UseNativeSockets=true）");
                }
                else
                {
                    Debug.Log("[StartNetwork] 使用直连模式（UseNativeSockets=true）");
                }
            }
        }



    }

    public void StopNetwork(bool leaveSteamLobby = true)
    {
        if (netManager != null && netManager.IsRunning)
        {
            netManager.Stop();
            Debug.Log(CoopLocalization.Get("net.networkStopped"));
        }

        IsServer = false;
        networkStarted = false;
        connectedPeer = null;

        if (leaveSteamLobby && TransportMode == NetworkTransportMode.SteamP2P && SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsInLobby)
        {
            SteamLobbyManager.Instance.LeaveLobby();
        }

        playerStatuses.Clear();
        clientPlayerStatuses.Clear();

        localPlayerStatus = null;

        foreach (var kvp in remoteCharacters)
            if (kvp.Value != null)
                Destroy(kvp.Value);
        remoteCharacters.Clear();

        foreach (var kvp in clientRemoteCharacters)
            if (kvp.Value != null)
                Destroy(kvp.Value);
        clientRemoteCharacters.Clear();

        
        Utils.Database.PlayerInfoDatabase.Instance.Clear();
        Debug.Log("[NetService] ✓ 已清空玩家数据库");

        ItemAgent_Gun.OnMainCharacterShootEvent -= COOPManager.WeaponHandle.Host_OnMainCharacterShoot;
    }

    public void ConnectToHost(string ip, int port)
    {
        
        isManualConnection = true;

        
        if (string.IsNullOrWhiteSpace(ip))
        {
            status = CoopLocalization.Get("net.ipEmpty");
            isConnecting = false;
            return;
        }

        if (port <= 0 || port > 65535)
        {
            status = CoopLocalization.Get("net.invalidPort");
            isConnecting = false;
            return;
        }

        if (IsServer)
        {
            Debug.LogWarning(CoopLocalization.Get("net.serverModeCannotConnect"));
            return;
        }

        if (isConnecting)
        {
            Debug.LogWarning(CoopLocalization.Get("net.alreadyConnecting"));
            return;
        }

        
        if (netManager == null || !netManager.IsRunning || IsServer || !networkStarted)
            try
            {
                StartNetwork(false); 
            }
            catch (Exception e)
            {
                Debug.LogError(CoopLocalization.Get("net.clientNetworkStartFailed", e));
                status = CoopLocalization.Get("net.clientNetworkStartFailedStatus");
                isConnecting = false;
                return;
            }

        
        if (netManager == null || !netManager.IsRunning)
        {
            status = CoopLocalization.Get("net.clientNotStarted");
            isConnecting = false;
            return;
        }

        try
        {
            status = CoopLocalization.Get("net.connectingTo", ip, port);
            isConnecting = true;

            
            try
            {
                connectedPeer?.Disconnect();
            }
            catch
            {
            }

            connectedPeer = null;

            if (writer == null) writer = new NetDataWriter();

            writer.Reset();
            writer.Put("gameKey");
            netManager.Connect(ip, port, writer);
        }
        catch (Exception ex)
        {
            Debug.LogError(CoopLocalization.Get("net.connectionFailedLog", ex));
            status = CoopLocalization.Get("net.connectionFailed");
            isConnecting = false;
            connectedPeer = null;
        }
    }


    public bool IsSelfId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        var mine = localPlayerStatus?.EndPoint;

        
        if (!string.IsNullOrEmpty(mine) && id == mine)
        {
            Debug.Log($"[IsSelfId] ✓ 匹配本地ID: {id}");
            return true;
        }

        
        if (!IsServer && connectedPeer != null)
        {
            var myNetworkId = connectedPeer.EndPoint?.ToString();
            if (!string.IsNullOrEmpty(myNetworkId) && id == myNetworkId)
            {
                Debug.Log($"[IsSelfId] ✓ 匹配连接Peer地址: {id}");
                return true;
            }
        }

        return false;
    }

    public string GetPlayerId(NetPeer peer)
    {
        if (peer == null)
        {
            if (localPlayerStatus != null && !string.IsNullOrEmpty(localPlayerStatus.EndPoint))
                return localPlayerStatus.EndPoint; 
            return $"Host:{port}";
        }

        if (playerStatuses != null && playerStatuses.TryGetValue(peer, out var st) && !string.IsNullOrEmpty(st.EndPoint))
            return st.EndPoint;
        return peer.EndPoint.ToString();
    }

    
    
    
    public void MarkPlayerJoinedSuccessfully(NetPeer peer)
    {
        if (!IsServer || peer == null) return;

        if (_peerConnectionTime.ContainsKey(peer))
        {
            var elapsed = Time.time - _peerConnectionTime[peer];
            _peerConnectionTime.Remove(peer);
            Debug.Log($"[JOIN_TIMEOUT] 玩家 {peer.EndPoint} 成功加入游戏，耗时: {elapsed:F2}秒");
        }
    }

    
    
    
    
    public void CheckJoinTimeouts()
    {
        if (!IsServer || _peerConnectionTime.Count == 0) return;

        var now = Time.time;
        var timeoutPeers = new List<NetPeer>();

        foreach (var kv in _peerConnectionTime)
        {
            var peer = kv.Key;
            var connectTime = kv.Value;
            var elapsed = now - connectTime;

            if (elapsed > JOIN_TIMEOUT_SECONDS)
            {
                timeoutPeers.Add(peer);
                Debug.LogWarning($"[JOIN_TIMEOUT] 玩家 {peer.EndPoint} 加入超时 ({elapsed:F2}秒 > {JOIN_TIMEOUT_SECONDS}秒)，即将踢出");
            }
        }

        
        foreach (var peer in timeoutPeers)
        {
            try
            {
                peer.Disconnect();
                Debug.Log($"[JOIN_TIMEOUT] 已踢出超时玩家: {peer.EndPoint}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JOIN_TIMEOUT] 踢出玩家时出错: {ex.Message}");
            }
        }
    }

    
    
    
    
    public void TryAutoReconnect()
    {
        
        float timeSinceLastReconnect = Time.time - lastReconnectTime;
        if (timeSinceLastReconnect < RECONNECT_COOLDOWN)
        {
            Debug.LogWarning($"[AUTO_RECONNECT] 重连请求被拒绝：冷却中 (剩余 {RECONNECT_COOLDOWN - timeSinceLastReconnect:F1} 秒)");
            return;
        }

        
        if (!hasSuccessfulConnection || string.IsNullOrEmpty(cachedConnectedIP) || cachedConnectedPort == 0)
        {
            Debug.LogWarning("[AUTO_RECONNECT] 无缓存的连接信息，跳过自动重连");
            return;
        }

        
        if (connectedPeer != null && connectedPeer.ConnectionState == ConnectionState.Connected)
        {
            Debug.Log("[AUTO_RECONNECT] 已经连接，跳过自动重连");
            return;
        }

        
        if (isConnecting)
        {
            Debug.LogWarning("[AUTO_RECONNECT] 正在连接中，跳过自动重连");
            return;
        }

        
        lastReconnectTime = Time.time;

        
        Debug.Log($"[AUTO_RECONNECT] 尝试自动重连到: {cachedConnectedIP}:{cachedConnectedPort}");

        
        ConnectToHost(cachedConnectedIP, cachedConnectedPort);
    }

    
    
    
    private void UpdateLocalPlayerToDatabase()
    {
        try
        {
            if (localPlayerStatus == null)
            {
                Debug.LogWarning("[NetService] 无法更新本地玩家到数据库：localPlayerStatus 为空");
                return;
            }

            
            string steamId = "";
            string steamName = "";
            string steamAvatarUrl = "";

            if (SteamManager.Initialized)
            {
                try
                {
                    var mySteamId = Steamworks.SteamUser.GetSteamID();
                    steamId = mySteamId.ToString();
                    steamName = Steamworks.SteamFriends.GetPersonaName();
                    int avatarHandle = Steamworks.SteamFriends.GetLargeFriendAvatar(mySteamId);
                    if (avatarHandle > 0)
                    {
                        steamAvatarUrl = $"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/{avatarHandle}";
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NetService] Failed to get Steam info: {ex.Message}");
                }
            }

            var playerDb = Utils.Database.PlayerInfoDatabase.Instance;

            
            bool success = playerDb.AddOrUpdatePlayer(
                steamId: steamId,
                playerName: steamName ?? localPlayerStatus.PlayerName ?? "LocalPlayer",
                avatarUrl: steamAvatarUrl,
                isLocal: true,  
                endPoint: localPlayerStatus.EndPoint,
                lastUpdate: DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            );

            if (success)
            {
                
                playerDb.SetCustomData(steamId, "Latency", localPlayerStatus.Latency);
                playerDb.SetCustomData(steamId, "IsInGame", localPlayerStatus.IsInGame);

                Debug.Log($"[NetService] ✓ 已更新本地玩家到数据库: {steamName} ({steamId}), IsLocal=true");
            }
            else
            {
                Debug.LogWarning($"[NetService] 更新本地玩家到数据库失败");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetService] 更新本地玩家到数据库异常: {ex.Message}\n{ex.StackTrace}");
        }
    }

    
    
    
    private void UpdatePlayerLastSeenInDatabase(string endPoint)
    {
        try
        {
            if (string.IsNullOrEmpty(endPoint))
            {
                Debug.LogWarning("[NetService] 无法更新 LastSeen：EndPoint 为空");
                return;
            }

            var playerDb = Utils.Database.PlayerInfoDatabase.Instance;
            var player = playerDb.GetPlayerByEndPoint(endPoint);

            if (player != null)
            {
                player.LastSeen = DateTime.Now;
                Debug.Log($"[NetService] ✓ 已更新玩家 LastSeen: {player.PlayerName} ({player.SteamId}), EndPoint={endPoint}");
            }
            else
            {
                Debug.LogWarning($"[NetService] 未找到 EndPoint={endPoint} 的玩家，无法更新 LastSeen");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetService] 更新 LastSeen 异常: {ex.Message}\n{ex.StackTrace}");
        }
    }

    
    
    
    private void SyncLatencyToDatabase(NetPeer peer, int latency)
    {
        try
        {
            if (peer == null)
                return;

            var playerDb = Utils.Database.PlayerInfoDatabase.Instance;

            
            string endPoint = GetPlayerId(peer);
            if (string.IsNullOrEmpty(endPoint))
            {
                Debug.LogWarning($"[NetService] 无法同步延迟：无法获取 EndPoint");
                return;
            }

            
            var player = playerDb.GetPlayerByEndPoint(endPoint);
            if (player != null)
            {
                
                playerDb.SetCustomData(player.SteamId, "Latency", latency);
                
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetService] 同步延迟到数据库异常: {ex.Message}");
        }
    }

    
    
    
    private void SyncIsInGameStatusToDatabase()
    {
        try
        {
            var playerDb = Utils.Database.PlayerInfoDatabase.Instance;

            
            if (IsServer)
            {
                
                if (localPlayerStatus != null && !string.IsNullOrEmpty(localPlayerStatus.EndPoint))
                {
                    var localPlayer = playerDb.GetPlayerByEndPoint(localPlayerStatus.EndPoint);
                    if (localPlayer != null)
                    {
                        playerDb.SetCustomData(localPlayer.SteamId, "IsInGame", localPlayerStatus.IsInGame);
                        if (!string.IsNullOrEmpty(localPlayerStatus.SceneId))
                        {
                            playerDb.SetCustomData(localPlayer.SteamId, "SceneId", localPlayerStatus.SceneId);
                        }
                    }
                }

                
                foreach (var kvp in playerStatuses)
                {
                    var status = kvp.Value;
                    if (status != null && !string.IsNullOrEmpty(status.EndPoint))
                    {
                        var player = playerDb.GetPlayerByEndPoint(status.EndPoint);
                        if (player != null)
                        {
                            playerDb.SetCustomData(player.SteamId, "IsInGame", status.IsInGame);
                            if (!string.IsNullOrEmpty(status.SceneId))
                            {
                                playerDb.SetCustomData(player.SteamId, "SceneId", status.SceneId);
                            }
                        }
                    }
                }
            }
            
            else
            {
                
                if (localPlayerStatus != null && !string.IsNullOrEmpty(localPlayerStatus.EndPoint))
                {
                    var localPlayer = playerDb.GetPlayerByEndPoint(localPlayerStatus.EndPoint);
                    if (localPlayer != null)
                    {
                        playerDb.SetCustomData(localPlayer.SteamId, "IsInGame", localPlayerStatus.IsInGame);
                        if (!string.IsNullOrEmpty(localPlayerStatus.SceneId))
                        {
                            playerDb.SetCustomData(localPlayer.SteamId, "SceneId", localPlayerStatus.SceneId);
                        }
                    }
                }

                
                foreach (var kvp in clientPlayerStatuses)
                {
                    var status = kvp.Value;
                    if (status != null && !string.IsNullOrEmpty(status.EndPoint))
                    {
                        var player = playerDb.GetPlayerByEndPoint(status.EndPoint);
                        if (player != null)
                        {
                            playerDb.SetCustomData(player.SteamId, "IsInGame", status.IsInGame);
                            if (!string.IsNullOrEmpty(status.SceneId))
                            {
                                playerDb.SetCustomData(player.SteamId, "SceneId", status.SceneId);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetService] 同步 IsInGame 状态到数据库异常: {ex.Message}");
        }
    }

    public void SetRelayRoomId(string roomId)
    {
        _relayRoomId = roomId;
        Debug.Log($"[NetService] 设置中继房间ID: {roomId}");
    }

    public void CreateVirtualPeerForRelay()
    {
        Debug.Log("[NetService] 为中继连接创建虚拟Peer");
    }

    public void SendVoiceData(Net.HybridNet.VoiceDataMessage message)
    {
        if (networkStarted)
        {
            Net.HybridNet.HybridNetCore.Send(message);
        }
    }

    public void SendVoiceState(Net.HybridNet.VoiceStateMessage message)
    {
        if (networkStarted)
        {
            Net.HybridNet.HybridNetCore.Send(message);
        }
    }
}