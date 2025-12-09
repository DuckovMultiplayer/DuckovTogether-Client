using System;
using System.Collections;
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
    private GameObject _connectPanel;
    private GameObject _statusPanel;
    private GameObject _playerListPanel;
    
    private TMP_InputField _ipInput;
    private TMP_InputField _portInput;
    private TMP_Text _statusText;
    private TMP_Text _connectionText;
    private Transform _playerListContent;
    
    private readonly Dictionary<string, GameObject> _playerEntries = new();
    
    public bool IsVisible { get; private set; } = true;
    public KeyCode ToggleKey { get; set; } = KeyCode.F1;
    
    private ModBehaviourF Service => ModBehaviourF.Instance;
    private bool IsConnected => Service?.networkStarted ?? false;
    
    private string _serverIP = "127.0.0.1";
    private string _serverPort = "9050";

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
        
        UpdateUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            ToggleVisibility();
        }
        
        UpdateConnectionStatus();
        UpdatePlayerList();
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
        _mainPanel = CreatePanel("MainPanel", _canvas.transform, new Vector2(400, 500), new Vector2(220, 300));
        
        var layout = _mainPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 15;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        
        CreateHeader();
        CreateConnectionPanel();
        CreateStatusPanel();
        CreatePlayerListPanel();
        CreateFooter();
    }

    private void CreateHeader()
    {
        var header = CreateContainer("Header", _mainPanel.transform, 50);
        var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childForceExpandWidth = false;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.spacing = 10;
        
        CreateLabel("Title", header.transform, "Duckov Together", 24, FontStyles.Bold, UIColors.Text);
        
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(header.transform, false);
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1;
        
        CreateLabel("Version", header.transform, "v1.0", 14, FontStyles.Normal, UIColors.TextSecondary);
    }

    private void CreateConnectionPanel()
    {
        _connectPanel = CreateCard("ConnectPanel", _mainPanel.transform, 180);
        
        var layout = _connectPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 15, 15);
        layout.spacing = 10;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        
        CreateLabel("ConnectTitle", _connectPanel.transform, "Server Connection", 18, FontStyles.Bold, UIColors.Text);
        
        var ipRow = CreateInputRow(_connectPanel.transform, "IP Address", _serverIP, out _ipInput);
        _ipInput.onValueChanged.AddListener(v => _serverIP = v);
        
        var portRow = CreateInputRow(_connectPanel.transform, "Port", _serverPort, out _portInput);
        _portInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        _portInput.onValueChanged.AddListener(v => _serverPort = v);
        
        var buttonRow = CreateContainer("ButtonRow", _connectPanel.transform, 45);
        var buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 10;
        buttonLayout.childForceExpandWidth = true;
        
        CreateButton("ConnectBtn", buttonRow.transform, "Connect", UIColors.Primary, OnConnectClick);
        CreateButton("DisconnectBtn", buttonRow.transform, "Disconnect", UIColors.Error, OnDisconnectClick);
    }

    private void CreateStatusPanel()
    {
        _statusPanel = CreateCard("StatusPanel", _mainPanel.transform, 80);
        
        var layout = _statusPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 10, 10);
        layout.spacing = 5;
        layout.childForceExpandWidth = true;
        
        _connectionText = CreateLabel("ConnectionStatus", _statusPanel.transform, "Disconnected", 16, FontStyles.Bold, UIColors.Error);
        _statusText = CreateLabel("StatusMessage", _statusPanel.transform, "Press F1 to toggle UI", 12, FontStyles.Normal, UIColors.TextSecondary);
    }

    private void CreatePlayerListPanel()
    {
        _playerListPanel = CreateCard("PlayerListPanel", _mainPanel.transform, 120);
        
        var layout = _playerListPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 10, 10);
        layout.spacing = 5;
        layout.childForceExpandHeight = false;
        
        CreateLabel("PlayersTitle", _playerListPanel.transform, "Players Online", 14, FontStyles.Bold, UIColors.Text);
        
        var scrollView = CreateScrollView("PlayerScroll", _playerListPanel.transform, 80);
        _playerListContent = scrollView.transform.Find("Viewport/Content");
    }

    private void CreateFooter()
    {
        var footer = CreateContainer("Footer", _mainPanel.transform, 30);
        var footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        
        CreateLabel("FooterText", footer.transform, "Duckov Team - Headless Server Mode", 11, FontStyles.Italic, UIColors.TextSecondary);
    }

    private void OnConnectClick()
    {
        if (string.IsNullOrEmpty(_serverIP) || string.IsNullOrEmpty(_serverPort))
        {
            SetStatus("Please enter server IP and port", UIColors.Warning);
            return;
        }
        
        if (!int.TryParse(_serverPort, out int port))
        {
            SetStatus("Invalid port number", UIColors.Error);
            return;
        }
        
        if (Service == null)
        {
            SetStatus("Service not ready", UIColors.Error);
            return;
        }
        
        Service.manualIP = _serverIP;
        Service.manualPort = _serverPort;
        Service.StartNetwork(false);
        Service.ConnectToHost(_serverIP, port);
        
        SetStatus($"Connecting to {_serverIP}:{port}...", UIColors.Warning);
    }

    private void OnDisconnectClick()
    {
        if (Service == null) return;
        
        Service.StopNetwork();
        SetStatus("Disconnected", UIColors.TextSecondary);
    }

    private void UpdateConnectionStatus()
    {
        if (_connectionText == null) return;
        
        if (IsConnected)
        {
            _connectionText.text = "Connected";
            _connectionText.color = UIColors.Success;
        }
        else
        {
            _connectionText.text = "Disconnected";
            _connectionText.color = UIColors.Error;
        }
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
                    var entry = CreatePlayerEntry(kvp.Key, kvp.Value?.PlayerName ?? "Unknown");
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

    private GameObject CreatePlayerEntry(string id, string name)
    {
        var entry = new GameObject($"Player_{id}");
        entry.transform.SetParent(_playerListContent, false);
        
        var layout = entry.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 2, 2);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleLeft;
        
        var layoutElement = entry.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 25;
        
        var bg = entry.AddComponent<Image>();
        bg.color = UIColors.CardBg;
        
        CreateLabel("Name", entry.transform, name, 13, FontStyles.Normal, UIColors.Text);
        
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

    private void UpdateUI()
    {
        if (_ipInput != null) _ipInput.text = _serverIP;
        if (_portInput != null) _portInput.text = _serverPort;
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
        
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = UIColors.Border;
        outline.effectDistance = new Vector2(1, -1);
        
        return panel;
    }

    private GameObject CreateCard(string name, Transform parent, float height)
    {
        var card = new GameObject(name);
        card.transform.SetParent(parent, false);
        
        var layoutElement = card.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        
        var bg = card.AddComponent<Image>();
        bg.color = UIColors.CardBg;
        
        return card;
    }

    private GameObject CreateContainer(string name, Transform parent, float height)
    {
        var container = new GameObject(name);
        container.transform.SetParent(parent, false);
        
        var layoutElement = container.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        
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

    private GameObject CreateInputRow(Transform parent, string label, string defaultValue, out TMP_InputField inputField)
    {
        var row = new GameObject($"{label}Row");
        row.transform.SetParent(parent, false);
        
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        
        var rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 35;
        
        var labelText = CreateLabel($"{label}Label", row.transform, label, 14, FontStyles.Normal, UIColors.TextSecondary);
        labelText.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;
        
        var inputGO = new GameObject($"{label}Input");
        inputGO.transform.SetParent(row.transform, false);
        
        var inputLayout = inputGO.AddComponent<LayoutElement>();
        inputLayout.flexibleWidth = 1;
        inputLayout.preferredHeight = 30;
        
        var inputBg = inputGO.AddComponent<Image>();
        inputBg.color = UIColors.InputBg;
        
        var textArea = new GameObject("TextArea");
        textArea.transform.SetParent(inputGO.transform, false);
        var textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.sizeDelta = new Vector2(-10, 0);
        textAreaRect.anchoredPosition = Vector2.zero;
        
        var textComponent = new GameObject("Text");
        textComponent.transform.SetParent(textArea.transform, false);
        var textRect = textComponent.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        var tmp = textComponent.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 14;
        tmp.color = UIColors.Text;
        tmp.alignment = TextAlignmentOptions.Left;
        
        inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.textComponent = tmp;
        inputField.textViewport = textAreaRect;
        inputField.text = defaultValue;
        
        return row;
    }

    private void CreateButton(string name, Transform parent, string text, Color bgColor, Action onClick)
    {
        var buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent, false);
        
        var layoutElement = buttonGO.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 40;
        layoutElement.flexibleWidth = 1;
        
        var bg = buttonGO.AddComponent<Image>();
        bg.color = bgColor;
        
        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = bg;
        
        var colors = button.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.1f;
        colors.pressedColor = bgColor * 0.9f;
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
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private GameObject CreateScrollView(string name, Transform parent, float height)
    {
        var scrollGO = new GameObject(name);
        scrollGO.transform.SetParent(parent, false);
        
        var scrollLayout = scrollGO.AddComponent<LayoutElement>();
        scrollLayout.preferredHeight = height;
        scrollLayout.flexibleWidth = 1;
        
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        
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
        contentLayout.spacing = 5;
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
        public static readonly Color PanelBg = new Color(0.12f, 0.12f, 0.14f, 0.95f);
        public static readonly Color CardBg = new Color(0.18f, 0.18f, 0.20f, 1f);
        public static readonly Color InputBg = new Color(0.22f, 0.22f, 0.24f, 1f);
        public static readonly Color Border = new Color(0.3f, 0.3f, 0.32f, 1f);
        
        public static readonly Color Text = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color TextSecondary = new Color(0.7f, 0.7f, 0.7f, 1f);
        
        public static readonly Color Primary = new Color(0.2f, 0.6f, 0.9f, 1f);
        public static readonly Color Success = new Color(0.3f, 0.8f, 0.4f, 1f);
        public static readonly Color Warning = new Color(0.9f, 0.7f, 0.2f, 1f);
        public static readonly Color Error = new Color(0.9f, 0.3f, 0.3f, 1f);
    }
}
