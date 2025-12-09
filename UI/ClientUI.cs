using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EscapeFromDuckovCoopMod.UI;

public class ClientUI : MonoBehaviour
{
    public static ClientUI Instance { get; private set; }
    
    private Canvas _canvas;
    private GameObject _mainPanel;
    private RectTransform _mainPanelRect;
    private TMP_InputField _ipInput;
    private TMP_InputField _portInput;
    private TMP_Text _statusText;
    private TMP_Text _connectionText;
    private TMP_Text _roomCountText;
    private Transform _serverListContent;
    private Transform _playerListContent;
    private Button _connectBtn;
    private Button _disconnectBtn;
    
    private readonly Dictionary<string, GameObject> _serverEntries = new();
    private readonly Dictionary<string, GameObject> _playerEntries = new();
    private readonly List<SavedServer> _savedServers = new();
    
    public bool IsVisible { get; private set; } = true;
    public KeyCode ToggleKey { get; set; } = KeyCode.F1;
    
    private ModBehaviourF Service => ModBehaviourF.Instance;
    private Net.CoopNetClient Client => Net.CoopNetClient.Instance;
    private bool IsConnected => Client?.IsConnected ?? false;
    
    private string _serverIP = "127.0.0.1";
    private string _serverPort = "9050";
    private string _connectedServerKey = null;
    
    [Serializable]
    public class SavedServer
    {
        public string IP;
        public int Port;
        public string Name;
        public int Ping;
        public bool IsOnline;
        public int PlayerCount;
        public int MaxPlayers;
        public int PluginCount;
        public string Icon;
        public string Key => $"{IP}:{Port}";
    }

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

    public void Initialize()
    {
        CreateEventSystem();
        CreateCanvas();
        CreateMainPanel();
        
        if (Service != null)
        {
            _serverIP = Service.manualIP ?? _serverIP;
            _serverPort = Service.manualPort ?? _serverPort;
        }
        
        UpdateInputFields();
        
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            ToggleVisibility();
        }
        
        UpdateConnectionStatus();
        UpdatePlayerList();
        UpdateButtonStates();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ToggleVisibility()
    {
        IsVisible = !IsVisible;
        if (_mainPanel != null)
        {
            _mainPanel.SetActive(IsVisible);
        }
    }

    private void CreateEventSystem()
    {
        if (EventSystem.current != null) return;
        
        var eventSystemGO = new GameObject("ClientUI_EventSystem");
        DontDestroyOnLoad(eventSystemGO);
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<StandaloneInputModule>();
    }

    private void CreateCanvas()
    {
        var canvasGO = new GameObject("ClientUI_Canvas");
        canvasGO.transform.SetParent(transform);
        
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1000;
        
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasGO.AddComponent<GraphicRaycaster>();
    }

    private void CreateMainPanel()
    {
        _mainPanel = CreatePanel("MainPanel", _canvas.transform, new Vector2(950, 650), new Vector2(100, 80));
        _mainPanelRect = _mainPanel.GetComponent<RectTransform>();
        
        var dragHandler = _mainPanel.AddComponent<UIDragHandler>();
        dragHandler.Target = _mainPanelRect;
        
        var mainLayout = _mainPanel.AddComponent<VerticalLayoutGroup>();
        mainLayout.padding = new RectOffset(0, 0, 0, 0);
        mainLayout.spacing = 0;
        mainLayout.childForceExpandWidth = true;
        mainLayout.childForceExpandHeight = false;
        mainLayout.childControlHeight = true;
        
        CreateTitleBar();
        CreateContentArea();
        CreateFooter();
    }

    private void CreateTitleBar()
    {
        var titleBar = CreateContainer("TitleBar", _mainPanel.transform, 50);
        var bg = titleBar.AddComponent<Image>();
        bg.color = UIColors.TitleBg;
        
        var layout = titleBar.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 15, 10, 10);
        layout.spacing = 15;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        
        CreateLabel("Title", titleBar.transform, "Duckov Together", 24, FontStyles.Bold, UIColors.Text);
        
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(titleBar.transform, false);
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1;
        
        _connectionText = CreateLabel("Status", titleBar.transform, L("ui.status.disconnected"), 14, FontStyles.Normal, UIColors.Error);
        
