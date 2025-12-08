using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using EscapeFromDuckovCoopMod.Net.Relay;
using EscapeFromDuckovCoopMod.Utils.Logger.Tools;

namespace EscapeFromDuckovCoopMod;

public class OnlineLobbyUI : MonoBehaviour
{
    public static OnlineLobbyUI Instance { get; private set; }
    
    private MModUI _mainUI;
    private MModUIComponents _components;
    private RelayServerManager _relayManager;
    
    private GameObject _nodeSelectorWindow;
    private bool _isNodeSelectorOpen = false;
    
    private readonly Dictionary<string, GameObject> _roomEntries = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> _nodeEntries = new Dictionary<string, GameObject>();
    
    private Coroutine _updateRoomListCoroutine;
    private Coroutine _nodeStatusUpdateCoroutine;
    private const float UPDATE_DEBOUNCE_DELAY = 0.3f;
    private const float NODE_PING_INTERVAL = 3f;
    
    private bool _shouldCreateRoomAfterNodeSelection = false;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    public void Initialize(MModUI mainUI, MModUIComponents components)
    {
        LoggerHelper.Log($"[OnlineLobby] Initialize被调用，mainUI: {(mainUI != null ? "有效" : "null")}");
        _mainUI = mainUI;
        _components = components;
        _relayManager = RelayServerManager.Instance;
        
        LoggerHelper.Log($"[OnlineLobby] 保存引用完成，_mainUI: {(_mainUI != null ? "有效" : "null")}");
        LoggerHelper.Log($"[OnlineLobby] MainPanel状态: {(_components?.MainPanel != null ? $"active={_components.MainPanel.activeSelf}" : "null")}");
        
        if (_relayManager == null)
        {
            LoggerHelper.LogWarning("[OnlineLobby] RelayServerManager未找到，将在首次打开时初始化");
        }
        else
        {
            LoggerHelper.Log("[OnlineLobby] 开始订阅事件...");
            _relayManager.OnRoomListUpdated += OnRoomListUpdated;
            LoggerHelper.Log("[OnlineLobby] OnRoomListUpdated订阅完成");
            _relayManager.OnNodeSelected += OnNodeSelected;
            LoggerHelper.Log("[OnlineLobby] OnNodeSelected订阅完成");
            _relayManager.OnNodePingCompleted += OnNodePingCompleted;
            LoggerHelper.Log("[OnlineLobby] OnNodePingCompleted订阅完成");
            _relayManager.OnError += OnError;
            LoggerHelper.Log("[OnlineLobby] OnError订阅完成");
            LoggerHelper.Log($"[OnlineLobby] 事件订阅后MainPanel状态: {(_components?.MainPanel != null ? $"active={_components.MainPanel.activeSelf}" : "null")}");
            
            LoggerHelper.Log("[OnlineLobby] 启动PingAndAutoConnectCoroutine");
            StartCoroutine(PingAndAutoConnectCoroutine());
        }
        
        var lobbyManager = mainUI.LobbyManager;
        if (lobbyManager != null)
        {
            LoggerHelper.Log("[OnlineLobby] 订阅LobbyManager事件");
            lobbyManager.LobbyListUpdated += OnSteamLobbyListUpdated;
        }
        
        LoggerHelper.Log($"[OnlineLobby] Initialize完成，MainPanel最终状态: {(_components?.MainPanel != null ? $"active={_components.MainPanel.activeSelf}" : "null")}");
    }
    
    private void OnNodePingCompleted(RelayNode node)
    {
        if (node != null)
        {
            UpdateNodeEntry(node);
        }
    }
    
    private void BuildNodeSelectorWindow()
    {
        if (_mainUI == null)
        {
            LoggerHelper.LogError("[OnlineLobby] MModUI为null");
            return;
        }
        
        if (_mainUI.Canvas == null)
        {
            LoggerHelper.LogError("[OnlineLobby] Canvas为null，MModUI可能未完成初始化");
            return;
        }
        
        if (_components == null)
        {
            LoggerHelper.LogError("[OnlineLobby] _components为null");
            return;
        }
        
        _nodeSelectorWindow = _mainUI.CreateModernPanel("NodeSelectorWindow", _mainUI.Canvas.transform, new Vector2(600, 500), Vector2.zero);
        _mainUI.MakeDraggable(_nodeSelectorWindow);
        
        LoggerHelper.Log("[OnlineLobby] 节点选择器窗口已创建");
        
        var layout = _nodeSelectorWindow.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 30, 30);
        layout.spacing = 20;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = false;
        
