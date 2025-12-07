using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using EscapeFromDuckovCoopMod.Utils.Logger.Tools;
using Newtonsoft.Json;

namespace EscapeFromDuckovCoopMod.Net.Relay;

public class RelayServerManager : MonoBehaviour
{
    public static RelayServerManager Instance { get; private set; }
    
    private RelayNode _selectedNode;
    private readonly List<OnlineRoom> _onlineRooms = new List<OnlineRoom>();
    private OnlineRoom _myRoom;
    
    private UdpClient _udpClient;
    private bool _isConnected = false;
    private float _lastHeartbeatTime = 0f;
    private const float HeartbeatInterval = 10f;
    
    private bool _isReceiving = false;
    
    public RelayNode SelectedNode => _selectedNode;
    public IReadOnlyList<OnlineRoom> OnlineRooms => _onlineRooms;
    public bool IsConnectedToRelay => _isConnected;
    public OnlineRoom MyRoom => _myRoom;
    
    public event Action<OnlineRoom[]> OnRoomListUpdated;
    public event Action<RelayNode> OnNodeSelected;
    public event Action<RelayNode> OnNodePingCompleted;
    public event Action<string> OnError;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Update()
    {
        if (!_isConnected || _selectedNode == null) return;
        
        if (Time.time - _lastHeartbeatTime >= HeartbeatInterval && _myRoom != null)
        {
            SendHeartbeat();
            _lastHeartbeatTime = Time.time;
        }
    }
    
    public void SelectNode(RelayNode node)
    {
        if (node == null)
        {
            LoggerHelper.LogWarning("[Relay] 选择的节点为空");
            return;
        }
        
        if (!node.IsAvailable)
        {
            LoggerHelper.LogWarning($"[Relay] 节点 {node.NodeName} 当前不可用或正在维护");
            OnError?.Invoke($"节点 {node.NodeName} 当前不可用，请选择其他节点");
            return;
        }
        
        _selectedNode = node;
        LoggerHelper.Log($"[Relay] 选择节点: {node.NodeName} ({node.DisplayAddress})");
        OnNodeSelected?.Invoke(node);
        
        StartCoroutine(PingNodeCoroutine(node));
    }
    
    public IEnumerator PingAllNodesCoroutine()
    {
        var nodes = RelayNode.GetAvailableNodes();
        foreach (var node in nodes)
        {
            yield return PingNodeCoroutine(node);
            yield return new WaitForSeconds(0.5f);
        }
        
        LoggerHelper.Log("[Relay] 所有节点ping测试完成");
        OnNodeSelected?.Invoke(null);
    }
    