        CreateButton("MinBtn", titleBar.transform, "_", UIColors.TextSecondary, () => ToggleVisibility(), 35, 30);
        CreateButton("CloseBtn", titleBar.transform, "×", UIColors.Error, () => ToggleVisibility(), 35, 30);
    }


    private void CreateContentArea()
    {
        var content = CreateContainer("Content", _mainPanel.transform, 500);
        content.AddComponent<LayoutElement>().flexibleHeight = 1;
        
        var layout = content.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 15, 15);
        layout.spacing = 15;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        
        CreateLeftPanel(content.transform);
        CreateRightPanel(content.transform);
    }

    private void CreateLeftPanel(Transform parent)
    {
        var leftPanel = CreateContainer("LeftPanel", parent, 0);
        leftPanel.AddComponent<LayoutElement>().preferredWidth = 540;
        
        var layout = leftPanel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        
        var headerCard = CreateCard("ServerListHeader", leftPanel.transform, 50);
        var headerLayout = headerCard.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = new RectOffset(15, 15, 10, 10);
        headerLayout.spacing = 10;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childForceExpandWidth = false;
        
        CreateLabel("Title", headerCard.transform, L("ui.serverList.title"), 16, FontStyles.Bold, UIColors.Text);
        _roomCountText = CreateLabel("Count", headerCard.transform, L("ui.serverList.count", 0), 12, FontStyles.Normal, UIColors.TextSecondary);
        
        var headerSpacer = new GameObject("Spacer");
        headerSpacer.transform.SetParent(headerCard.transform, false);
        headerSpacer.AddComponent<LayoutElement>().flexibleWidth = 1;
        
        CreateButton("RefreshBtn", headerCard.transform, L("ui.button.refresh"), UIColors.Primary, OnRefreshClick, 80, 30, 13);
        
        var scrollView = CreateScrollView("ServerListScroll", leftPanel.transform, 350);
        _serverListContent = scrollView.transform.Find("Viewport/Content");
        
        CreateEmptyServerListHint();
    }

    private void CreateEmptyServerListHint()
    {
        var hint = new GameObject("EmptyHint");
        hint.transform.SetParent(_serverListContent, false);
        
        hint.AddComponent<LayoutElement>().preferredHeight = 100;
        
        var tmp = hint.AddComponent<TextMeshProUGUI>();
        tmp.text = L("ui.serverList.empty");
        tmp.fontSize = 14;
        tmp.color = UIColors.TextSecondary;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Italic;
    }

    private void CreateRightPanel(Transform parent)
    {
        var rightPanel = CreateContainer("RightPanel", parent, 0);
        rightPanel.AddComponent<LayoutElement>().flexibleWidth = 1;
        
        var layout = rightPanel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        
        CreateConnectCard(rightPanel.transform);
        CreatePlayerListCard(rightPanel.transform);
        CreateQuickActionsCard(rightPanel.transform);
    }

    private void CreateConnectCard(Transform parent)
    {
        var card = CreateCard("ConnectCard", parent, 165);
        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 12, 12);
        layout.spacing = 8;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        
        CreateLabel("Title", card.transform, L("ui.addServer.title"), 15, FontStyles.Bold, UIColors.Text);
        
        var ipRow = CreateInputRow(card.transform, L("ui.connect.ip"), _serverIP, out _ipInput, 75);
        _ipInput.onValueChanged.AddListener(v => _serverIP = v);
        
        var portRow = CreateInputRow(card.transform, L("ui.connect.port"), _serverPort, out _portInput, 75);
        _portInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        _portInput.onValueChanged.AddListener(v => _serverPort = v);
        
        var buttonRow = CreateContainer("Buttons", card.transform, 42);
        var buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 10;
        buttonLayout.childForceExpandWidth = true;
        
        CreateButton("AddServerBtn", buttonRow.transform, L("ui.button.addServer"), UIColors.Primary, OnAddServerClick, -1, 38, 14);
        _disconnectBtn = CreateButtonWithRef("DisconnectBtn", buttonRow.transform, L("ui.button.disconnect"), UIColors.Error, OnDisconnectClick, -1, 38, 14);
    }

    private void CreatePlayerListCard(Transform parent)
    {
        var card = CreateCard("PlayerListCard", parent, 0);
        card.AddComponent<LayoutElement>().flexibleHeight = 1;
        
        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 10, 10);
        layout.spacing = 5;
        layout.childForceExpandHeight = false;
        
        CreateLabel("Title", card.transform, L("ui.players.title"), 14, FontStyles.Bold, UIColors.Text);
        
        var scrollView = CreateScrollView("PlayerScroll", card.transform, 120);
        _playerListContent = scrollView.transform.Find("Viewport/Content");
    }

    private void CreateQuickActionsCard(Transform parent)
    {
        var card = CreateCard("QuickActionsCard", parent, 85);
        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 10, 10);
        layout.spacing = 8;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        
        CreateLabel("Title", card.transform, L("ui.actions.quickActions"), 14, FontStyles.Bold, UIColors.Text);
        
        var buttonRow = CreateContainer("ActionButtons", card.transform, 35);
        var buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 8;
        buttonLayout.childForceExpandWidth = true;
        
        CreateButton("VoiceBtn", buttonRow.transform, L("ui.voice.title"), UIColors.Info, OnVoiceSettings, -1, 32, 12);
        CreateButton("SettingsBtn", buttonRow.transform, L("ui.settings.title"), UIColors.Secondary, OnSettings, -1, 32, 12);
    }

    private void CreateFooter()
    {
        var footer = CreateContainer("Footer", _mainPanel.transform, 35);
        var bg = footer.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.10f, 1f);
        
        var layout = footer.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 8, 8);
        layout.childAlignment = TextAnchor.MiddleLeft;
        
        _statusText = CreateLabel("Status", footer.transform, L("ui.hint.pressKey", ToggleKey.ToString()), 12, FontStyles.Normal, UIColors.TextSecondary);
        
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(footer.transform, false);
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1;
        
        CreateLabel("Version", footer.transform, "v1.0.0", 11, FontStyles.Italic, new Color(0.5f, 0.5f, 0.5f, 0.8f));
    }

    private void OnRefreshClick()
    {
        RefreshAllServers();
    }

    private void OnAddServerClick()
    {
        if (string.IsNullOrEmpty(_serverIP) || string.IsNullOrEmpty(_serverPort))
        {
            SetStatus(L("ui.error.emptyInput"), UIColors.Error);
            return;
        }
        
        if (!int.TryParse(_serverPort, out int port))
        {
            SetStatus(L("ui.error.invalidPort"), UIColors.Error);
            return;
        }
        
        var key = $"{_serverIP}:{port}";
        if (_savedServers.Exists(s => s.Key == key))
        {
            SetStatus(L("ui.error.serverExists"), UIColors.Warning);
            return;
        }
        
        var server = new SavedServer
        {
            IP = _serverIP,
            Port = port,
            Name = key,
            IsOnline = false,
            Ping = -1
        };
        
        _savedServers.Add(server);
        CreateServerEntry(server);
        UpdateServerCount();
        SetStatus(L("ui.status.serverAdded"), UIColors.Success);
        
        CheckServerStatus(server);
    }
    
    private void RefreshAllServers()
    {
        SetStatus(L("ui.status.refreshing"), UIColors.Warning);
        foreach (var server in _savedServers)
        {
            CheckServerStatus(server);
        }
    }
    
    private void CheckServerStatus(SavedServer server)
    {
        StartCoroutine(CheckServerStatusCoroutine(server));
    }
    
    private System.Collections.IEnumerator CheckServerStatusCoroutine(SavedServer server)
    {
        server.IsOnline = false;
        server.Ping = -1;
        UpdateServerEntryUI(server);
        
        var checkTask = new System.Threading.Tasks.Task(() => CheckServerStatusAsync(server));
        checkTask.Start();
        
        while (!checkTask.IsCompleted)
        {
            yield return null;
        }
        
        UpdateServerEntryUI(server);
    }
    
    private class DiscoveryListener : LiteNetLib.INetEventListener
    {
        public SavedServer Server;
        public bool Received;
        public System.Diagnostics.Stopwatch Timer;
        
        public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint remoteEndPoint, LiteNetLib.NetPacketReader reader, LiteNetLib.UnconnectedMessageType messageType)
        {
            try
            {
                var responseType = reader.GetString();
                if (responseType == "DISCOVER_RESPONSE")
                {
                    Server.Name = reader.GetString();
                    Server.PlayerCount = reader.GetInt();
                    Server.MaxPlayers = reader.GetInt();
                    Server.PluginCount = reader.TryGetInt(out var pluginCount) ? pluginCount : 0;
                    Server.Icon = reader.TryGetString(out var icon) ? icon : "default";
                    Server.Ping = (int)Timer.ElapsedMilliseconds;
                    Server.IsOnline = true;
                    Received = true;
                }
            }
            catch { }
        }
        
        public void OnPeerConnected(LiteNetLib.NetPeer peer) { }
        public void OnPeerDisconnected(LiteNetLib.NetPeer peer, LiteNetLib.DisconnectInfo disconnectInfo) { }
        public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError) { }
        public void OnNetworkReceive(LiteNetLib.NetPeer peer, LiteNetLib.NetPacketReader reader, byte channelNumber, LiteNetLib.DeliveryMethod deliveryMethod) { }
        public void OnNetworkLatencyUpdate(LiteNetLib.NetPeer peer, int latency) { }
        public void OnConnectionRequest(LiteNetLib.ConnectionRequest request) { }
    }
    
    private void CheckServerStatusAsync(SavedServer server)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var listener = new DiscoveryListener { Server = server, Timer = timer };
        LiteNetLib.NetManager netManager = null;
        
        try
        {
            netManager = new LiteNetLib.NetManager(listener)
            {
                UnconnectedMessagesEnabled = true
            };
            
            if (!netManager.Start()) return;
            
            var writer = new LiteNetLib.Utils.NetDataWriter();
            writer.Put("DISCOVER_REQUEST");
            
            var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(server.IP), server.Port);
            netManager.SendUnconnectedMessage(writer, endpoint);
            
            var timeout = 2000;
            while (timer.ElapsedMilliseconds < timeout && !listener.Received)
            {
                netManager.PollEvents();
                System.Threading.Thread.Sleep(10);
            }
        }
        catch
        {
            server.IsOnline = false;
        }
        finally
        {
            netManager?.Stop();
        }
    }
    
    private void CreateServerEntry(SavedServer server)
    {
        var entry = new GameObject($"Server_{server.Key}");
        entry.transform.SetParent(_serverListContent, false);
        
        var entryLayout = entry.AddComponent<LayoutElement>();
        entryLayout.preferredHeight = 65;
        
        var bg = entry.AddComponent<Image>();
        bg.color = UIColors.CardBg;
        
        var layout = entry.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 6, 6);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        
        var iconContainer = new GameObject("Icon");
        iconContainer.transform.SetParent(entry.transform, false);
        var iconLE = iconContainer.AddComponent<LayoutElement>();
        iconLE.preferredWidth = 45;
        iconLE.preferredHeight = 45;
        var iconBg = iconContainer.AddComponent<Image>();
        iconBg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
        var iconText = CreateLabel("IconText", iconContainer.transform, "S", 20, FontStyles.Bold, UIColors.Text);
        iconText.alignment = TextAlignmentOptions.Center;
        var iconTextRect = iconText.GetComponent<RectTransform>();
        iconTextRect.anchorMin = Vector2.zero;
        iconTextRect.anchorMax = Vector2.one;
        iconTextRect.sizeDelta = Vector2.zero;
        
        var infoContainer = new GameObject("Info");
        infoContainer.transform.SetParent(entry.transform, false);
        var infoLayout = infoContainer.AddComponent<VerticalLayoutGroup>();
        infoLayout.spacing = 1;
        infoLayout.childForceExpandHeight = false;
        infoContainer.AddComponent<LayoutElement>().flexibleWidth = 1;
        
        CreateLabel("Name", infoContainer.transform, server.Name, 13, FontStyles.Bold, UIColors.Text);
        CreateLabel("Address", infoContainer.transform, server.Key, 10, FontStyles.Normal, UIColors.TextSecondary);
        var detailText = server.IsOnline ? $"{server.PlayerCount}/{server.MaxPlayers} | {L("ui.server.plugins")}: {server.PluginCount}" : L("ui.status.offline");
        CreateLabel("Details", infoContainer.transform, detailText, 10, FontStyles.Normal, server.IsOnline ? UIColors.Primary : UIColors.Error);
        
        var pingLabel = CreateLabel("Ping", entry.transform, server.IsOnline && server.Ping >= 0 ? $"{server.Ping}ms" : "-", 11, FontStyles.Normal, GetPingColor(server.Ping));
        pingLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;
        
        var joinBtn = CreateButtonWithRef("JoinBtn", entry.transform, L("ui.button.connect"), server.IsOnline ? UIColors.Success : UIColors.Secondary, () => OnConnectToServer(server), 60, 32, 11);
        joinBtn.interactable = server.IsOnline;
        
        var deleteBtn = CreateButtonWithRef("DeleteBtn", entry.transform, "×", UIColors.Error, () => OnDeleteServer(server), 30, 32, 13);
        
        _serverEntries[server.Key] = entry;
        
        var emptyHint = _serverListContent.Find("EmptyHint");
        if (emptyHint != null) emptyHint.gameObject.SetActive(false);
    }
    
    private Color GetPingColor(int ping)
    {
        if (ping < 0) return UIColors.TextSecondary;
        if (ping < 50) return UIColors.Success;
        if (ping < 100) return UIColors.Warning;
        return UIColors.Error;
    }
    
    private void UpdateServerEntryUI(SavedServer server)
    {
        if (!_serverEntries.TryGetValue(server.Key, out var entry)) return;
        
        var nameLabel = entry.transform.Find("Info/Name")?.GetComponent<TMP_Text>();
        if (nameLabel != null) nameLabel.text = server.Name;
        
        var detailsLabel = entry.transform.Find("Info/Details")?.GetComponent<TMP_Text>();
        if (detailsLabel != null)
        {
            detailsLabel.text = server.IsOnline ? $"{server.PlayerCount}/{server.MaxPlayers} | {L("ui.server.plugins")}: {server.PluginCount}" : L("ui.status.offline");
            detailsLabel.color = server.IsOnline ? UIColors.Primary : UIColors.Error;
        }
        
        var pingLabel = entry.transform.Find("Ping")?.GetComponent<TMP_Text>();
        if (pingLabel != null)
        {
            pingLabel.text = server.IsOnline && server.Ping >= 0 ? $"{server.Ping}ms" : "-";
            pingLabel.color = GetPingColor(server.Ping);
        }
        
        var joinBtn = entry.transform.Find("JoinBtn")?.GetComponent<Button>();
        if (joinBtn != null)
        {
            joinBtn.interactable = server.IsOnline;
            var btnColors = joinBtn.colors;
            btnColors.normalColor = server.IsOnline ? UIColors.Success : UIColors.Secondary;
            joinBtn.colors = btnColors;
        }
    }
    
    private void OnConnectToServer(SavedServer server)
    {
        if (Client == null)
        {
            SetStatus(L("ui.error.serviceNotReady"), UIColors.Error);
            return;
        }
        
        _connectedServerKey = server.Key;
        Client.Connect(server.IP, server.Port);
        SetStatus(L("ui.status.connecting"), UIColors.Warning);
    }
    
    private void OnDeleteServer(SavedServer server)
    {
        _savedServers.Remove(server);
        if (_serverEntries.TryGetValue(server.Key, out var entry))
        {
            Destroy(entry);
            _serverEntries.Remove(server.Key);
        }
        UpdateServerCount();
        
        if (_savedServers.Count == 0)
        {
            var emptyHint = _serverListContent.Find("EmptyHint");
            if (emptyHint != null) emptyHint.gameObject.SetActive(true);
        }
    }
    
    private void UpdateServerCount()
    {
        if (_roomCountText != null)
            _roomCountText.text = L("ui.serverList.count", _savedServers.Count);
    }

    private void OnDisconnectClick()
    {
        if (Service == null) return;
        
        Service.StopNetwork();
        SetStatus(L("ui.status.disconnected"), UIColors.TextSecondary);
    }

    private void OnVoiceSettings()
    {
        SetStatus(L("ui.voice.title"), UIColors.Info);
    }

    private void OnSettings()
    {
        SetStatus(L("ui.settings.title"), UIColors.Info);
    }


    private void UpdateConnectionStatus()
    {
        if (_connectionText == null) return;
        
        if (IsConnected)
        {
            _connectionText.text = L("ui.status.connected");
            _connectionText.color = UIColors.Success;
        }
        else
        {
            _connectionText.text = L("ui.status.disconnected");
            _connectionText.color = UIColors.Error;
        }
    }

    private void UpdateButtonStates()
    {
        if (_connectBtn != null) _connectBtn.interactable = !IsConnected;
        if (_disconnectBtn != null) _disconnectBtn.interactable = IsConnected;
    }

    private void UpdatePlayerList()
    {
        if (_playerListContent == null || Service == null) return;
        
        var currentPlayers = new HashSet<string>();
        
        if (Service.clientPlayerStatuses != null)
        {
            foreach (var kvp in Service.clientPlayerStatuses)
            {
                currentPlayers.Add(kvp.Key);
                
                if (!_playerEntries.ContainsKey(kvp.Key))
                {
                    bool isLocal = Service.IsSelfId(kvp.Key);
                    var displayName = kvp.Value?.PlayerName ?? "Unknown";
                    if (isLocal) displayName += $" {L("ui.players.you")}";
                    var entry = CreatePlayerEntry(kvp.Key, displayName, isLocal);
                    _playerEntries[kvp.Key] = entry;
                }
            }
        }
        
        var toRemove = new List<string>();
        foreach (var kvp in _playerEntries)
        {
            if (!currentPlayers.Contains(kvp.Key))
            {
                toRemove.Add(kvp.Key);
                if (kvp.Value != null) Destroy(kvp.Value);
            }
        }
        foreach (var key in toRemove)
        {
            _playerEntries.Remove(key);
        }
    }

    private GameObject CreatePlayerEntry(string id, string name, bool isLocal)
    {
        var entry = new GameObject($"Player_{id}");
        entry.transform.SetParent(_playerListContent, false);
        
        var layout = entry.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 5, 5);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleLeft;
        
        var layoutElement = entry.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 28;
        
        var bg = entry.AddComponent<Image>();
        bg.color = isLocal ? new Color(0.2f, 0.35f, 0.25f, 1f) : UIColors.CardBg;
        
        CreateLabel("Name", entry.transform, name, 13, isLocal ? FontStyles.Bold : FontStyles.Normal, UIColors.Text);
        
        return entry;
    }

    public void SetStatus(string message, Color color)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
            _statusText.color = color;
        }
    }

    private void UpdateInputFields()
    {
        if (_ipInput != null) _ipInput.text = _serverIP;
        if (_portInput != null) _portInput.text = _serverPort;
    }

    private string L(string key, params object[] args)
    {
        return CoopLocalization.Get(key, args);
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        
        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(position.x, -position.y);
        
        var bg = panel.AddComponent<Image>();
        bg.color = UIColors.PanelBg;
        
        var shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(3, -3);
        
        return panel;
    }

    private GameObject CreateCard(string name, Transform parent, float height)
    {
        var card = new GameObject(name);
        card.transform.SetParent(parent, false);
        
        if (height > 0)
        {
            card.AddComponent<LayoutElement>().preferredHeight = height;
        }
        
        var bg = card.AddComponent<Image>();
        bg.color = UIColors.CardBg;
        
        return card;
    }

    private GameObject CreateContainer(string name, Transform parent, float height)
    {
        var container = new GameObject(name);
        container.transform.SetParent(parent, false);
        
        if (height > 0)
        {
            container.AddComponent<LayoutElement>().preferredHeight = height;
        }
        
        return container;
    }

    private TMP_Text CreateLabel(string name, Transform parent, string text, int fontSize, FontStyles style, Color color)
    {
        var labelGO = new GameObject(name);
        labelGO.transform.SetParent(parent, false);
        
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left;
        
        return tmp;
    }

    private GameObject CreateInputRow(Transform parent, string label, string defaultValue, out TMP_InputField inputField, float labelWidth = 80)
    {
        var row = new GameObject($"{label}Row");
        row.transform.SetParent(parent, false);
        
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        
        row.AddComponent<LayoutElement>().preferredHeight = 32;
        
        var labelText = CreateLabel("Label", row.transform, label, 13, FontStyles.Normal, UIColors.TextSecondary);
        labelText.gameObject.AddComponent<LayoutElement>().preferredWidth = labelWidth;
        
        var inputGO = new GameObject("Input");
        inputGO.transform.SetParent(row.transform, false);
        
        var inputLayout = inputGO.AddComponent<LayoutElement>();
        inputLayout.flexibleWidth = 1;
        inputLayout.preferredHeight = 28;
        
        var inputBg = inputGO.AddComponent<Image>();
        inputBg.color = UIColors.InputBg;
        
        var textArea = new GameObject("TextArea");
        textArea.transform.SetParent(inputGO.transform, false);
        var textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.sizeDelta = new Vector2(-16, 0);
        textAreaRect.anchoredPosition = Vector2.zero;
        
        var textComponent = new GameObject("Text");
        textComponent.transform.SetParent(textArea.transform, false);
        var textRect = textComponent.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        var tmp = textComponent.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 13;
        tmp.color = UIColors.Text;
        tmp.alignment = TextAlignmentOptions.Left;
        
        inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.textComponent = tmp;
        inputField.textViewport = textAreaRect;
        inputField.text = defaultValue;
        
        return row;
    }

    private void CreateButton(string name, Transform parent, string text, Color bgColor, Action onClick, float width = -1, float height = 40, int fontSize = 14)
    {
        CreateButtonWithRef(name, parent, text, bgColor, onClick, width, height, fontSize);
    }

    private Button CreateButtonWithRef(string name, Transform parent, string text, Color bgColor, Action onClick, float width = -1, float height = 40, int fontSize = 14)
    {
        var buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent, false);
        
        var layoutElement = buttonGO.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        if (width > 0) layoutElement.preferredWidth = width;
        else layoutElement.flexibleWidth = 1;
        
        var bg = buttonGO.AddComponent<Image>();
        bg.color = bgColor;
        
        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = bg;
        
        var colors = button.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.15f;
        colors.pressedColor = bgColor * 0.85f;
        colors.disabledColor = bgColor * 0.5f;
        button.colors = colors;
        
        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }
        
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(buttonGO.transform, false);
        
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        
        return button;
    }

    private GameObject CreateScrollView(string name, Transform parent, float height)
    {
        var scrollGO = new GameObject(name);
        scrollGO.transform.SetParent(parent, false);
        
        var scrollLayout = scrollGO.AddComponent<LayoutElement>();
        scrollLayout.preferredHeight = height;
        scrollLayout.flexibleWidth = 1;
        scrollLayout.flexibleHeight = 1;
        
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;
        
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGO.transform, false);
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
        contentLayout.spacing = 6;
        contentLayout.padding = new RectOffset(5, 5, 5, 5);
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        
        var contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        
        return scrollGO;
    }

    public static class UIColors
    {
        public static readonly Color PanelBg = new Color(0.10f, 0.10f, 0.12f, 0.98f);
        public static readonly Color TitleBg = new Color(0.07f, 0.07f, 0.09f, 1f);
        public static readonly Color CardBg = new Color(0.15f, 0.15f, 0.17f, 1f);
        public static readonly Color InputBg = new Color(0.20f, 0.20f, 0.22f, 1f);
        public static readonly Color Border = new Color(0.25f, 0.25f, 0.28f, 1f);
        
        public static readonly Color Text = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color TextSecondary = new Color(0.60f, 0.60f, 0.60f, 1f);
        
        public static readonly Color Primary = new Color(0.20f, 0.50f, 0.80f, 1f);
        public static readonly Color Secondary = new Color(0.40f, 0.40f, 0.45f, 1f);
        public static readonly Color Success = new Color(0.20f, 0.70f, 0.30f, 1f);
        public static readonly Color Warning = new Color(0.85f, 0.65f, 0.15f, 1f);
        public static readonly Color Error = new Color(0.80f, 0.25f, 0.25f, 1f);
        public static readonly Color Info = new Color(0.30f, 0.60f, 0.80f, 1f);
    }
}

public class UIDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform Target;
    private Vector2 _dragOffset;
    private Canvas _canvas;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Target == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            Target.parent as RectTransform, 
            eventData.position, 
            _canvas?.worldCamera, 
            out var localPoint);
        _dragOffset = Target.anchoredPosition - localPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Target == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            Target.parent as RectTransform, 
            eventData.position, 
            _canvas?.worldCamera, 
            out var localPoint);
        Target.anchoredPosition = localPoint + _dragOffset;
    }

    public void OnEndDrag(PointerEventData eventData) { }
}