        var titleBar = _mainUI.CreateTitleBar(_nodeSelectorWindow.transform);
        _mainUI.CreateText("Title", titleBar.transform, "选择服务器节点", 24, MModUI.ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);
        
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(titleBar.transform, false);
        var spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleWidth = 1;
        
        _mainUI.CreateIconButton("CloseBtn", titleBar.transform, "x", () =>
        {
            CloseNodeSelector();
        }, 40, MModUI.ModernColors.Error);
        
        _mainUI.CreateText("Hint", _nodeSelectorWindow.transform, 
            "选择节点使用在线大厅，或跳过直接创建本地主机", 14, MModUI.ModernColors.TextSecondary);
        
        var buttonRow = _mainUI.CreateHorizontalGroup(_nodeSelectorWindow.transform, "ButtonRow");
        
        _mainUI.CreateModernButton("SkipBtn", buttonRow.transform, "跳过（本地主机）", () =>
        {
            CreateDirectHost();
        }, 200, MModUI.ModernColors.Warning, 45, 16);
        
        var scrollArea = CreateScrollArea(_nodeSelectorWindow.transform, "NodeListScroll", 400);
        _components.NodeListContent = scrollArea.transform.Find("Viewport/Content");
        
        BuildNodeList();
        
        _nodeSelectorWindow.SetActive(false);
    }
    
    private void BuildNodeList()
    {
        if (_components.NodeListContent == null)
        {
            LoggerHelper.LogError("[OnlineLobby] NodeListContent为null");
            return;
        }
        
        foreach (Transform child in _components.NodeListContent)
        {
            Destroy(child.gameObject);
        }
        _nodeEntries.Clear();
        
        var nodes = RelayNode.GetAvailableNodes();
        LoggerHelper.Log($"[OnlineLobby] 获取到{nodes.Length}个节点");
        
        foreach (var node in nodes)
        {
            LoggerHelper.Log($"[OnlineLobby] 创建节点条目: {node.NodeName}");
            CreateNodeEntry(node);
        }
    }
    
    private void CreateNodeEntry(RelayNode node)
    {
        var entry = _mainUI.CreateModernCard(_components.NodeListContent, $"Node_{node.NodeId}");
        _nodeEntries[node.NodeId] = entry;
        
        var button = entry.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = MModUI.GlassTheme.CardBg;
        colors.highlightedColor = MModUI.GlassTheme.ButtonHover;
        colors.pressedColor = MModUI.GlassTheme.ButtonActive;
        button.colors = colors;
        button.onClick.AddListener(() => SelectNode(node));
        
        var headerRow = _mainUI.CreateHorizontalGroup(entry.transform, "Header");
        
        var latencyDot = new GameObject("LatencyDot");
        latencyDot.transform.SetParent(headerRow.transform, false);
        var dotLayout = latencyDot.AddComponent<LayoutElement>();
        dotLayout.preferredWidth = 12;
        dotLayout.preferredHeight = 12;
        var dotImage = latencyDot.AddComponent<Image>();
        dotImage.color = GetLatencyColor(node.Latency);
        
        _mainUI.CreateText("Name", headerRow.transform, node.NodeName, 18, 
            MModUI.ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);
        
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(headerRow.transform, false);
        var spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleWidth = 1;
        
        var latencyText = _mainUI.CreateText("Latency", headerRow.transform, 
            node.Latency >= 0 ? $"{node.Latency}ms" : "测试中...", 16, 
            GetLatencyColor(node.Latency));
        
        _mainUI.CreateDivider(entry.transform);
        
        var infoRow = _mainUI.CreateHorizontalGroup(entry.transform, "Info");
        _mainUI.CreateText("Address", infoRow.transform, $"节点地址: {node.DisplayAddress}", 13, 
            MModUI.ModernColors.TextSecondary);
    }
    
    private void UpdateNodeEntry(RelayNode node)
    {
        if (!_nodeEntries.TryGetValue(node.NodeId, out var entry) || entry == null)
            return;
        
        var button = entry.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = node.IsAvailable;
        }
        
        var headerRow = entry.transform.Find("Header");
        if (headerRow == null) return;
        
        var latencyDot = headerRow.Find("LatencyDot");
        if (latencyDot != null)
        {
            var dotImage = latencyDot.GetComponent<Image>();
            if (dotImage != null)
            {
                if (node.IsAvailable)
                {
                    dotImage.color = GetLatencyColor(node.Latency);
                }
                else
                {
                    dotImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                }
            }
        }
        
        var latencyText = headerRow.Find("Latency");
        if (latencyText != null)
        {
            var textComp = latencyText.GetComponent<TMP_Text>();
            if (textComp != null)
            {
                if (node.IsAvailable)
                {
                    textComp.text = node.Latency >= 0 ? $"{node.Latency}ms" : "测试中...";
                    textComp.color = GetLatencyColor(node.Latency);
                }
                else
                {
                    textComp.text = "维护中";
                    textComp.color = new Color(1f, 0.53f, 0f, 1f);
                }
            }
        }
    }
    
    private Color GetLatencyColor(int latency)
    {
        if (latency < 0) return MModUI.ModernColors.TextTertiary;
        if (latency < 50) return MModUI.ModernColors.Success;
        if (latency < 100) return MModUI.ModernColors.Warning;
        if (latency < 200) return new Color(1f, 0.65f, 0f, 1f);
        return MModUI.ModernColors.Error;
    }
    
    private IEnumerator PingAndAutoConnectCoroutine()
    {
        yield return _relayManager.PingAllNodesCoroutine();
        
        var availableNodes = RelayNode.GetAvailableNodes().Where(n => n.IsAvailable && n.Latency >= 0).OrderBy(n => n.Latency).ToArray();
        if (availableNodes.Length > 0)
        {
            var firstNode = availableNodes[0];
            LoggerHelper.Log($"[OnlineLobby] 自动连接到最佳节点: {firstNode.NodeName} ({firstNode.Latency}ms)");
            _relayManager.SelectNode(firstNode);
            _relayManager.ConnectToRelay();
            
            if (_components?.SelectedNodeText != null)
            {
                _components.SelectedNodeText.text = $"当前节点: {firstNode.NodeName} - {firstNode.DisplayAddress} ({firstNode.Latency}ms)";
            }
        }
        else
        {
            LoggerHelper.LogWarning("[OnlineLobby] 没有可用节点");
        }
    }
    
    private void SelectNode(RelayNode node)
    {
        LoggerHelper.Log($"[OnlineLobby] 选择节点: {node.NodeName}，当前状态: {(node.IsAvailable ? "可用" : "不可用")}，延迟: {node.Latency}ms");
        
        if (!node.IsAvailable)
        {
            LoggerHelper.LogWarning($"[OnlineLobby] 节点 {node.NodeName} 当前不可用，跳过选择");
            CharacterMainControl.Main?.PopText($"节点 {node.NodeName} 当前不可用或正在维护", 3f);
            return;
        }
        
        _relayManager.SelectNode(node);
        
        if (_relayManager.SelectedNode == null)
        {
            LoggerHelper.LogError("[OnlineLobby] 节点选择失败");
            return;
        }
        
        _relayManager.DisconnectFromRelay();
        _relayManager.ConnectToRelay();
        
        if (!_relayManager.IsConnectedToRelay)
        {
            LoggerHelper.LogError("[OnlineLobby] 连接到中继服务器失败");
            return;
        }
        
        CloseNodeSelector();
        
        if (_components.SelectedNodeText != null)
        {
            _components.SelectedNodeText.text = $"当前节点: {node.NodeName} - {node.DisplayAddress} ({node.Latency}ms)";
        }
        
        LoggerHelper.Log($"[OnlineLobby] 已切换到节点: {node.NodeName}");
        
        if (_shouldCreateRoomAfterNodeSelection)
        {
            _shouldCreateRoomAfterNodeSelection = false;
            CreateHostRoom();
        }
        else
        {
            CharacterMainControl.Main?.PopText($"已切换到节点: {node.NodeName}", 2f);
        }
    }
    
    public void CreateHostRoom()
    {
        if (_relayManager == null || _relayManager.SelectedNode == null)
        {
            LoggerHelper.LogError("[OnlineLobby] 未选择节点，无法创建房间");
            CharacterMainControl.Main?.PopText("请先选择节点", 2f);
            return;
        }
        
        var netService = ModBehaviourF.Instance;
        if (netService == null)
        {
            LoggerHelper.LogError("[OnlineLobby] NetService未找到");
            return;
        }
        
        int serverPort = netService.port;
        if (serverPort <= 0)
        {
            serverPort = 9050;
        }
        
        netService.port = serverPort;
        netService.StartNetwork(true);
        
        var localPlayer = Utils.Database.PlayerInfoDatabase.Instance.GetLocalPlayer();
        string roomName = localPlayer != null ? $"{localPlayer.PlayerName}的房间" : "联机房间";
        
        bool isInGame = LocalPlayerManager.Instance.ComputeIsInGame(out var sceneId);
        string mapName = isInGame && !string.IsNullOrEmpty(sceneId) 
            ? Utils.SceneNameMapper.GetDisplayName(sceneId) 
            : "";
        
        string hostAddress = "0.0.0.0";
        _relayManager.RegisterRoom(roomName, 4, false, mapName, hostAddress, serverPort);
        
        LoggerHelper.Log($"[OnlineLobby] 主机已创建并注册到节点: {_relayManager.SelectedNode.NodeName}，监听端口: {serverPort}");
        
        if (CharacterMainControl.Main != null)
        {
            CharacterMainControl.Main.PopText($"房间已创建并上传到: {_relayManager.SelectedNode.NodeName}", 3f);
        }
    }
    
    public void OpenNodeSelector(bool createRoomAfterSelection = true)
    {
        LoggerHelper.Log($"[OnlineLobby] OpenNodeSelector被调用，_mainUI状态: {(_mainUI != null ? "有效" : "null")}");
        
        _shouldCreateRoomAfterNodeSelection = createRoomAfterSelection;
        
        if (_mainUI == null)
        {
            _mainUI = MModUI.Instance;
            LoggerHelper.Log($"[OnlineLobby] 尝试从MModUI.Instance获取引用: {(_mainUI != null ? "成功" : "失败")}");
            
            if (_mainUI == null)
            {
                LoggerHelper.LogError("[OnlineLobby] 无法获取MModUI实例");
                return;
            }
        }
        
        if (_components == null)
        {
            LoggerHelper.LogWarning("[OnlineLobby] _components为null，创建新实例");
            _components = new MModUIComponents();
        }
        
        if (_relayManager == null)
        {
            _relayManager = RelayServerManager.Instance;
            if (_relayManager == null)
            {
                LoggerHelper.LogError("[OnlineLobby] RelayServerManager仍未找到");
                return;
            }
            
            _relayManager.OnRoomListUpdated += OnRoomListUpdated;
            _relayManager.OnNodeSelected += OnNodeSelected;
            _relayManager.OnNodePingCompleted += OnNodePingCompleted;
            _relayManager.OnError += OnError;
        }
        
        if (_nodeSelectorWindow == null)
        {
            LoggerHelper.Log("[OnlineLobby] 节点选择器窗口为null，开始创建");
            BuildNodeSelectorWindow();
            
            if (_nodeSelectorWindow == null)
            {
                LoggerHelper.LogError("[OnlineLobby] 创建节点选择器窗口失败");
                return;
            }
        }
        
        _isNodeSelectorOpen = true;
        _nodeSelectorWindow.SetActive(true);
        
        LoggerHelper.Log("[OnlineLobby] 节点选择器窗口已显示");
        
        BuildNodeList();
        StartCoroutine(_relayManager.PingAllNodesCoroutine());
        
        if (_nodeStatusUpdateCoroutine != null)
        {
            StopCoroutine(_nodeStatusUpdateCoroutine);
        }
        _nodeStatusUpdateCoroutine = StartCoroutine(NodeStatusUpdateLoop());
    }
    
    public void CloseNodeSelector()
    {
        if (_nodeSelectorWindow == null) return;
        
        _isNodeSelectorOpen = false;
        _nodeSelectorWindow.SetActive(false);
        
        if (_nodeStatusUpdateCoroutine != null)
        {
            StopCoroutine(_nodeStatusUpdateCoroutine);
            _nodeStatusUpdateCoroutine = null;
        }
    }
    
    private void CreateDirectHost()
    {
        CloseNodeSelector();
        
        var netService = ModBehaviourF.Instance;
        if (netService == null)
        {
            LoggerHelper.LogError("[OnlineLobby] NetService未找到");
            return;
        }
        
        int serverPort = netService.port;
        if (serverPort <= 0)
        {
            serverPort = 9050;
        }
        
        netService.port = serverPort;
        netService.StartNetwork(true);
        
        LoggerHelper.Log($"[OnlineLobby] 创建本地主机，端口: {serverPort}");
        
        if (CharacterMainControl.Main != null)
        {
            CharacterMainControl.Main.PopText($"主机已创建，端口: {serverPort}", 3f);
        }
    }
    
    public void BuildOnlineRoomList(Transform parent)
    {
    }
    
    private List<OnlineRoom> _steamP2PRooms = new List<OnlineRoom>();
    private List<OnlineRoom> _onlineRooms = new List<OnlineRoom>();
    
    private void OnRoomListUpdated(OnlineRoom[] rooms)
    {
        _onlineRooms = rooms.ToList();
        UpdateCombinedRoomList();
    }
    
    private void OnSteamLobbyListUpdated(IReadOnlyList<SteamLobbyManager.LobbyInfo> lobbies)
    {
        _steamP2PRooms.Clear();
        foreach (var lobby in lobbies)
        {
            var room = new OnlineRoom
            {
                RoomId = lobby.LobbyId.ToString(),
                RoomName = lobby.LobbyName,
                HostName = lobby.HostName,
                CurrentPlayers = lobby.MemberCount,
                MaxPlayers = lobby.MaxMembers,
                HasPassword = lobby.RequiresPassword,
                IsP2P = true,
                P2PLobbyId = lobby.LobbyId,
                CreateTime = DateTime.Now,
                LastHeartbeat = DateTime.Now,
                NodeId = "steam_p2p",
                MapName = ""
            };
            _steamP2PRooms.Add(room);
        }
        UpdateCombinedRoomList();
    }
    
    private void UpdateCombinedRoomList()
    {
        if (_updateRoomListCoroutine != null)
        {
            StopCoroutine(_updateRoomListCoroutine);
        }
        
        var combinedRooms = new List<OnlineRoom>();
        combinedRooms.AddRange(_steamP2PRooms);
        combinedRooms.AddRange(_onlineRooms);
        _updateRoomListCoroutine = StartCoroutine(DebouncedUpdateRoomList(combinedRooms));
    }
    
    private void UpdateRoomList(List<OnlineRoom> rooms)
    {
        if (_updateRoomListCoroutine != null)
        {
            StopCoroutine(_updateRoomListCoroutine);
        }
        _updateRoomListCoroutine = StartCoroutine(DebouncedUpdateRoomList(rooms));
    }
    
    private IEnumerator DebouncedUpdateRoomList(List<OnlineRoom> rooms)
    {
        yield return new WaitForSeconds(UPDATE_DEBOUNCE_DELAY);
        yield return UpdateRoomListCoroutine(rooms);
        _updateRoomListCoroutine = null;
    }
    
    private IEnumerator UpdateRoomListCoroutine(List<OnlineRoom> rooms)
    {
        if (_components.OnlineRoomListContent == null)
        {
            LoggerHelper.LogError("[OnlineLobby] 在线房间列表UI未创建");
            yield break;
        }
        
        LoggerHelper.Log($"[OnlineLobby] 更新房间列表，共 {rooms.Count} 个房间");
        
        foreach (Transform child in _components.OnlineRoomListContent)
        {
            Destroy(child.gameObject);
        }
        _roomEntries.Clear();
        
        int count = 0;
        foreach (var room in rooms)
        {
            CreateRoomEntry(room);
            count++;
            
            if (count % 5 == 0)
            {
                yield return null;
            }
        }
        
        if (_components.OnlineRoomCountText != null)
        {
            _components.OnlineRoomCountText.text = $"在线房间: {rooms.Count}";
        }
    }
    
    private void CreateRoomEntry(OnlineRoom room)
    {
        var entry = _mainUI.CreateModernCard(_components.OnlineRoomListContent, $"Room_{room.RoomId}");
        _roomEntries[room.RoomId] = entry;
        
        var headerRow = _mainUI.CreateHorizontalGroup(entry.transform, "Header");
        
        var roomDisplayName = room.HasPassword ? $"[密] {room.RoomName}" : room.RoomName;
        _mainUI.CreateText("Name", headerRow.transform, roomDisplayName, 16, 
            MModUI.ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);
        
        if (room.IsP2P)
        {
            _mainUI.CreateBadge(headerRow.transform, "P2P", MModUI.ModernColors.Info);
        }
        
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(headerRow.transform, false);
        var spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleWidth = 1;
        
        _mainUI.CreateBadge(headerRow.transform, room.PlayersText, 
            room.IsFull ? MModUI.ModernColors.Error : MModUI.ModernColors.Success);
        
        _mainUI.CreateDivider(entry.transform);
        
        var infoRow = _mainUI.CreateHorizontalGroup(entry.transform, "Info");
        
        _mainUI.CreateText("Host", infoRow.transform, $"房主: {room.HostName}", 13, 
            MModUI.ModernColors.TextSecondary);
        
        if (room.IsP2P)
        {
            _mainUI.CreateText("Node", infoRow.transform, "节点: Steam P2P", 13, 
                MModUI.ModernColors.Info);
        }
        else
        {
            _mainUI.CreateText("Node", infoRow.transform, $"节点: {room.GetNodeDisplayName()}", 13, 
                MModUI.ModernColors.TextSecondary);
            
            if (room.Latency >= 0)
            {
                _mainUI.CreateText("Latency", infoRow.transform, $"{room.Latency}ms", 13, 
                    GetLatencyColor(room.Latency));
            }
        }
        
        _mainUI.CreateText("Map", infoRow.transform, 
            string.IsNullOrEmpty(room.MapName) ? "未知地图" : room.MapName, 13, 
            MModUI.ModernColors.TextTertiary);
        
        var buttonRow = _mainUI.CreateHorizontalGroup(entry.transform, "Buttons");
        
        if (!room.IsFull)
        {
            _mainUI.CreateModernButton("JoinBtn", buttonRow.transform, "加入", () =>
            {
                JoinRoom(room);
            }, 80, MModUI.ModernColors.Primary, 35, 14);
        }
        else
        {
            _mainUI.CreateModernButton("FullBtn", buttonRow.transform, "已满", null, 80, 
                MModUI.ModernColors.TextTertiary, 35, 14);
        }
    }
    
    private void JoinRoom(OnlineRoom room)
    {
        LoggerHelper.Log($"[OnlineLobby] 加入房间: {room.RoomName}, IsP2P: {room.IsP2P}");
        
        if (room.IsP2P)
        {
            var lobbyManager = _mainUI.LobbyManager;
            if (lobbyManager != null)
            {
                var lobbyId = new CSteamID(room.P2PLobbyId);
                var password = room.HasPassword ? _mainUI._onlineJoinPassword : "";
                if (room.HasPassword)
                {
                    lobbyManager.TryJoinLobbyWithPassword(lobbyId, password, out var error);
                    LoggerHelper.Log($"[OnlineLobby] 加入Steam P2P房间: {lobbyId}, 密码: {password}, 错误: {error}");
                }
                else
                {
                    lobbyManager.JoinLobby(lobbyId);
                    LoggerHelper.Log($"[OnlineLobby] 加入Steam P2P房间: {lobbyId}");
                }
            }
            else
            {
                LoggerHelper.LogError("[OnlineLobby] LobbyManager为null，无法加入P2P房间");
            }
        }
        else
        {
            if (room.HasPassword && !string.IsNullOrEmpty(_mainUI._onlineJoinPassword))
            {
                LoggerHelper.Log($"[OnlineLobby] 使用密码加入在线房间: {_mainUI._onlineJoinPassword}");
            }
            _relayManager.JoinRoom(room);
        }
    }
    
    private void OnNodeSelected(RelayNode node)
    {
        if (node != null)
        {
            LoggerHelper.Log($"[OnlineLobby] 节点已选择: {node.NodeName}");
        }
    }
    
    private void OnError(string error)
    {
        LoggerHelper.LogError($"[OnlineLobby] 错误: {error}");
        CharacterMainControl.Main?.PopText(error, 3f);
    }
    
    private GameObject CreateScrollArea(Transform parent, string name, float height)
    {
        var scrollArea = new GameObject(name);
        scrollArea.transform.SetParent(parent, false);
        
        var scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;
        
        var rectTransform = scrollArea.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(0, height);
        
        var layoutElement = scrollArea.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1;
        layoutElement.preferredHeight = height;
        
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        var viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        
        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        
        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 10;
        contentLayout.padding = new RectOffset(10, 10, 10, 10);
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlHeight = false;
        
        var contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        
        return scrollArea;
    }
    
    private IEnumerator NodeStatusUpdateLoop()
    {
        while (_isNodeSelectorOpen)
        {
            yield return new WaitForSeconds(NODE_PING_INTERVAL);
            
            if (_relayManager != null && _isNodeSelectorOpen)
            {
                StartCoroutine(_relayManager.PingAllNodesCoroutine());
            }
        }
    }
    
    private void OnDestroy()
    {
        if (_nodeStatusUpdateCoroutine != null)
        {
            StopCoroutine(_nodeStatusUpdateCoroutine);
            _nodeStatusUpdateCoroutine = null;
        }
        
        if (_relayManager != null)
        {
            _relayManager.OnRoomListUpdated -= OnRoomListUpdated;
            _relayManager.OnNodeSelected -= OnNodeSelected;
            _relayManager.OnError -= OnError;
        }
        
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