    private IEnumerator PingNodeCoroutine(RelayNode node)
    {
        var startTime = Time.realtimeSinceStartup;
        UdpClient udpPing = null;
        System.Threading.Tasks.Task<UdpReceiveResult> receiveTask = null;
        bool hasError = false;
        
        try
        {
            udpPing = new UdpClient();
            udpPing.Client.SendTimeout = 2000;
            udpPing.Client.ReceiveTimeout = 2000;
            
            var endpoint = new IPEndPoint(IPAddress.Parse(node.Address), node.Port);
            var pingData = Encoding.UTF8.GetBytes("{\"type\":\"ping\"}");
            
            LoggerHelper.Log($"[Relay] 向节点 {node.NodeName} 发送UDP ping");
            var sentBytes = udpPing.Send(pingData, pingData.Length, endpoint);
            LoggerHelper.Log($"[Relay] 已发送 {sentBytes} 字节到节点 {node.NodeName}，等待响应...");
            
            receiveTask = udpPing.ReceiveAsync();
        }
        catch (Exception ex)
        {
            node.IsAvailable = false;
            node.Latency = -1;
            LoggerHelper.LogError($"[Relay] Ping节点失败 {node.NodeName}: {ex.GetType().Name} - {ex.Message}");
            hasError = true;
        }
        
        if (!hasError && receiveTask != null)
        {
            var timeoutTime = Time.realtimeSinceStartup + 2f;
            
            while (!receiveTask.IsCompleted && Time.realtimeSinceStartup < timeoutTime)
            {
                yield return null;
            }
            
            if (receiveTask.IsCompleted)
            {
                try
                {
                    var result = receiveTask.Result;
                    var response = Encoding.UTF8.GetString(result.Buffer);
                    var elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
                    
                    node.Latency = Mathf.RoundToInt(elapsed);
                    node.LastPingTime = DateTime.Now;
                    
                    try
                    {
                        var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                        if (jsonResponse != null && jsonResponse.ContainsKey("nodes_status"))
                        {
                            var nodesStatus = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonResponse["nodes_status"].ToString());
                            if (nodesStatus != null && nodesStatus.ContainsKey(node.NodeId))
                            {
                                var status = nodesStatus[node.NodeId];
                                node.IsAvailable = status == "online";
                                if (!node.IsAvailable)
                                {
                                    LoggerHelper.LogWarning($"[Relay] 节点 {node.NodeName} 当前状态: {status}");
                                }
                            }
                            else
                            {
                                node.IsAvailable = true;
                            }
                        }
                        else
                        {
                            node.IsAvailable = true;
                        }
                    }
                    catch
                    {
                        node.IsAvailable = true;
                    }
                    
                    LoggerHelper.Log($"[Relay] 节点 {node.NodeName} 响应成功，延迟: {node.Latency}ms，状态: {(node.IsAvailable ? "可用" : "维护中")}");
                }
                catch (Exception ex)
                {
                    node.IsAvailable = false;
                    node.Latency = -1;
                    LoggerHelper.LogError($"[Relay] 节点 {node.NodeName} 响应解析失败: {ex.Message}");
                }
            }
            else
            {
                node.IsAvailable = false;
                node.Latency = -1;
                LoggerHelper.LogWarning($"[Relay] 节点 {node.NodeName} 超时（2秒无响应），UDP包可能被丢弃或服务器未运行");
            }
        }
        
        udpPing?.Close();
        
        OnNodePingCompleted?.Invoke(node);
    }
    
    public void ConnectToRelay()
    {
        if (_selectedNode == null)
        {
            OnError?.Invoke("请先选择节点");
            return;
        }
        
        try
        {
            _udpClient = new UdpClient();
            _isConnected = true;
            LoggerHelper.Log($"[Relay] 已连接到节点: {_selectedNode.NodeName}");
            
            if (!_isReceiving)
            {
                StartCoroutine(ReceiveMessagesCoroutine());
            }
            
            RequestRoomList();
        }
        catch (Exception ex)
        {
            LoggerHelper.LogError($"[Relay] 连接失败: {ex.Message}");
            OnError?.Invoke($"连接失败: {ex.Message}");
            _isConnected = false;
        }
    }
    
    public void DisconnectFromRelay()
    {
        if (_myRoom != null)
        {
            UnregisterRoom();
        }
        
        _isReceiving = false;
        
        if (_udpClient != null)
        {
            _udpClient.Close();
            _udpClient = null;
        }
        
        _isConnected = false;
        _onlineRooms.Clear();
        
        LoggerHelper.DisableRelayLogging();
        LoggerHelper.Log("[Relay] 已断开中继服务器连接");
    }
    
    private IEnumerator ReceiveMessagesCoroutine()
    {
        _isReceiving = true;
        LoggerHelper.Log("[Relay] 开始接收UDP消息");
        
        while (_isConnected && _udpClient != null)
        {
            if (_udpClient.Available > 0)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Any, 0);
                    var data = _udpClient.Receive(ref endpoint);
                    var json = Encoding.UTF8.GetString(data);
                    
                    LoggerHelper.Log($"[Relay] 收到消息: {json}");
                    
                    var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    if (response != null && response.ContainsKey("type"))
                    {
                        var type = response["type"].ToString();
                        HandleServerResponse(type, json);
                    }
                }
                catch (Exception ex)
                {
                    LoggerHelper.LogError($"[Relay] 接收消息失败: {ex.Message}");
                }
            }
            
            yield return null;
        }
        
        _isReceiving = false;
        LoggerHelper.Log("[Relay] 停止接收UDP消息");
    }
    
    private void HandleServerResponse(string type, string json)
    {
        try
        {
            switch (type)
            {
                case "rooms_list":
                    var roomsResponse = JsonConvert.DeserializeObject<RoomsListResponse>(json);
                    if (roomsResponse != null && roomsResponse.rooms != null)
                    {
                        UpdateRoomList(roomsResponse.rooms.ToList());
                    }
                    break;
                    
                case "register_ok":
                    LoggerHelper.Log("[Relay] 房间注册成功");
                    if (_myRoom != null && _selectedNode != null)
                    {
                        LoggerHelper.EnableRelayLogging(_selectedNode.Address, _selectedNode.Port, _myRoom.RoomId);
                        LoggerHelper.Log("[Relay] 已启用日志上传功能");
                        
                        var localPlayer = Utils.Database.PlayerInfoDatabase.Instance.GetLocalPlayer();
                        string clientId = localPlayer?.SteamId ?? System.Guid.NewGuid().ToString();
                        var joinMessage = new
                        {
                            type = "join_room",
                            room_id = _myRoom.RoomId,
                            client_id = clientId
                        };
                        SendToRelay(joinMessage);
                        
                        var netService = NetService.Instance;
                        if (netService != null)
                        {
                            netService.SetRelayRoomId(_myRoom.RoomId);
                        }
                        
                        LoggerHelper.Log($"[Relay] 房主已加入中继房间: {_myRoom.RoomId}");
                    }
                    break;
                    
                case "error":
                    var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(json);
                    OnError?.Invoke(errorResponse?.message ?? "未知错误");
                    break;
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.LogError($"[Relay] 处理服务器响应失败: {ex.Message}");
        }
    }
    
    private class RoomsListResponse
    {
        public string type { get; set; }
        public OnlineRoom[] rooms { get; set; }
    }
    
    private class ErrorResponse
    {
        public string type { get; set; }
        public string message { get; set; }
    }
    
    public void RegisterRoom(string roomName, int maxPlayers, bool hasPassword, string mapName, string hostAddress, int hostPort)
    {
        if (!_isConnected || _selectedNode == null)
        {
            OnError?.Invoke("未连接到中继服务器");
            return;
        }
        
        var localPlayer = Utils.Database.PlayerInfoDatabase.Instance.GetLocalPlayer();
        
        _myRoom = new OnlineRoom
        {
            RoomId = Guid.NewGuid().ToString(),
            RoomName = roomName,
            HostName = localPlayer?.PlayerName ?? "Unknown",
            HostSteamId = localPlayer?.SteamId ?? "0",
            NodeId = _selectedNode.NodeId,
            CurrentPlayers = 1,
            MaxPlayers = maxPlayers,
            HasPassword = hasPassword,
            MapName = mapName,
            CreateTime = DateTime.Now,
            LastHeartbeat = DateTime.Now
        };
        
        var message = new
        {
            type = "register_room",
            room_data = new
            {
                room_id = _myRoom.RoomId,
                room_name = _myRoom.RoomName,
                host_name = _myRoom.HostName,
                host_steam_id = _myRoom.HostSteamId,
                node_id = _myRoom.NodeId,
                current_players = _myRoom.CurrentPlayers,
                max_players = _myRoom.MaxPlayers,
                has_password = _myRoom.HasPassword,
                map_name = _myRoom.MapName,
                host_address = hostAddress,
                host_port = hostPort
            }
        };
        
        SendToRelay(message);
        LoggerHelper.Log($"[Relay] 注册房间: {roomName}，节点: {_selectedNode.NodeId}，主机: {hostAddress}:{hostPort}");
    }
    
    public void UnregisterRoom()
    {
        if (_myRoom == null) return;
        
        var message = new
        {
            type = "unregister_room",
            room_id = _myRoom.RoomId
        };
        
        SendToRelay(message);
        _myRoom = null;
        LoggerHelper.Log("[Relay] 注销房间");
    }
    
    private void SendHeartbeat()
    {
        if (_myRoom == null) return;
        
        _myRoom.LastHeartbeat = DateTime.Now;
        _myRoom.CurrentPlayers = GetCurrentPlayerCount();
        
        if (LocalPlayerManager.Instance != null && LocalPlayerManager.Instance.ComputeIsInGame(out var sceneId) && !string.IsNullOrEmpty(sceneId))
        {
            _myRoom.MapName = Utils.SceneNameMapper.GetDisplayName(sceneId);
        }
        
        var message = new
        {
            type = "heartbeat",
            room_id = _myRoom.RoomId
        };
        
        SendToRelay(message);
    }
    
    public void RequestRoomList()
    {
        if (!_isConnected) return;
        
        var message = new
        {
            type = "get_rooms"
        };
        
        SendToRelay(message);
    }
    
    private void SendToRelay(object message)
    {
        if (_udpClient == null || _selectedNode == null) return;
        
        try
        {
            var json = JsonConvert.SerializeObject(message);
            var data = Encoding.UTF8.GetBytes(json);
            var endpoint = new IPEndPoint(IPAddress.Parse(_selectedNode.Address), _selectedNode.Port);
            _udpClient.Send(data, data.Length, endpoint);
        }
        catch (Exception ex)
        {
            LoggerHelper.LogError($"[Relay] 发送消息失败: {ex.Message}");
        }
    }
    
    public void UpdateRoomList(List<OnlineRoom> rooms)
    {
        _onlineRooms.Clear();
        
        var availableNodes = RelayNode.GetAvailableNodes();
        foreach (var room in rooms)
        {
            if (!room.IsP2P)
            {
                var node = availableNodes.FirstOrDefault(n => n.NodeId == room.NodeId);
                if (node != null && node.Latency >= 0)
                {
                    room.Latency = node.Latency;
                }
            }
        }
        
        _onlineRooms.AddRange(rooms);
        OnRoomListUpdated?.Invoke(_onlineRooms.ToArray());
        LoggerHelper.Log($"[Relay] 房间列表已更新: {_onlineRooms.Count} 个房间");
    }
    
    private int GetCurrentPlayerCount()
    {
        var netService = NetService.Instance;
        if (netService == null) return 1;
        
        int count = 1;
        if (netService.remoteCharacters != null)
            count += netService.remoteCharacters.Count;
        
        return count;
    }
    
    public void JoinRoom(OnlineRoom room)
    {
        if (room == null) return;
        
        LoggerHelper.Log($"[Relay] 通过中继加入房间: {room.RoomName}");
        
        var localPlayer = Utils.Database.PlayerInfoDatabase.Instance.GetLocalPlayer();
        string clientId = localPlayer?.SteamId ?? System.Guid.NewGuid().ToString();
        
        var message = new
        {
            type = "join_room",
            room_id = room.RoomId,
            client_id = clientId
        };
        
        SendToRelay(message);
        
        _myRoom = room;
        
        LoggerHelper.Log($"[Relay] 已发送加入房间请求，房间ID: {room.RoomId}");
        
        var netService = NetService.Instance;
        if (netService != null)
        {
            if (!netService.networkStarted)
            {
                netService.StartNetwork(false);
            }
            
            netService.SetRelayRoomId(room.RoomId);
            netService.CreateVirtualPeerForRelay();
        }
    }
    
    private void OnDestroy()
    {
        DisconnectFromRelay();
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
