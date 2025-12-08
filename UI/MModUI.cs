















using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LeTai.Asset.TranslucentImage;
using Steamworks;
using RenderMode = UnityEngine.RenderMode;
using EscapeFromDuckovCoopMod.Utils.Logger.Tools;
using EscapeFromDuckovCoopMod.Net.Relay;

namespace EscapeFromDuckovCoopMod;

public class MModUI : MonoBehaviour
{
    public static MModUI Instance;

    
    private Canvas _canvas;
    private MModUIComponents _components;
    private MModUILayoutBuilder _layoutBuilder;

    internal Canvas Canvas => _canvas;

    private GameObject _hostEntryPrefab;
    private GameObject _playerEntryPrefab;
    
    
    private GameObject _cursorIndicator;

    public bool showUI = true;
    public bool showPlayerStatusWindow;
    public bool showVoiceSettingsWindow;
    
    
    private bool _isWaitingForHotkey = false;
    private KeyCode _currentVoiceHotkey = KeyCode.V;
    private float _inputVolume = 1.0f;
    private float _outputVolume = 1.0f;
    private bool _isVoiceMuted = false;
    private float _noiseReductionLevel = 0.5f;
    private bool _smartNoiseReduction = true;
    private Slider _noiseReductionSlider;

    public KeyCode toggleUIKey = KeyCode.Equals;  
    public KeyCode togglePlayerStatusKey = KeyCode.P;
    public readonly KeyCode readyKey = KeyCode.J;

    private readonly List<string> _hostList = new();
    private readonly HashSet<string> _hostSet = new();
    private string _manualIP = "192.168.123.1";
    private string _manualPort = "9050";
    private int _port = 9050;
    private string _status = "未连接";

    private readonly Dictionary<string, GameObject> _hostEntries = new();
    private readonly Dictionary<string, GameObject> _playerEntries = new();
    private readonly HashSet<string> _displayedPlayerIds = new();  
    private readonly Dictionary<string, TMP_Text> _playerPingTexts = new();  

    
    private readonly List<SteamLobbyManager.LobbyInfo> _steamLobbyInfos = new();
    private readonly HashSet<ulong> _displayedSteamLobbies = new();  
    private string _steamLobbyName = string.Empty;
    private string _steamLobbyPassword = string.Empty;
    private bool _steamLobbyFriendsOnly;
    private int _steamLobbyMaxPlayers = 2;
    private string _steamJoinPassword = string.Empty;
    internal string _onlineJoinPassword = string.Empty;

    
    private bool _lastVoteActive = false;
    private string _lastVoteSceneId = "";
    private bool _lastLocalReady = false;
    private readonly HashSet<string> _lastVoteParticipants = new();
    private float _lastVoteUpdateTime = 0f;

    
    public static class ModernColors
    {
        
        public static readonly Color Primary = new Color(0.30f, 0.69f, 0.31f, 1f);      
        public static readonly Color PrimaryHover = new Color(0.26f, 0.60f, 0.27f, 1f); 
        public static readonly Color PrimaryActive = new Color(0.22f, 0.52f, 0.23f, 1f); 

        
        public static readonly Color PrimaryText = new Color(1f, 1f, 1f, 0.95f);        

        
        public static readonly Color BgDark = new Color(0.23f, 0.23f, 0.23f, 1f);       
        public static readonly Color BgMedium = new Color(0.27f, 0.27f, 0.27f, 1f);     
        public static readonly Color BgLight = new Color(0.32f, 0.32f, 0.32f, 1f);      

        
        public static readonly Color TextPrimary = new Color(1f, 1f, 1f, 0.95f);        
        public static readonly Color TextSecondary = new Color(1f, 1f, 1f, 0.75f);      
        public static readonly Color TextTertiary = new Color(1f, 1f, 1f, 0.55f);       

        
        public static readonly Color Success = new Color(0.45f, 0.75f, 0.50f, 1f);      
        public static readonly Color Warning = new Color(0.90f, 0.75f, 0.35f, 1f);      
        public static readonly Color Error = new Color(0.85f, 0.45f, 0.40f, 1f);        
        public static readonly Color Info = new Color(0.55f, 0.65f, 0.80f, 1f);         

        
        public static readonly Color InputBg = new Color(0.33f, 0.33f, 0.33f, 1f);      
        public static readonly Color InputBorder = new Color(0.42f, 0.42f, 0.42f, 1f);  
        public static readonly Color InputFocus = PrimaryHover;

        
        public static readonly Color Divider = new Color(0.40f, 0.40f, 0.40f, 1f);      

        
        public static readonly Color GlassBg = new Color(0.30f, 0.30f, 0.30f, 0.55f);   

        
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.25f);             






    }

    public static class GlassTheme
    {
        public static readonly Color PanelBg = new Color(0.25f, 0.25f, 0.25f, 0.92f);
        public static readonly Color CardBg = new Color(0.28f, 0.28f, 0.28f, 0.9f);
        public static readonly Color ButtonBg = new Color(0.30f, 0.30f, 0.30f, 0.95f);
        public static readonly Color ButtonHover = new Color(0.35f, 0.35f, 0.35f, 0.97f);
        public static readonly Color ButtonActive = new Color(0.20f, 0.20f, 0.20f, 1f);
        public static readonly Color InputBg = new Color(0.33f, 0.33f, 0.33f, 0.9f);
        public static readonly Color Accent = new Color(0.6f, 0.8f, 0.9f, 1f);
        public static readonly Color Text = new Color(1f, 1f, 1f, 0.95f);
        public static readonly Color TextSecondary = new Color(1f, 1f, 1f, 0.8f);
        public static readonly Color Divider = new Color(1f, 1f, 1f, 0.08f);
    }

    // Mod instance
    private NetManager netManager => Mod?.netManager;
    private NetDataWriter writer => Mod?.writer;
    private NetPeer connectedPeer => Mod?.connectedPeer;
    private PlayerStatus localPlayerStatus => Mod?.localPlayerStatus;
    private bool networkStarted => Mod != null && Mod.networkStarted;

    private List<string> hostList => Mod?.hostList ?? _hostList;
    private HashSet<string> hostSet => Mod?.hostSet ?? _hostSet;

    private Dictionary<NetPeer, GameObject> remoteCharacters => Mod?.remoteCharacters;
    private Dictionary<NetPeer, PlayerStatus> playerStatuses => Mod?.playerStatuses;
    private Dictionary<string, GameObject> clientRemoteCharacters => Mod?.clientRemoteCharacters;
    private Dictionary<string, PlayerStatus> clientPlayerStatuses => Mod?.clientPlayerStatuses;

    // Steam lobby
    internal SteamLobbyManager LobbyManager => SteamLobbyManager.Instance;

    // Mod instance
    internal ModBehaviourF Mod => ModBehaviourF.Instance;
    [System.Obsolete("Client is never server. Headless server handles all server logic.")]
    internal bool IsServer => false;
    internal int port => _port;
    internal string status => _status;
    internal string manualIP
    {
        get => Mod?.manualIP ?? _manualIP;
        set
        {
            _manualIP = value;
            if (Mod != null) Mod.manualIP = value;
        }
    }
    internal string manualPort
    {
        get => Mod?.manualPort ?? _manualPort;
        set
        {
            _manualPort = value;
            if (Mod != null) Mod.manualPort = value;
        }
    }

    private void Update()
    {
        // Localization
        CoopLocalization.CheckLanguageChange();

        
        if (showUI)
        {
            
            
            if (_cursorIndicator != null)
            {
                _cursorIndicator.SetActive(true);
                _cursorIndicator.transform.position = Input.mousePosition;
            }
        }
        else
        {
            
            if (_cursorIndicator != null)
            {
                _cursorIndicator.SetActive(false);
            }
        }

        
        if (Input.GetKeyDown(toggleUIKey))
        {
            showUI = !showUI;
            if (_components?.MainPanel != null)
            {
                StartCoroutine(AnimatePanel(_components.MainPanel, showUI));
            }
        }

        
        if (Input.GetKeyDown(togglePlayerStatusKey))
        {
            showPlayerStatusWindow = !showPlayerStatusWindow;
            if (_components?.PlayerStatusPanel != null)
            {
                StartCoroutine(AnimatePanel(_components.PlayerStatusPanel, showPlayerStatusWindow));
            }
        }

        
        if (_components?.VoiceSettingsPanel != null && _components.VoiceSettingsPanel.activeSelf != showVoiceSettingsWindow)
        {
            StartCoroutine(AnimatePanel(_components.VoiceSettingsPanel, showVoiceSettingsWindow));
        }

        
        if (Input.GetKeyDown(KeyCode.F9))
        {
            LoggerHelper.Log("[MModUI] F9 按下 - 输出 PlayerInfoDatabase 调试信息");
            Utils.Database.PlayerInfoDatabase.Instance.DebugPrintDatabase();
        }

        
        if (Input.GetKeyDown(KeyCode.F10))
        {
            LoggerHelper.Log("[MModUI] F10 按下 - 测试 CustomData 功能");
            Utils.Database.PlayerInfoDatabase.Instance.DebugTestCustomData();
        }
        
        if (Input.GetKeyDown(KeyCode.F11))
        {
            LoggerHelper.Log("[MModUI] F11 按下 - 导出服务端数据");
            Utils.GameDataExporter.ExportAllData();
        }

        
        UpdateModeDisplay();

        
        UpdateConnectionStatus();

        
        UpdateVotePanel();

        
        UpdateSpectatorPanel();

        
        UpdateHostList();
        UpdatePlayerList();

        
        UpdateSteamLobbyList();

        
        UpdatePlayerPingDisplays();
    }

    
    internal IEnumerator AnimatePanel(GameObject panel, bool show)
    {
        if (show)
        {
            panel.SetActive(true);
            var canvasGroup = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;

            float time = 0;
            while (time < 0.2f)
            {
                time += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0, 1, time / 0.2f);
                yield return null;
            }
            canvasGroup.alpha = 1;
        }
        else
        {
            var canvasGroup = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();

            float time = 0;
            while (time < 0.15f)
            {
                time += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1, 0, time / 0.15f);
                yield return null;
            }
            panel.SetActive(false);
        }
    }

    public void Init()
    {
        Instance = this;

        
        _components = new MModUIComponents();
        _layoutBuilder = new MModUILayoutBuilder(this, _components);

        var mod = Mod;
        if (mod != null)
        {
            _manualIP = mod.manualIP;
            _manualPort = mod.manualPort;
            _status = mod.status;
            _port = mod.port;
            _hostList.Clear();
            _hostSet.Clear();
            _hostList.AddRange(mod.hostList);
            foreach (var host in mod.hostSet) _hostSet.Add(host);
        }

        
        if (LobbyManager != null)
        {
            LobbyManager.LobbyListUpdated -= OnLobbyListUpdated;
            LobbyManager.LobbyListUpdated += OnLobbyListUpdated;
            LobbyManager.LobbyJoined -= OnLobbyJoined;
            LobbyManager.LobbyJoined += OnLobbyJoined;
            _steamLobbyInfos.Clear();
            _steamLobbyInfos.AddRange(LobbyManager.AvailableLobbies);
        }

        CreateUI();
    }

    private void OnDestroy()
    {
        if (LobbyManager != null)
        {
            LobbyManager.LobbyListUpdated -= OnLobbyListUpdated;
        }
    }

    private void OnLobbyListUpdated(IReadOnlyList<SteamLobbyManager.LobbyInfo> lobbies)
    {
        _steamLobbyInfos.Clear();
        _steamLobbyInfos.AddRange(lobbies);
    }

    private void OnLobbyJoined()
    {
        LoggerHelper.Log("[MModUI] Lobby加入成功，强制刷新玩家列表");
        
        _displayedPlayerIds.Clear();
    }

    private void CreateUI()
    {
        
        if (EventSystem.current == null)
        {
            var eventSystemGO = new GameObject("EventSystem");
            DontDestroyOnLoad(eventSystemGO);
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();
        }

        
        InitializeBlurSource();

        
        var canvasGO = new GameObject("CoopModCanvas");
        DontDestroyOnLoad(canvasGO);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;

        var canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        
        CreateMainPanel();

        
        CreatePlayerStatusPanel();

        
        CreateVotePanel();

        
        CreateSpectatorPanel();
        
        
        CreateVoiceSettingsPanel();
        
        
        Main.UI.VersionLabel.Create();
        
        
        CreateCursorIndicator();
        
        StartCoroutine(InitializeOnlineLobbyDelayed());
    }
    
    private IEnumerator InitializeOnlineLobbyDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        
        LoggerHelper.Log($"[MModUI] 延迟初始化OnlineLobby开始，MainPanel active={_components?.MainPanel?.activeSelf}");
        
        if (OnlineLobbyUI.Instance != null)
        {
            LoggerHelper.Log("[MModUI] OnlineLobby实例已存在，直接使用");
            OnlineLobbyUI.Instance.Initialize(this, _components);
        }
        else
        {
            var lobbyGO = new GameObject("OnlineLobbyManager");
            lobbyGO.transform.SetParent(_canvas.transform, false);
            var lobby = lobbyGO.AddComponent<OnlineLobbyUI>();
            
            LoggerHelper.Log($"[MModUI] OnlineLobby组件已添加，准备Initialize");
            lobby.Initialize(this, _components);
        }
        
        LoggerHelper.Log($"[MModUI] OnlineLobby初始化完成，MainPanel active={_components?.MainPanel?.activeSelf}");
    }
    
    
    
    
    private void CreateCursorIndicator()
    {
        _cursorIndicator = new GameObject("CursorIndicator");
        _cursorIndicator.transform.SetParent(_canvas.transform, false);
        
        
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= radius)
                {
                    
                    float alpha = distance < radius - 1 ? 1f : (radius - distance);
                    pixels[y * size + x] = new Color(1f, 0f, 0f, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        
        var image = _cursorIndicator.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false; 
        
        var rectTransform = _cursorIndicator.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(20, 20); 
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        
        _cursorIndicator.transform.SetAsLastSibling();
        
        _cursorIndicator.SetActive(false);
    }

    #region UI 创建方法

    private void InitializeBlurSource()
    {
        
        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            LoggerHelper.LogWarning("主相机未找到，模糊效果将不可用");
            return;
        }

        var source = mainCamera.GetComponent<TranslucentImageSource>();
        if (source == null)
        {
            source = mainCamera.gameObject.AddComponent<TranslucentImageSource>();
        }

        
        var blurConfig = new ScalableBlurConfig
        {
            Strength = 12f,      
            Iteration = 4        
        };
        source.BlurConfig = blurConfig;
        source.Downsample = 1;  
    }

    private void CreateMainPanel()
    {
        
        _layoutBuilder.BuildMainPanel(_canvas.transform);

        
        UpdateTransportModePanels();
    }

    private void CreatePlayerStatusPanel()
    {
        _components.PlayerStatusPanel = CreateModernPanel("PlayerStatusPanel", _canvas.transform, new Vector2(420, 600), new Vector2(1680, 130));
        MakeDraggable(_components.PlayerStatusPanel);
        _components.PlayerStatusPanel.SetActive(false);

        var layout = _components.PlayerStatusPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 0;
        layout.childForceExpandHeight = false;

        
        var titleBar = CreateTitleBar(_components.PlayerStatusPanel.transform);
        CreateText("Title", titleBar.transform, CoopLocalization.Get("ui.window.playerStatus"), 22, ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);

        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(titleBar.transform, false);
        var spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleWidth = 1;

        CreateIconButton("CloseBtn", titleBar.transform, "x", () =>
        {
            showPlayerStatusWindow = false;
            StartCoroutine(AnimatePanel(_components.PlayerStatusPanel, false));
        }, 36, ModernColors.Error);

        
        var contentArea = CreateContentArea(_components.PlayerStatusPanel.transform);

        
        var scrollView = CreateModernScrollView("PlayerListScroll", contentArea.transform, 450);
        _components.PlayerListContent = scrollView.transform.Find("Viewport/Content");
    }

    private void CreateVotePanel()
    {
        _components.VotePanel = CreateModernPanel("VotePanel", _canvas.transform, new Vector2(420, 320), new Vector2(-1, 0), TextAnchor.MiddleLeft);
        _components.VotePanel.SetActive(false);

        var layout = _components.VotePanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
    }

    private void CreateSpectatorPanel()
    {
        _components.SpectatorPanel = CreateModernPanel("SpectatorPanel", _canvas.transform, new Vector2(430, 40), new Vector2(-1, -1), TextAnchor.LowerCenter);
        _components.SpectatorPanel.SetActive(false);

        var layout = _components.SpectatorPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(25, 25, 20, 20);

        var text = CreateText("SpectatorHint", _components.SpectatorPanel.transform,
            CoopLocalization.Get("ui.spectator.mode"), 18, ModernColors.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
    }
    
    private void CreateVoiceSettingsPanel()
    {
        _components.VoiceSettingsPanel = CreateModernPanel("VoiceSettingsPanel", _canvas.transform, new Vector2(500, 700), new Vector2(700, 90));
        MakeDraggable(_components.VoiceSettingsPanel);
        _components.VoiceSettingsPanel.SetActive(false);
        
        var layout = _components.VoiceSettingsPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 0;
        layout.childForceExpandHeight = false;
        
        var titleBar = CreateTitleBar(_components.VoiceSettingsPanel.transform);
        CreateText("Title", titleBar.transform, CoopLocalization.Get("ui.voice.settingsTitle"), 22, ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);
        
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(titleBar.transform, false);
        var spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleWidth = 1;
        
        CreateIconButton("CloseBtn", titleBar.transform, "x", () => {
            showVoiceSettingsWindow = false;
            StartCoroutine(AnimatePanel(_components.VoiceSettingsPanel, false));
        }, 36, ModernColors.Error);
        
        var contentArea = CreateContentArea(_components.VoiceSettingsPanel.transform);
        var scrollView = CreateModernScrollView("VoiceSettingsScroll", contentArea.transform, 580);
        var scrollContent = scrollView.transform.Find("Viewport/Content");

        var hotkeyCard = CreateModernCard(scrollContent, "HotkeySettings");
        CreateSectionHeader(hotkeyCard.transform, CoopLocalization.Get("ui.voice.hotkey"));
        var hotkeyRow = CreateHorizontalGroup(hotkeyCard.transform, "HotkeyRow");
        CreateText("CurrentKeyLabel", hotkeyRow.transform, CoopLocalization.Get("ui.voice.currentKey") + ": ", 14, ModernColors.TextSecondary);
        var currentKeyText = CreateText("CurrentKeyValue", hotkeyRow.transform, _currentVoiceHotkey.ToString(), 14, ModernColors.Success, TextAlignmentOptions.Left, FontStyles.Bold);
        CreateModernButton("SetHotkeyBtn", hotkeyCard.transform, CoopLocalization.Get("ui.voice.pressKey"), () => {
            if (!_isWaitingForHotkey)
            {
                _isWaitingForHotkey = true;
                var btn = hotkeyCard.transform.Find("SetHotkeyBtn")?.GetComponent<Button>();
                StartCoroutine(WaitForHotkeyInput(currentKeyText, btn));
            }
        }, -1, ModernColors.Primary, 45, 15);

        var muteCard = CreateModernCard(scrollContent, "MuteSettings");
        CreateSectionHeader(muteCard.transform, CoopLocalization.Get("ui.voice.mute"));
        var muteRow = CreateHorizontalGroup(muteCard.transform, "MuteRow");
        CreateText("MuteLabel", muteRow.transform, CoopLocalization.Get("ui.voice.muted"), 14, ModernColors.TextSecondary);
        CreateToggle(muteRow.transform, _isVoiceMuted, (value) => {
            _isVoiceMuted = value;
            if (Main.Voice.VoiceManager.Instance != null) {
                Main.Voice.VoiceManager.Instance.SetMuted(value);
            }
        });

        var inputVolumeCard = CreateModernCard(scrollContent, "InputVolumeSettings");
        CreateSectionHeader(inputVolumeCard.transform, CoopLocalization.Get("ui.voice.inputVolume"));
        var inputVolRow = CreateHorizontalGroup(inputVolumeCard.transform, "InputVolRow");
        var inputVolText = CreateText("InputVolValue", inputVolRow.transform, $"{Mathf.RoundToInt(_inputVolume * 100)}%", 14, ModernColors.TextSecondary);
        CreateSlider(inputVolRow.transform, _inputVolume, 0f, 2f, (value) => {
            _inputVolume = value;
            inputVolText.text = $"{Mathf.RoundToInt(value * 100)}%";
            if (Main.Voice.VoiceManager.Instance != null) {
                Main.Voice.VoiceManager.Instance.SetInputVolume(value);
            }
        });
        
        var outputVolumeCard = CreateModernCard(scrollContent, "OutputVolumeSettings");
        CreateSectionHeader(outputVolumeCard.transform, CoopLocalization.Get("ui.voice.outputVolume"));
        var outputVolRow = CreateHorizontalGroup(outputVolumeCard.transform, "OutputVolRow");
        var outputVolText = CreateText("OutputVolValue", outputVolRow.transform, $"{Mathf.RoundToInt(_outputVolume * 100)}%", 14, ModernColors.TextSecondary);
        CreateSlider(outputVolRow.transform, _outputVolume, 0f, 2f, (value) => {
            _outputVolume = value;
            outputVolText.text = $"{Mathf.RoundToInt(value * 100)}%";
            if (Main.Voice.VoiceManager.Instance != null) {
                Main.Voice.VoiceManager.Instance.SetOutputVolume(value);
            }
        });
        
        var smartNoiseCard = CreateModernCard(scrollContent, "SmartNoiseReduction");
        CreateSectionHeader(smartNoiseCard.transform, CoopLocalization.Get("ui.voice.smartNoise"));
        var smartNoiseRow = CreateHorizontalGroup(smartNoiseCard.transform, "SmartNoiseRow");
        CreateText("SmartNoiseLabel", smartNoiseRow.transform, CoopLocalization.Get("ui.voice.enableSmartNoise"), 14, ModernColors.TextSecondary);
        CreateToggle(smartNoiseRow.transform, _smartNoiseReduction, (value) => {
            _smartNoiseReduction = value;
            if (_noiseReductionSlider != null)
            {
                _noiseReductionSlider.interactable = !value;
            }
            if (Main.Voice.VoiceManager.Instance != null) {
                Main.Voice.VoiceManager.Instance.SetSmartNoiseReduction(value);
            }
        });
        
        var noiseCard = CreateModernCard(scrollContent, "NoiseReduction");
        CreateSectionHeader(noiseCard.transform, CoopLocalization.Get("ui.voice.noiseReduction"));
        var noiseRow = CreateHorizontalGroup(noiseCard.transform, "NoiseRow");
        var noiseText = CreateText("NoiseValue", noiseRow.transform, $"{Mathf.RoundToInt(_noiseReductionLevel * 100)}%", 14, ModernColors.TextSecondary);
        _noiseReductionSlider = CreateSlider(noiseRow.transform, _noiseReductionLevel, 0f, 1f, (value) => {
            _noiseReductionLevel = value;
            noiseText.text = $"{Mathf.RoundToInt(value * 100)}%";
            if (Main.Voice.VoiceManager.Instance != null) {
                Main.Voice.VoiceManager.Instance.SetNoiseReduction(value);
            }
        });
        _noiseReductionSlider.interactable = !_smartNoiseReduction;
        
        var inputCard = CreateModernCard(scrollContent, "InputDevice");
        CreateSectionHeader(inputCard.transform, CoopLocalization.Get("ui.voice.inputDevice"));
        CreateText("InputDeviceName", inputCard.transform, GetCurrentInputDevice(), 14, ModernColors.TextSecondary);
        
        var activeCard = CreateModernCard(scrollContent, "ActiveSpeakers");
        CreateSectionHeader(activeCard.transform, CoopLocalization.Get("ui.voice.activeList"));
        _components.ActiveSpeakersContent = activeCard.transform;
    }

    private string GetCurrentInputDevice()
    {
        if (Microphone.devices.Length > 0)
        {
            return Microphone.devices[0];
        }
        return CoopLocalization.Get("ui.voice.defaultDevice");
    }

    private IEnumerator WaitForHotkeyInput(TMP_Text displayText, Button hotkeyButton)
    {
        var buttonText = hotkeyButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = CoopLocalization.Get("ui.voice.pressAnyKey");
            buttonText.color = ModernColors.Warning;
        }
        
        displayText.text = "...";
        displayText.color = ModernColors.Warning;
        
        yield return new WaitForSeconds(0.1f);
        
        while (_isWaitingForHotkey)
        {
            if (Input.anyKeyDown)
            {
                foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(key) && key != KeyCode.Mouse0 && key != KeyCode.Mouse1)
                    {
                        _currentVoiceHotkey = key;
                        displayText.text = key.ToString();
                        displayText.color = ModernColors.Success;
                        _isWaitingForHotkey = false;
                        
                        if (buttonText != null)
                        {
                            buttonText.text = CoopLocalization.Get("ui.voice.pressKey");
                            buttonText.color = ModernColors.PrimaryText;
                        }
                        
                        if (Main.Voice.VoiceManager.Instance != null)
                        {
                            Main.Voice.VoiceManager.Instance.SetPushToTalkKey(key);
                        }
                        yield break;
                    }
                }
            }
            yield return null;
        }
        
        if (buttonText != null)
        {
            buttonText.text = CoopLocalization.Get("ui.voice.pressKey");
            buttonText.color = ModernColors.PrimaryText;
        }
        displayText.color = ModernColors.Success;
    }

    private Slider CreateSlider(Transform parent, float value, float min, float max, System.Action<float> onValueChanged)
    {
        var sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(parent, false);
        var sliderLayout = sliderObj.AddComponent<LayoutElement>();
        sliderLayout.preferredWidth = 200;
        sliderLayout.preferredHeight = 20;
        var slider = sliderObj.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.onValueChanged.AddListener((val) => onValueChanged?.Invoke(val));
        var bg = new GameObject("Background");
        bg.transform.SetParent(sliderObj.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = ModernColors.InputBg;
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        var fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-10, 0);
        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRect = fill.AddComponent<RectTransform>();
        fillRect.sizeDelta = Vector2.zero;
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = ModernColors.Primary;
        slider.fillRect = fillRect;
        slider.targetGraphic = fillImage;
        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        var handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = new Vector2(-10, 0);
        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleRect = handle.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20, 20);
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = ModernColors.TextPrimary;
        slider.handleRect = handleRect;
        return slider;
    }

    private Toggle CreateToggle(Transform parent, bool isOn, System.Action<bool> onValueChanged)
    {
        var toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(parent, false);
        var toggleLayout = toggleObj.AddComponent<LayoutElement>();
        toggleLayout.preferredWidth = 50;
        toggleLayout.preferredHeight = 25;
        
        var toggle = toggleObj.AddComponent<Toggle>();
        toggle.isOn = isOn;
        toggle.onValueChanged.AddListener((val) => onValueChanged?.Invoke(val));
        
        var bg = new GameObject("Background");
        bg.transform.SetParent(toggleObj.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = ModernColors.InputBg;
        
        var checkmark = new GameObject("Checkmark");
        checkmark.transform.SetParent(bg.transform, false);
        var checkRect = checkmark.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkRect.sizeDelta = new Vector2(20, 20);
        var checkImage = checkmark.AddComponent<Image>();
        checkImage.color = ModernColors.Success;
        
        toggle.graphic = checkImage;
        toggle.targetGraphic = bgImage;
        
        return toggle;
    }

    #endregion

    #region UI 更新方法

    private void UpdateModeDisplay()
    {
        
        bool isActiveServer = IsServer && networkStarted;
        bool isSteamMode = TransportMode == NetworkTransportMode.SteamP2P;
        bool isInSteamLobby = isSteamMode && LobbyManager != null && LobbyManager.IsInLobby;

        if (_components?.ModeText != null)
        {
            string modeText = CoopLocalization.Get("ui.status.notConnected");
            if (networkStarted)
            {
                if (isSteamMode)
                {
                    modeText = isInSteamLobby ? (LobbyManager.IsHost ? CoopLocalization.Get("ui.mode.server") : CoopLocalization.Get("ui.mode.client")) : CoopLocalization.Get("ui.transport.mode.steam");
                }
                else
                {
                    modeText = IsServer ? CoopLocalization.Get("ui.mode.server") : CoopLocalization.Get("ui.mode.client");
                }
            }
            else if (isSteamMode)
            {
                modeText = CoopLocalization.Get("ui.transport.mode.steam");
            }
            _components.ModeText.text = modeText;

            if (_components.ModeIndicator != null)
                _components.ModeIndicator.color = (isActiveServer || isInSteamLobby) ? ModernColors.Success : ModernColors.Info;
        }

        
        if (_components?.ModeInfoText != null)
        {
            if (isSteamMode)
            {
                if (isInSteamLobby)
                {
                    if (LobbyManager.IsHost)
                    {
                        var lobbyInfo = LobbyManager.TryGetLobbyInfo(LobbyManager.CurrentLobbyId, out var info) ? (SteamLobbyManager.LobbyInfo?)info : null;
                        if (lobbyInfo != null)
                        {
                            _components.ModeInfoText.text = CoopLocalization.Get("ui.steam.currentLobby", lobbyInfo.Value.LobbyName, lobbyInfo.Value.MemberCount, lobbyInfo.Value.MaxMembers);
                        }
                        else
                        {
                            _components.ModeInfoText.text = CoopLocalization.Get("ui.steam.server.waiting");
                        }
                    }
                    else
                    {
                        _components.ModeInfoText.text = CoopLocalization.Get("ui.steam.client.connected");
                    }
                    _components.ModeInfoText.color = ModernColors.Success;
                }
                else
                {
                    _components.ModeInfoText.text = CoopLocalization.Get("ui.steam.hint.createOrJoin");
                    _components.ModeInfoText.color = ModernColors.TextSecondary;
                }
            }
            else
            {
                if (isActiveServer)
                {
                    int currentPort = Mod?.port ?? 9050;
                    _components.ModeInfoText.text = CoopLocalization.Get("ui.server.listenPort") + " " + currentPort;
                    _components.ModeInfoText.color = ModernColors.TextSecondary;
                }
                else
                {
                    _components.ModeInfoText.text = CoopLocalization.Get("ui.server.hint.willUsePort", manualPort);
                    _components.ModeInfoText.color = ModernColors.TextSecondary;
                }
            }
        }

        
        if (_components?.ModeToggleButton != null && _components?.ModeToggleButtonText != null)
        {
            if (isSteamMode)
            {
                
                _components.ModeToggleButton.gameObject.SetActive(false);
            }
            else
            {
                _components.ModeToggleButton.gameObject.SetActive(true);
                _components.ModeToggleButtonText.text = isActiveServer ? CoopLocalization.Get("ui.server.close") : CoopLocalization.Get("ui.server.create");

                
                var image = _components.ModeToggleButton.GetComponent<Image>();
                if (image != null)
                {
                    var colors = _components.ModeToggleButton.colors;
                    var baseColor = isActiveServer ? new Color(0.85f, 0.45f, 0.40f, 0.95f) : new Color(0.45f, 0.75f, 0.50f, 0.95f);
                    colors.normalColor = baseColor;
                    colors.highlightedColor = new Color(baseColor.r + 0.05f, baseColor.g + 0.05f, baseColor.b + 0.05f, baseColor.a);
                    colors.pressedColor = new Color(baseColor.r - 0.1f, baseColor.g - 0.1f, baseColor.b - 0.1f, baseColor.a);
                    _components.ModeToggleButton.colors = colors;
                }
            }
        }

        
        if (_components?.ServerPortText != null)
        {
            if (isSteamMode)
            {
                _components.ServerPortText.color = ModernColors.TextSecondary;
            }
        }

        if (_components?.ConnectionCountText != null)
        {
            if (isSteamMode && isInSteamLobby)
            {
                var lobbyInfo = LobbyManager.TryGetLobbyInfo(LobbyManager.CurrentLobbyId, out var info) ? (SteamLobbyManager.LobbyInfo?)info : null;
                var count = lobbyInfo != null ? lobbyInfo.Value.MemberCount - 1 : 0; 
                _components.ConnectionCountText.text = $"{count}";
                _components.ConnectionCountText.color = count > 0 ? ModernColors.Success : ModernColors.TextSecondary;
            }
            else if (isActiveServer)
            {
                var count = netManager?.ConnectedPeerList.Count ?? 0;
                _components.ConnectionCountText.text = $"{count}";
                _components.ConnectionCountText.color = count > 0 ? ModernColors.Success : ModernColors.TextSecondary;
            }
            else
            {
                _components.ConnectionCountText.text = "0";
                _components.ConnectionCountText.color = ModernColors.TextSecondary;
            }
        }

        
        if (isSteamMode && _components?.SteamCreateLeaveButton != null && _components?.SteamCreateLeaveButtonText != null)
        {
            bool lobbyActive = LobbyManager != null && LobbyManager.IsInLobby;

            
            _components.SteamCreateLeaveButtonText.text = lobbyActive
                ? CoopLocalization.Get("ui.steam.leaveLobby")
                : CoopLocalization.Get("ui.steam.createHost");

            
            var buttonImage = _components.SteamCreateLeaveButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                var colors = _components.SteamCreateLeaveButton.colors;
                var baseColor = lobbyActive ? ModernColors.Error : ModernColors.Success;
                colors.normalColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.95f);
                colors.highlightedColor = new Color(baseColor.r + 0.05f, baseColor.g + 0.05f, baseColor.b + 0.05f, 0.97f);
                colors.pressedColor = new Color(baseColor.r - 0.1f, baseColor.g - 0.1f, baseColor.b - 0.1f, 1f);
                _components.SteamCreateLeaveButton.colors = colors;
            }
        }
    }

    
    
    
    private void SetStatusText(string text, Color color)
    {
        if (_components?.StatusText != null)
        {
            _components.StatusText.text = text;
            _components.StatusText.color = color;
        }
        if (_components?.SteamStatusText != null)
        {
            _components.SteamStatusText.text = text;
            _components.SteamStatusText.color = color;
        }
    }

    private void UpdateConnectionStatus()
    {
        if (Mod == null) return;
        if (_components?.StatusText == null && _components?.SteamStatusText == null) return;

        var currentStatus = Mod.status;

        
        if (currentStatus != _status)
        {
            _status = currentStatus;

            
            Color statusColor = ModernColors.TextSecondary;
            string statusIcon = "[*]";

            if (currentStatus.Contains("已连接"))
            {
                statusColor = ModernColors.Success;
                statusIcon = "[OK]";
            }
            else if (currentStatus.Contains("连接中") || currentStatus.Contains("正在连接"))
            {
                statusColor = ModernColors.Info;
                statusIcon = "[*]";
            }
            else if (currentStatus.Contains("断开") || currentStatus.Contains("失败") || currentStatus.Contains("错误"))
            {
                statusColor = ModernColors.Error;
                statusIcon = "[!]";
            }
            else if (currentStatus.Contains("启动"))
            {
                statusColor = ModernColors.Success;
                statusIcon = "[OK]";
            }

            string statusText = $"{statusIcon} {currentStatus}";
            SetStatusText(statusText, statusColor);
        }

        
        if (!IsServer && connectedPeer != null && networkStarted)
        {
            CheckServerInGame();
        }
    }

    private float _serverCheckTimer = 0f;
    private const float SERVER_CHECK_INTERVAL = 2f; 
    private float _pingUpdateTimer = 0f;
    private const float PING_UPDATE_INTERVAL = 1f; 

    private void CheckServerInGame()
    {
        _serverCheckTimer += Time.deltaTime;
        if (_serverCheckTimer < SERVER_CHECK_INTERVAL)
            return;

        _serverCheckTimer = 0f;

        
        if (playerStatuses != null && playerStatuses.Count > 0)
        {
            
            foreach (var kvp in playerStatuses)
            {
                var hostStatus = kvp.Value;
                if (hostStatus != null && hostStatus.EndPoint.Contains("Host"))
                {
                    
                    if (!hostStatus.IsInGame)
                    {
                        LoggerHelper.LogWarning("服务端不在关卡内，断开连接");

                        SetStatusText("[!] " + CoopLocalization.Get("ui.error.serverNotInGame"), ModernColors.Warning);

                        
                        if (connectedPeer != null)
                        {
                            connectedPeer.Disconnect();
                        }
                        return;
                    }
                    break;
                }
            }
        }
    }

    private void UpdateHostList()
    {
        if (_components?.HostListContent == null || IsServer) return;

        
        var toRemove = _hostEntries.Keys.Where(h => !hostSet.Contains(h)).ToList();
        foreach (var h in toRemove)
        {
            Destroy(_hostEntries[h]);
            _hostEntries.Remove(h);
        }

        
        foreach (var host in hostList)
        {
            if (!_hostEntries.ContainsKey(host))
            {
                var entry = CreateHostEntry(host);
                _hostEntries[host] = entry;
            }
        }

        
        if (hostList.Count == 0 && _components.HostListContent.childCount == 0)
        {
            var emptyHint = CreateText("EmptyHint", _components.HostListContent, CoopLocalization.Get("ui.hostList.empty"), 14, ModernColors.TextTertiary, TextAlignmentOptions.Center);
        }
    }

    private GameObject CreateHostEntry(string host)
    {
        
        var entry = new GameObject($"Host_{host}");
        entry.transform.SetParent(_components.HostListContent, false);

        var entryLayout = entry.AddComponent<HorizontalLayoutGroup>();
        entryLayout.padding = new RectOffset(20, 20, 15, 15);  
        entryLayout.spacing = 15;
        entryLayout.childForceExpandWidth = false;
        entryLayout.childControlWidth = true;
        entryLayout.childAlignment = TextAnchor.MiddleLeft;  

        var bg = entry.AddComponent<Image>();
        bg.color = GlassTheme.CardBg;
        bg.sprite = CreateEmbeddedNoiseSprite();
        bg.type = Image.Type.Tiled;

        var entryLayoutElement = entry.AddComponent<LayoutElement>();
        entryLayoutElement.preferredHeight = 75;  
        entryLayoutElement.minHeight = 75;
        entryLayoutElement.flexibleWidth = 1;

        
        var button = entry.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = GlassTheme.CardBg;
        colors.highlightedColor = GlassTheme.ButtonHover;
        colors.pressedColor = GlassTheme.ButtonActive;
        button.colors = colors;
        button.targetGraphic = bg;

        var parts = host.Split(':');
        var ip = parts.Length > 0 ? parts[0] : host;
        var portStr = parts.Length > 1 ? parts[1] : "9050";

        
        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(entry.transform, false);
        var iconLayout = iconObj.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 40;
        iconLayout.preferredHeight = 40;
        var iconImage = iconObj.AddComponent<Image>();
        iconImage.color = ModernColors.Primary;

        
        var infoArea = new GameObject("InfoArea");
        infoArea.transform.SetParent(entry.transform, false);
        var infoLayout = infoArea.AddComponent<VerticalLayoutGroup>();
        infoLayout.spacing = 6;  
        infoLayout.childForceExpandHeight = false;
        infoLayout.childControlHeight = false;
        infoLayout.childAlignment = TextAnchor.MiddleLeft;  
        var infoLayoutElement = infoArea.AddComponent<LayoutElement>();
        infoLayoutElement.preferredWidth = 500;

        CreateText("ServerName", infoArea.transform, CoopLocalization.Get("ui.hostList.lanServer", ip), 16, ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);
        CreateText("ServerDetails", infoArea.transform, CoopLocalization.Get("ui.hostList.serverDetails", portStr), 13, ModernColors.TextSecondary, TextAlignmentOptions.Left);

        
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(entry.transform, false);
        var spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleWidth = 1;

        
        var statusBadge = CreateBadge(entry.transform, CoopLocalization.Get("ui.status.online"), ModernColors.Success);
        statusBadge.GetComponent<LayoutElement>().preferredWidth = 70;

        
        CreateModernButton("ConnectBtn", entry.transform, CoopLocalization.Get("ui.hostList.connect"), () =>
        {
            
            if (!CheckCanConnect())
                return;

            if (parts.Length == 2 && int.TryParse(parts[1], out var p))
            {
                if (netManager == null || !netManager.IsRunning || IsServer || !networkStarted)
                    Mod.StartNetwork(false);
                Mod.ConnectToHost(parts[0], p);
            }
        }, 120, ModernColors.Primary, 45, 16);

        return entry;
    }

    public void UpdatePlayerList(bool forceRebuild = false)
    {
        if (_components?.PlayerListContent == null) return;

        
        var allPlayers = Utils.Database.PlayerInfoDatabase.Instance.GetAllPlayers().ToList();
        
        
        var currentPlayerIds = new HashSet<string>(
            allPlayers.Select(p => p.SteamId)
        );
        
        
        bool needsRebuild = forceRebuild || !_displayedPlayerIds.SetEquals(currentPlayerIds);
        
        if (!needsRebuild)
            return;
        
        LoggerHelper.Log($"[MModUI] 玩家列表已更新，重建UI (当前: {currentPlayerIds.Count}, 之前: {_displayedPlayerIds.Count}, 强制: {forceRebuild})");
        
        
        foreach (Transform child in _components.PlayerListContent)
            Destroy(child.gameObject);
        _playerEntries.Clear();
        _playerPingTexts.Clear();
        
        
        _displayedPlayerIds.Clear();
        foreach (var id in currentPlayerIds)
            _displayedPlayerIds.Add(id);
        
        
        foreach (var player in allPlayers)
        {
            CreatePlayerEntry(player);
        }
    }

    
    
    
    private ulong GetSteamIdFromStatus(PlayerStatus status)
    {
        if (!SteamManager.Initialized || LobbyManager == null || !LobbyManager.IsInLobby)
        {
            return 0;
        }

        
        if (status.EndPoint.StartsWith("Steam:"))
        {
            var steamIdStr = status.EndPoint.Substring(6);  
            if (ulong.TryParse(steamIdStr, out ulong steamId))
            {
                return steamId;
            }
        }

        
        if (status.EndPoint.StartsWith("Host:") && SteamManager.Initialized)
        {
            var lobbyOwner = SteamMatchmaking.GetLobbyOwner(new CSteamID(LobbyManager.CurrentLobbyId));
            return lobbyOwner.m_SteamID;
        }

        
        var parts = status.EndPoint.Split(':');
        if (parts.Length == 2 && System.Net.IPAddress.TryParse(parts[0], out var ipAddr) && int.TryParse(parts[1], out var port))
        {
            var ipEndPoint = new System.Net.IPEndPoint(ipAddr, port);
            if (SteamEndPointMapper.Instance != null &&
                SteamEndPointMapper.Instance.TryGetSteamID(ipEndPoint, out CSteamID cSteamId))
            {
                return cSteamId.m_SteamID;
            }
        }

        return 0;
    }

    private void CreatePlayerEntry(Utils.Database.PlayerInfoEntity player)
    {
        var entry = CreateModernCard(_components.PlayerListContent, $"Player_{player.SteamId}");

        
        if (player.IsLocalPlayer)
        {
            var bg = entry.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(0.24f, 0.52f, 0.98f, 0.15f); 
                var outline = entry.AddComponent<Outline>();
                outline.effectColor = ModernColors.Primary;
                outline.effectDistance = new Vector2(2, -2);
            }
        }

        var headerRow = CreateHorizontalGroup(entry.transform, "Header");

        
        bool isInGame = player.CustomData.TryGetValue("IsInGame", out var inGameObj) 
            && inGameObj is bool inGameValue && inGameValue;
        
        var statusDot = new GameObject("StatusDot");
        statusDot.transform.SetParent(headerRow.transform, false);
        var dotLayout = statusDot.AddComponent<LayoutElement>();
        dotLayout.preferredWidth = 10;
        dotLayout.preferredHeight = 10;
        var dotImage = statusDot.AddComponent<Image>();
        dotImage.color = isInGame ? ModernColors.Success : ModernColors.Warning;

        
        var nameText = CreateText("Name", headerRow.transform, player.PlayerName, 16, 
            ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);
        
        
        if (player.IsLocalPlayer)
        {
            CreateBadge(headerRow.transform, CoopLocalization.Get("ui.playerStatus.local"), 
                ModernColors.Primary);
        }

        CreateDivider(entry.transform);

        var infoRow = CreateHorizontalGroup(entry.transform, "Info");
        
        
        CreateText("ID", infoRow.transform, 
            CoopLocalization.Get("ui.playerStatus.id") + ": " + player.SteamId, 
            13, ModernColors.TextSecondary);
        
        
        int latency = 0;
        if (player.CustomData.TryGetValue("Latency", out var latencyObj) && latencyObj is int latencyValue)
        {
            latency = latencyValue;
        }
        
        var pingText = CreateText("Ping", infoRow.transform, $"{latency}ms", 13,
            latency < 50 ? ModernColors.Success :
            latency < 100 ? ModernColors.Warning : ModernColors.Error);
        
        
        _playerPingTexts[player.SteamId] = pingText;
        
        
        var stateText = CreateText("State", infoRow.transform, 
            isInGame ? CoopLocalization.Get("ui.playerStatus.inGameStatus") : 
                       CoopLocalization.Get("ui.playerStatus.idle"), 
            13, isInGame ? ModernColors.Success : ModernColors.TextSecondary);
        
        
        if (IsServer && !player.IsLocalPlayer && SteamManager.Initialized)
        {
            if (ulong.TryParse(player.SteamId, out ulong targetSteamId) && targetSteamId > 0)
            {
                var kickButton = CreateIconButton("KickBtn", infoRow.transform, "踢", () =>
                {
                    LoggerHelper.Log($"[MModUI] 主机踢出玩家: SteamID={targetSteamId}");
                    KickMessage.Server_KickPlayer(targetSteamId, "被主机踢出");
                }, 50, ModernColors.Error);
            }
        }
    }

    private void UpdateVotePanel()
    {
        if (SceneNet.Instance == null)
        {
            if (_components?.VotePanel != null && _components.VotePanel.activeSelf)
            {
                StartCoroutine(AnimatePanel(_components.VotePanel, false));
                _lastVoteActive = false;
            }
            return;
        }

        bool active = SceneNet.Instance.sceneVoteActive;

        
        if (_components?.VotePanel != null && _components.VotePanel.activeSelf != active)
        {
            if (active)
                StartCoroutine(AnimatePanel(_components.VotePanel, true));
            else
                StartCoroutine(AnimatePanel(_components.VotePanel, false));
            _lastVoteActive = active;
        }

        if (!active) return;

        
        bool needsRebuild = false;
        string rebuildReason = "";

        if (_lastVoteActive != active)
        {
            needsRebuild = true;
            rebuildReason = "vote active changed";
        }
        else if (_lastVoteSceneId != SceneNet.Instance.sceneTargetId)
        {
            needsRebuild = true;
            rebuildReason = "target scene changed";
        }
        else if (_lastLocalReady != SceneNet.Instance.localReady)
        {
            needsRebuild = true;
            rebuildReason = "local ready changed";
        }
        else
        {
            
            var currentParticipants = new HashSet<string>(SceneNet.Instance.sceneParticipantIds);
            if (!_lastVoteParticipants.SetEquals(currentParticipants))
            {
                needsRebuild = true;
                rebuildReason = $"participants changed ({_lastVoteParticipants.Count} -> {currentParticipants.Count})";
            }
        }

        if (!needsRebuild)
        {
            
            if (Time.time - _lastVoteUpdateTime > 1f)
            {
                needsRebuild = true;
                rebuildReason = "periodic update";
                _lastVoteUpdateTime = Time.time;
            }
        }

        if (!needsRebuild) return;

        

        
        _lastVoteActive = active;
        _lastVoteSceneId = SceneNet.Instance.sceneTargetId;
        _lastLocalReady = SceneNet.Instance.localReady;
        _lastVoteParticipants.Clear();
        foreach (var pid in SceneNet.Instance.sceneParticipantIds)
            _lastVoteParticipants.Add(pid);

        
        var childCount = _components.VotePanel.transform.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(_components.VotePanel.transform.GetChild(i).gameObject);
        }

        
        var sceneName = Utils.SceneNameMapper.GetDisplayName(SceneNet.Instance.sceneTargetId);

        
        var titleText = CreateText("VoteTitle", _components.VotePanel.transform, CoopLocalization.Get("ui.vote.title"), 22, ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);
        var titleLayout = titleText.gameObject.GetComponent<LayoutElement>();
        titleLayout.flexibleWidth = 0;
        titleLayout.preferredWidth = -1;

        var sceneText = CreateText("SceneName", _components.VotePanel.transform, sceneName, 18, ModernColors.Primary, TextAlignmentOptions.Left, FontStyles.Bold);
        var sceneLayout = sceneText.gameObject.GetComponent<LayoutElement>();
        sceneLayout.flexibleWidth = 0;
        sceneLayout.preferredWidth = -1;

        CreateDivider(_components.VotePanel.transform);

        
        var readySection = CreateModernCard(_components.VotePanel.transform, "ReadySection");
        var readyLayout = readySection.GetComponent<LayoutElement>();
        if (readyLayout != null)
        {
            readyLayout.flexibleWidth = 0;
            readyLayout.minWidth = -1;
        }

        var readyText = CreateText("ReadyStatus", readySection.transform,
            SceneNet.Instance.localReady ? "[OK] " + CoopLocalization.Get("ui.vote.ready") : "[  ] " + CoopLocalization.Get("ui.vote.notReady"), 16,
            SceneNet.Instance.localReady ? ModernColors.Success : ModernColors.Warning);
        CreateText("ReadyHint", readySection.transform, CoopLocalization.Get("ui.vote.pressKey", readyKey, ""), 13, ModernColors.TextTertiary);

        
        if (IsServer)
        {
            CreateDivider(_components.VotePanel.transform);
            var cancelButton = CreateModernButton("CancelVote", _components.VotePanel.transform,
                CoopLocalization.Get("ui.vote.cancel", "取消投票"),
                OnCancelVote, -1, ModernColors.Error, 40, 14);
        }

        
        var listTitle = CreateText("PlayerListTitle", _components.VotePanel.transform, CoopLocalization.Get("ui.vote.playerReadyStatus"), 16, ModernColors.TextSecondary);
        var listTitleLayout = listTitle.gameObject.GetComponent<LayoutElement>();
        listTitleLayout.flexibleWidth = 0;
        listTitleLayout.preferredWidth = -1;

        
        foreach (var pid in SceneNet.Instance.sceneParticipantIds)
        {
            SceneNet.Instance.sceneReady.TryGetValue(pid, out var ready);
            var playerRow = CreateModernListItem(_components.VotePanel.transform, $"Player_{pid}");

            var statusIcon = CreateText("Status", playerRow.transform, ready ? CoopLocalization.Get("ui.vote.readyIcon") : CoopLocalization.Get("ui.vote.notReadyIcon"), 16,
                ready ? ModernColors.Success : ModernColors.TextTertiary);
            var statusLayout = statusIcon.gameObject.GetComponent<LayoutElement>();
            statusLayout.flexibleWidth = 0;
            statusLayout.preferredWidth = 60;

            
            string displayName = pid;
            string displayId = pid;

            
            if (SceneNet.Instance.cachedVoteData?.playerList?.items != null)
            {
                int playerCount = SceneNet.Instance.cachedVoteData.playerList.items.Count();
                LoggerHelper.Log($"[MModUI] 尝试从投票数据获取玩家名字: pid={pid}, 投票数据玩家数={playerCount}");
                foreach (var player in SceneNet.Instance.cachedVoteData.playerList.items)
                {
                    LoggerHelper.Log($"[MModUI] 检查玩家: playerId={player.playerId}, steamName={player.steamName}");
                    if (player.playerId == pid && !string.IsNullOrEmpty(player.steamName))
                    {
                        
                        bool isHost = player.playerId.StartsWith("Host:");
                        string prefix = isHost ? "HOST" : "CLIENT";
                        displayName = $"{prefix}_{player.steamName}";
                        displayId = player.steamId;
                        LoggerHelper.Log($"[MModUI] ✅ 从投票数据获取到名字: {displayName}");
                        break;
                    }
                }
            }
            else
            {
                LoggerHelper.Log($"[MModUI] ⚠️ 投票数据为空，无法获取 Steam 名字");
            }

            
            if (displayName == pid && TransportMode == NetworkTransportMode.SteamP2P && SteamManager.Initialized && LobbyManager != null && LobbyManager.IsInLobby)
            {
                try
                {
                    
                    ulong steamIdValue = 0;

                    
                    if (ulong.TryParse(pid, out steamIdValue) && steamIdValue > 0)
                    {
                        
                    }
                    else
                    {
                        
                        if (pid.StartsWith("Host:"))
                        {
                            
                            
                            if (localPlayerStatus != null && localPlayerStatus.EndPoint == pid)
                            {
                                steamIdValue = SteamUser.GetSteamID().m_SteamID;
                            }
                            else if (SteamManager.Initialized)
                            {
                                
                                var lobbyOwner = SteamMatchmaking.GetLobbyOwner(new CSteamID(LobbyManager.CurrentLobbyId));
                                steamIdValue = lobbyOwner.m_SteamID;
                            }
                        }
                        else if (pid.StartsWith("Client:"))
                        {
                            
                            
                            if (localPlayerStatus != null && localPlayerStatus.EndPoint == pid)
                            {
                                steamIdValue = SteamUser.GetSteamID().m_SteamID;
                            }
                            else
                            {
                                
                                IEnumerable<PlayerStatus> allStatuses = IsServer
                                    ? playerStatuses?.Values
                                    : clientPlayerStatuses?.Values;
                                if (allStatuses != null)
                                {
                                    foreach (var status in allStatuses)
                                    {
                                        if (status.EndPoint == pid)
                                        {
                                            steamIdValue = GetSteamIdFromStatus(status);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            
                            var parts = pid.Split(':');
                            if (parts.Length == 2 && System.Net.IPAddress.TryParse(parts[0], out var ipAddr) && int.TryParse(parts[1], out var port))
                            {
                                var ipEndPoint = new System.Net.IPEndPoint(ipAddr, port);
                                if (SteamEndPointMapper.Instance != null &&
                                    SteamEndPointMapper.Instance.TryGetSteamID(ipEndPoint, out CSteamID cSteamId))
                                {
                                    steamIdValue = cSteamId.m_SteamID;
                                }
                            }
                        }
                    }

                    
                    if (steamIdValue > 0)
                    {
                        var cSteamId = new CSteamID(steamIdValue);
                        string cachedName = LobbyManager.GetCachedMemberName(cSteamId);

                        if (!string.IsNullOrEmpty(cachedName))
                        {
                            
                            var lobbyOwner = SteamMatchmaking.GetLobbyOwner(new CSteamID(LobbyManager.CurrentLobbyId));
                            string prefix = (steamIdValue == lobbyOwner.m_SteamID) ? "HOST" : "CLIENT";
                            displayName = $"{prefix}_{cachedName}";
                        }
                        else
                        {
                            
                            string steamUsername = SteamFriends.GetFriendPersonaName(cSteamId);
                            if (!string.IsNullOrEmpty(steamUsername) && steamUsername != "[unknown]")
                            {
                                var lobbyOwner = SteamMatchmaking.GetLobbyOwner(new CSteamID(LobbyManager.CurrentLobbyId));
                                string prefix = (steamIdValue == lobbyOwner.m_SteamID) ? "HOST" : "CLIENT";
                                displayName = $"{prefix}_{steamUsername}";
                            }
                            else
                            {
                                displayName = $"Player_{steamIdValue.ToString().Substring(Math.Max(0, steamIdValue.ToString().Length - 4))}";
                            }
                        }

                        displayId = steamIdValue.ToString();
                    }
                }
                catch (System.Exception ex)
                {
                    LoggerHelper.LogWarning($"[MModUI] Steam API 调用失败（可能在直连模式下错误调用）: {ex.Message}");
                    
                }
            }

            
            LoggerHelper.Log($"[MModUI] 最终显示名称: pid={pid}, displayName={displayName}, displayId={displayId}");
            var nameText = CreateText("Name", playerRow.transform, displayName, 14, ModernColors.TextPrimary);
            var nameLayout = nameText.gameObject.GetComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;

            CreateText("ID", playerRow.transform, displayId, 12, ModernColors.TextSecondary);
        }
    }

    
    
    
    private void OnCancelVote()
    {
        if (SceneNet.Instance == null)
        {
            SetStatusText("[!] 投票系统未初始化", ModernColors.Error);
            return;
        }

        
        if (!IsServer)
        {
            SetStatusText("[!] 只有房主可以取消投票", ModernColors.Error);
            return;
        }

        
        SceneNet.Instance.CancelVote();
        SetStatusText("[OK] 已取消投票", ModernColors.Success);
        LoggerHelper.Log("[MModUI] 房主取消了投票");
    }

    private void UpdateSpectatorPanel()
    {
        if (_components?.SpectatorPanel != null)
        {
            var shouldShow = Spectator.Instance?._spectatorActive ?? false;
            if (_components.SpectatorPanel.activeSelf != shouldShow)
            {
                if (shouldShow)
                    StartCoroutine(AnimatePanel(_components.SpectatorPanel, true));
                else
                    StartCoroutine(AnimatePanel(_components.SpectatorPanel, false));
            }
        }
    }

    
    
    
    private void UpdatePlayerPingDisplays()
    {
        if (_playerPingTexts.Count == 0) return;

        
        _pingUpdateTimer += Time.deltaTime;
        if (_pingUpdateTimer < PING_UPDATE_INTERVAL)
            return;

        _pingUpdateTimer = 0f;

        
        var allPlayers = Utils.Database.PlayerInfoDatabase.Instance.GetAllPlayers();

        
        foreach (var player in allPlayers)
        {
            
            if (_playerPingTexts.TryGetValue(player.SteamId, out var pingText) && pingText != null)
            {
                
                int latency = 0;
                if (player.CustomData.TryGetValue("Latency", out var latencyObj) && latencyObj is int latencyValue)
                {
                    latency = latencyValue;
                }

                
                pingText.text = $"{latency}ms";

                
                if (latency < 50)
                    pingText.color = ModernColors.Success;
                else if (latency < 100)
                    pingText.color = ModernColors.Warning;
                else
                    pingText.color = ModernColors.Error;
            }
        }
    }

    #endregion

    #region 现代化UI Helper方法

    internal GameObject CreateModernPanel(string name, Transform parent, Vector2 size, Vector2 anchorPos, TextAnchor pivot = TextAnchor.UpperLeft)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        var rect = panel.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        SetAnchor(rect, anchorPos, pivot);

        
        bool useTranslucentImage = false;
        TranslucentImage translucentImage = null;

        try
        {
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var source = mainCamera.GetComponent<TranslucentImageSource>();
                if (source != null)
                {
                    translucentImage = panel.AddComponent<TranslucentImage>();
                    translucentImage.source = source;
                    translucentImage.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
                    useTranslucentImage = true;

                    
                    StartCoroutine(InitializeTranslucentImageProperties(translucentImage));
                }
            }
        }
        catch (System.Exception e)
        {
            LoggerHelper.LogWarning($"TranslucentImage 初始化失败，使用普通背景: {e.Message}");
            if (translucentImage != null)
            {
                Destroy(translucentImage);
                translucentImage = null;
            }
            useTranslucentImage = false;
        }

        
        if (!useTranslucentImage)
        {
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.25f, 0.93f);
            image.sprite = CreateEmbeddedNoiseSprite();
            image.type = Image.Type.Tiled;
        }

        
        var shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = MModUI.ModernColors.Shadow;
        shadow.effectDistance = new Vector2(0, -4);

        
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.08f);
        outline.effectDistance = new Vector2(1, -1);

        
        panel.AddComponent<CanvasGroup>();

        return panel;
    }

    private IEnumerator InitializeTranslucentImageProperties(TranslucentImage translucentImage)
    {
        
        yield return null;
        yield return null;

        if (translucentImage != null && translucentImage.material != null)
        {
            try
            {
                translucentImage.vibrancy = 0.3f;      
                translucentImage.brightness = 0.9f;    
                translucentImage.flatten = 0.5f;       
            }
            catch (System.Exception e)
            {
                LoggerHelper.LogWarning($"TranslucentImage 参数设置失败: {e.Message}");
            }
        }
    }
    private static Sprite CreateEmbeddedNoiseSprite()
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        var rand = new System.Random();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float g = 0.5f + (float)(rand.NextDouble() - 0.5) * 0.12f; 
                tex.SetPixel(x, y, new Color(g, g, g, 0.83f)); 

            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    internal GameObject CreateTitleBar(Transform parent)
    {
        var titleBar = CreateHorizontalGroup(parent, "TitleBar");
        var layout = titleBar.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 15, 15);
        layout.spacing = 12;

        
        var bg = titleBar.AddComponent<Image>();
        bg.color = GlassTheme.CardBg;                
        bg.sprite = CreateEmbeddedNoiseSprite();     
        bg.type = Image.Type.Tiled;

        
        var outline = titleBar.AddComponent<Outline>();
        outline.effectColor = GlassTheme.Divider;
        outline.effectDistance = new Vector2(1, -1);

        var shadow = titleBar.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.25f);
        shadow.effectDistance = new Vector2(0, -2);

        var layoutElement = titleBar.GetComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1;
        layoutElement.preferredHeight = 60;
        layoutElement.minHeight = 60;
        layoutElement.flexibleHeight = 0;  

        return titleBar;
    }


    private GameObject CreateContentArea(Transform parent)
    {
        var content = new GameObject("ContentArea");
        content.transform.SetParent(parent, false);

        var rect = content.AddComponent<RectTransform>();

        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 15, 20);
        layout.spacing = 15;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;

        var image = content.AddComponent<Image>();
        image.color = new Color(0.28f, 0.28f, 0.28f, 0.87f); 
        image.sprite = CreateEmbeddedNoiseSprite();
        image.type = Image.Type.Tiled;

        
        var outline = content.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        outline.effectDistance = new Vector2(1, -1);

        var layoutElement = content.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1;
        layoutElement.flexibleHeight = 1;

        return content;
    }


    internal GameObject CreateModernCard(Transform parent, string name)
    {
        var card = new GameObject(name);
        card.transform.SetParent(parent, false);

        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 12, 12);
        layout.spacing = 8;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;

        var image = card.AddComponent<Image>();
        image.color = GlassTheme.CardBg;
        image.sprite = CreateEmbeddedNoiseSprite();
        image.type = Image.Type.Tiled;

        var outline = card.AddComponent<Outline>();
        outline.effectColor = GlassTheme.Divider;
        outline.effectDistance = new Vector2(1, -1);

        var shadow = card.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        shadow.effectDistance = new Vector2(0, -3);

        var layoutElement = card.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1;

        return card;
    }


    private GameObject CreateModernListItem(Transform parent, string name)
    {
        var item = new GameObject(name);
        item.transform.SetParent(parent, false);

        var layout = item.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleLeft;

        var image = item.AddComponent<Image>();
        image.color = GlassTheme.CardBg;
        image.sprite = CreateEmbeddedNoiseSprite();
        image.type = Image.Type.Tiled;

        var layoutElem = item.AddComponent<LayoutElement>();
        layoutElem.preferredHeight = 44;
        layoutElem.flexibleWidth = 1;

        return item;
    }


    internal void CreateSectionHeader(Transform parent, string text)
    {
        var header = CreateText("SectionHeader", parent, text, 15, GlassTheme.TextSecondary, TextAlignmentOptions.Left, FontStyles.Bold);
        var layoutElement = header.gameObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 26;
        layoutElement.minHeight = 26;

        
        var divider = new GameObject("Divider");
        divider.transform.SetParent(parent, false);
        var img = divider.AddComponent<Image>();
        img.color = GlassTheme.Divider;

        var rect = divider.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.sizeDelta = new Vector2(0, 1);
    }


    internal TMP_Text CreateInfoRow(Transform parent, string label, string value)
    {
        var row = CreateHorizontalGroup(parent, $"InfoRow_{label}");
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 10;

        var labelText = CreateText("Label", row.transform, label, 14, GlassTheme.TextSecondary);
        var labelLayout = labelText.gameObject.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 70;
        labelLayout.flexibleWidth = 0;

        var valueText = CreateText("Value", row.transform, value, 14, GlassTheme.Text, TextAlignmentOptions.Left, FontStyles.Bold);

        return valueText;
    }


    internal GameObject CreateStatusBar(Transform parent)
    {
        var bar = new GameObject("StatusBar");
        bar.transform.SetParent(parent, false);

        var layout = bar.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 10, 10);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleLeft;

        var bg = bar.AddComponent<Image>();
        bg.color = GlassTheme.CardBg;
        bg.sprite = CreateEmbeddedNoiseSprite();
        bg.type = Image.Type.Tiled;

        var outline = bar.AddComponent<Outline>();
        outline.effectColor = GlassTheme.Divider;
        outline.effectDistance = new Vector2(1, -1);

        var shadow = bar.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.25f);
        shadow.effectDistance = new Vector2(0, -2);

        var layoutElement = bar.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1;
        layoutElement.preferredHeight = 42;
        layoutElement.minHeight = 42;
        layoutElement.flexibleHeight = 0;  

        return bar;
    }


    private GameObject CreateActionBar(Transform parent)
    {
        var bar = CreateHorizontalGroup(parent, "ActionBar");
        var layout = bar.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 8, 8);
        layout.spacing = 10;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var bg = bar.AddComponent<Image>();
        bg.color = new Color(0.22f, 0.22f, 0.22f, 0.95f); 
        bg.sprite = CreateEmbeddedNoiseSprite();
        bg.type = Image.Type.Tiled;

        var outline = bar.AddComponent<Outline>();
        outline.effectColor = GlassTheme.Divider;
        outline.effectDistance = new Vector2(1, -1);

        var shadow = bar.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.3f);
        shadow.effectDistance = new Vector2(0, -3);

        var layoutElement = bar.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 48;
        layoutElement.minHeight = 48;

        return bar;
    }


    private GameObject CreateHintBar(Transform parent)
    {
        var bar = new GameObject("HintBar");
        bar.transform.SetParent(parent, false);

        var layout = bar.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 12, 12);
        layout.childAlignment = TextAnchor.MiddleCenter;

        var bg = bar.AddComponent<Image>();
        bg.color = new Color(0.32f, 0.32f, 0.32f, 0.94f); 
        bg.sprite = CreateEmbeddedNoiseSprite();
        bg.type = Image.Type.Tiled;

        var outline = bar.AddComponent<Outline>();
        outline.effectColor = GlassTheme.Divider;
        outline.effectDistance = new Vector2(1, -1);

        var layoutElement = bar.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1;
        layoutElement.preferredHeight = 42;
        layoutElement.minHeight = 42;

        return bar;
    }


    internal GameObject CreateBadge(Transform parent, string text, Color color)
    {
        var badge = new GameObject("Badge");
        badge.transform.SetParent(parent, false);

        var layoutElement = badge.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 20;
        layoutElement.preferredWidth = 50;

        var bg = badge.AddComponent<Image>();
        bg.color = new Color(color.r, color.g, color.b, 0.2f);

        var badgeText = CreateText("Text", badge.transform, text, 11, color, TextAlignmentOptions.Center, FontStyles.Bold);

        return badge;
    }

    internal void CreateDivider(Transform parent)
    {
        var divider = new GameObject("Divider");
        divider.transform.SetParent(parent, false);

        var layoutElement = divider.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 1;
        layoutElement.flexibleWidth = 1;

        var image = divider.AddComponent<Image>();
        image.color = ModernColors.Divider;
    }

    private void SetAnchor(RectTransform rect, Vector2 anchorPos, TextAnchor pivot)
    {
        if (anchorPos.x < 0) 
        {
            rect.anchorMin = new Vector2(0.5f, anchorPos.y < 0 ? 0 : 1);
            rect.anchorMax = new Vector2(0.5f, anchorPos.y < 0 ? 0 : 1);
            rect.pivot = new Vector2(0.5f, anchorPos.y < 0 ? 0 : 1);
            rect.anchoredPosition = new Vector2(0, anchorPos.y < 0 ? 50 : -50);
        }
        else 
        {
            
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            
            rect.anchoredPosition = new Vector2(anchorPos.x, -anchorPos.y);
        }
    }

    private GameObject CreateSection(Transform parent, string name)
    {
        return CreateModernCard(parent, name);
    }

    internal GameObject CreateHorizontalGroup(Transform parent, string name)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);

        var layout = group.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        var layoutElement = group.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1;

        return group;
    }

    internal TMP_Text CreateText(string name, Transform parent, string text, int fontSize = 16, Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
    {
        var textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        var tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.color = color ?? ModernColors.TextPrimary;
        tmpText.alignment = alignment;
        tmpText.fontStyle = style;
        tmpText.enableWordWrapping = true;

        var fitter = textObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layoutElement = textObj.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1;

        return tmpText;
    }

    internal Button CreateModernButton(string name, Transform parent, string text, UnityEngine.Events.UnityAction onClick, float width = -1, Color? color = null, float height = 40, int fontSize = 15)
    {
        var btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        var layout = btnObj.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
        if (width > 0) layout.preferredWidth = width;
        else layout.flexibleWidth = 1;

        var baseColor = GlassTheme.ButtonBg;
        var image = btnObj.AddComponent<Image>();
        image.color = baseColor;
        image.sprite = CreateEmbeddedNoiseSprite();
        image.type = Image.Type.Tiled;

        var button = btnObj.AddComponent<Button>();
        button.onClick.AddListener(onClick);
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = GlassTheme.ButtonHover;
        colors.pressedColor = GlassTheme.ButtonActive;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
        colors.fadeDuration = 0.15f;
        button.colors = colors;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = GlassTheme.Text;
        tmp.fontStyle = FontStyles.Bold;

        var rect = tmp.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        return button;
    }


    internal Button CreateIconButton(string name, Transform parent, string icon, UnityEngine.Events.UnityAction onClick, float size = 32, Color? color = null)
    {
        var btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        var layoutElement = btnObj.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = size;
        layoutElement.preferredHeight = size;

        var btnColor = color ?? ModernColors.TextSecondary;
        var image = btnObj.AddComponent<Image>();
        image.color = new Color(0, 0, 0, 0); 

        var button = btnObj.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var colors = button.colors;
        colors.normalColor = new Color(1, 1, 1, 0);
        colors.highlightedColor = new Color(1, 1, 1, 0.1f);
        colors.pressedColor = new Color(1, 1, 1, 0.2f);
        colors.disabledColor = new Color(1, 1, 1, 0);
        button.colors = colors;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        var tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = icon;
        tmpText.fontSize = (int)(size * 0.6f);
        tmpText.color = btnColor;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.fontStyle = FontStyles.Bold;

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        return button;
    }

    internal TMP_InputField CreateModernInputField(string name, Transform parent, string placeholder, string defaultValue)
    {
        var inputObj = new GameObject(name);
        inputObj.transform.SetParent(parent, false);

        var layout = inputObj.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1;
        layout.preferredHeight = 35;
        layout.minHeight = 35;

        var image = inputObj.AddComponent<Image>();
        image.color = GlassTheme.InputBg;
        image.sprite = CreateEmbeddedNoiseSprite();
        image.type = Image.Type.Tiled;

        var input = inputObj.AddComponent<TMP_InputField>();

        var textArea = new GameObject("TextArea");
        textArea.transform.SetParent(inputObj.transform, false);
        var rect = textArea.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12, 8);
        rect.offsetMax = new Vector2(-12, -8);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(textArea.transform, false);
        var tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.color = GlassTheme.Text;
        tmpText.fontSize = 15;
        tmpText.alignment = TextAlignmentOptions.Left;

        var placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(textArea.transform, false);
        var placeholderText = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholderText.text = placeholder;
        placeholderText.color = GlassTheme.TextSecondary;
        placeholderText.fontSize = 15;
        placeholderText.fontStyle = FontStyles.Italic;

        input.textViewport = rect;
        input.textComponent = tmpText;
        input.placeholder = placeholderText;
        input.text = defaultValue;

        return input;
    }


    internal GameObject CreateModernScrollView(string name, Transform parent, float height)
    {
        var scrollObj = new GameObject(name);
        scrollObj.transform.SetParent(parent, false);

        var scrollRect = scrollObj.AddComponent<RectTransform>();
        var layoutElement = scrollObj.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        layoutElement.minHeight = height;
        layoutElement.flexibleHeight = 0;

        
        var scrollImage = scrollObj.AddComponent<Image>();
        scrollImage.color = GlassTheme.CardBg;              
        scrollImage.sprite = CreateEmbeddedNoiseSprite();   
        scrollImage.type = Image.Type.Tiled;

        
        var outline = scrollObj.AddComponent<Outline>();
        outline.effectColor = GlassTheme.Divider;
        outline.effectDistance = new Vector2(1, -1);

        var shadow = scrollObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.25f);
        shadow.effectDistance = new Vector2(0, -3);

        
        var scroll = scrollObj.AddComponent<ScrollRect>();

        
        var viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.offsetMin = new Vector2(5, 5);
        viewportRect.offsetMax = new Vector2(-5, -5);

        var viewportImage = viewportObj.AddComponent<Image>();
        viewportImage.color = new Color(0.27f, 0.27f, 0.27f, 0.95f); 
        viewportImage.sprite = CreateEmbeddedNoiseSprite();
        viewportImage.type = Image.Type.Tiled;

        var viewportMask = viewportObj.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        
        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        var contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8;
        contentLayout.padding = new RectOffset(8, 8, 8, 8);
        contentLayout.childForceExpandHeight = false;

        var contentFitter = contentObj.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        
        scroll.content = contentRect;
        scroll.viewport = viewportRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        
        var scrollbarObj = new GameObject("Scrollbar");
        scrollbarObj.transform.SetParent(scrollObj.transform, false);
        var scrollbarRect = scrollbarObj.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1, 0);
        scrollbarRect.anchorMax = new Vector2(1, 1);
        scrollbarRect.pivot = new Vector2(1, 0.5f);
        scrollbarRect.sizeDelta = new Vector2(8, 0);

        var scrollbarImage = scrollbarObj.AddComponent<Image>();
        scrollbarImage.color = new Color(1f, 1f, 1f, 0.05f); 

        var scrollbar = scrollbarObj.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = scrollbarImage;

        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        return scrollObj;
    }


    internal void MakeDraggable(GameObject panel)
    {
        var dragger = panel.AddComponent<UIDragger>();
    }

    
    internal void CreateSteamServerListUI(Transform parent, MModUIComponents components)
    {
        
        var steamHeader = CreateModernCard(parent, "SteamHeader");
        var steamHeaderLayout = steamHeader.GetComponent<LayoutElement>();
        steamHeaderLayout.preferredHeight = 60;
        steamHeaderLayout.minHeight = 60;
        steamHeaderLayout.flexibleHeight = 0;

        var steamHeaderGroup = CreateHorizontalGroup(steamHeader.transform, "HeaderGroup");
        steamHeaderGroup.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);

        CreateText("ListTitle", steamHeaderGroup.transform, CoopLocalization.Get("ui.steam.lobbyList"), 20, ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);

        var headerSpacer = new GameObject("Spacer");
        headerSpacer.transform.SetParent(steamHeaderGroup.transform, false);
        var headerSpacerLayout = headerSpacer.AddComponent<LayoutElement>();
        headerSpacerLayout.flexibleWidth = 1;

        
        CreateModernButton("RefreshBtn", steamHeaderGroup.transform, CoopLocalization.Get("ui.steam.refresh"), () =>
        {
            if (LobbyManager != null) LobbyManager.RequestLobbyList();
        }, 120, ModernColors.Primary, 38, 15);

        
        var lobbyScroll = CreateModernScrollView("SteamLobbyScroll", parent, 445);
        var scrollLayout = lobbyScroll.GetComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1;
        components.SteamLobbyListContent = lobbyScroll.transform.Find("Viewport/Content");

        
        var passCard = CreateModernCard(parent, "JoinPassCard");
        var passCardLayout = passCard.GetComponent<LayoutElement>();
        passCardLayout.preferredHeight = 80;
        passCardLayout.minHeight = 80;

        var passRow = CreateHorizontalGroup(passCard.transform, "JoinPassRow");
        CreateText("JoinPassLabel", passRow.transform, CoopLocalization.Get("ui.steam.joinPassword"), 14, GlassTheme.TextSecondary).gameObject.GetComponent<LayoutElement>().preferredWidth = 80;
        var joinPassInput = CreateModernInputField("JoinPass", passRow.transform, CoopLocalization.Get("ui.steam.joinPasswordPlaceholder"), _steamJoinPassword);
        joinPassInput.contentType = TMP_InputField.ContentType.Password;
        joinPassInput.onValueChanged.AddListener(value => _steamJoinPassword = value);

        
        var steamStatusBar = CreateStatusBar(parent);
        _components.SteamStatusText = CreateText("SteamStatus", steamStatusBar.transform, $"[*] {status}", 14, ModernColors.TextSecondary);

        var steamStatusSpacer = new GameObject("Spacer");
        steamStatusSpacer.transform.SetParent(steamStatusBar.transform, false);
        var steamStatusSpacerLayout = steamStatusSpacer.AddComponent<LayoutElement>();
        steamStatusSpacerLayout.flexibleWidth = 1;

        CreateText("Hint", steamStatusBar.transform, CoopLocalization.Get("ui.hint.toggleUI", "="), 12, ModernColors.TextTertiary, TextAlignmentOptions.Right);
    }

    
    internal void CreateSteamControlPanel(Transform parent)
    {
        var controlCard = CreateModernCard(parent, "SteamControlCard");
        var controlLayout = controlCard.GetComponent<LayoutElement>();
        controlLayout.flexibleHeight = 1;

        CreateSectionHeader(controlCard.transform, CoopLocalization.Get("ui.steam.lobbySettings"));

        
        var nameRow = CreateHorizontalGroup(controlCard.transform, "NameRow");
        CreateText("NameLabel", nameRow.transform, CoopLocalization.Get("ui.steam.lobbyName"), 14, GlassTheme.TextSecondary).gameObject.GetComponent<LayoutElement>().preferredWidth = 70;
        var nameInput = CreateModernInputField("LobbyName", nameRow.transform, CoopLocalization.Get("ui.steam.lobbyNamePlaceholder"), _steamLobbyName);
        nameInput.onValueChanged.AddListener(value =>
        {
            _steamLobbyName = value;
            UpdateLobbyOptionsFromUI();
        });

        
        var passRow = CreateHorizontalGroup(controlCard.transform, "PassRow");
        CreateText("PassLabel", passRow.transform, CoopLocalization.Get("ui.steam.lobbyPassword"), 14, GlassTheme.TextSecondary).gameObject.GetComponent<LayoutElement>().preferredWidth = 70;
        var passInput = CreateModernInputField("LobbyPass", passRow.transform, CoopLocalization.Get("ui.steam.lobbyPasswordPlaceholder"), _steamLobbyPassword);
        passInput.contentType = TMP_InputField.ContentType.Password;
        passInput.onValueChanged.AddListener(value =>
        {
            _steamLobbyPassword = value;
            UpdateLobbyOptionsFromUI();
        });

        
        var visRow = CreateHorizontalGroup(controlCard.transform, "VisRow");
        CreateText("VisLabel", visRow.transform, CoopLocalization.Get("ui.steam.visibility"), 14, GlassTheme.TextSecondary).gameObject.GetComponent<LayoutElement>().preferredWidth = 70;

        var visButtons = CreateHorizontalGroup(visRow.transform, "VisButtons");
        visButtons.GetComponent<HorizontalLayoutGroup>().spacing = 5;
        CreateModernButton("Public", visButtons.transform, CoopLocalization.Get("ui.steam.visibility.public"), () =>
        {
            _steamLobbyFriendsOnly = false;
            UpdateLobbyOptionsFromUI();
        }, 90, _steamLobbyFriendsOnly ? GlassTheme.ButtonBg : ModernColors.Primary, 35, 13);

        CreateModernButton("Friends", visButtons.transform, CoopLocalization.Get("ui.steam.visibility.friends"), () =>
        {
            _steamLobbyFriendsOnly = true;
            UpdateLobbyOptionsFromUI();
        }, 90, _steamLobbyFriendsOnly ? ModernColors.Primary : GlassTheme.ButtonBg, 35, 13);

        
        var maxRow = CreateHorizontalGroup(controlCard.transform, "MaxRow");
        CreateText("MaxLabel", maxRow.transform, CoopLocalization.Get("ui.steam.maxPlayers.label"), 14, GlassTheme.TextSecondary).gameObject.GetComponent<LayoutElement>().preferredWidth = 70;

        var maxButtons = CreateHorizontalGroup(maxRow.transform, "MaxButtons");
        maxButtons.GetComponent<HorizontalLayoutGroup>().spacing = 5;
        CreateModernButton("Minus", maxButtons.transform, "-", () =>
        {
            _steamLobbyMaxPlayers = Mathf.Max(2, _steamLobbyMaxPlayers - 1);
            if (_components.SteamMaxPlayersText != null)
                _components.SteamMaxPlayersText.text = _steamLobbyMaxPlayers.ToString();
            UpdateLobbyOptionsFromUI();
        }, 35, GlassTheme.ButtonBg, 35, 13);

        _components.SteamMaxPlayersText = CreateText("MaxValue", maxButtons.transform, _steamLobbyMaxPlayers.ToString(), 14, ModernColors.TextPrimary);
        _components.SteamMaxPlayersText.gameObject.GetComponent<LayoutElement>().preferredWidth = 40;

        CreateModernButton("Plus", maxButtons.transform, "+", () =>
        {
            _steamLobbyMaxPlayers = Mathf.Min(16, _steamLobbyMaxPlayers + 1);
            if (_components.SteamMaxPlayersText != null)
                _components.SteamMaxPlayersText.text = _steamLobbyMaxPlayers.ToString();
            UpdateLobbyOptionsFromUI();
        }, 35, GlassTheme.ButtonBg, 35, 13);

        CreateDivider(controlCard.transform);

        
        var isInLobby = LobbyManager != null && LobbyManager.IsInLobby;
        _components.SteamCreateLeaveButton = CreateModernButton("CreateLobby", controlCard.transform,
            isInLobby ? CoopLocalization.Get("ui.steam.leaveLobby") : CoopLocalization.Get("ui.steam.createHost"),
            OnSteamCreateOrLeave, -1, isInLobby ? ModernColors.Error : ModernColors.Success, 45, 16);

        
        _components.SteamCreateLeaveButtonText = _components.SteamCreateLeaveButton.GetComponentInChildren<TextMeshProUGUI>();
    }


    private void UpdateTransportModePanels()
    {
        
        if (_components?.DirectModePanel != null && _components?.SteamModePanel != null)
        {
            _components.DirectModePanel.SetActive(TransportMode == NetworkTransportMode.Direct);
            _components.SteamModePanel.SetActive(TransportMode == NetworkTransportMode.SteamP2P);
        }

        
        if (_components?.DirectServerListArea != null && _components?.SteamServerListArea != null)
        {
            
            if (_components.DirectServerListArea == _components.SteamServerListArea)
            {
                
                _components.DirectServerListArea.SetActive(true);
            }
            else
            {
                
                _components.DirectServerListArea.SetActive(TransportMode == NetworkTransportMode.Direct);
                _components.SteamServerListArea.SetActive(TransportMode == NetworkTransportMode.SteamP2P);
            }
        }
    }


    private void UpdateLobbyOptionsFromUI()
    {
        if (Service == null) return;

        var maxPlayers = Mathf.Clamp(_steamLobbyMaxPlayers, 2, 16);
        _steamLobbyMaxPlayers = maxPlayers;

        var options = new SteamLobbyOptions
        {
            LobbyName = _steamLobbyName,
            Password = _steamLobbyPassword,
            Visibility = _steamLobbyFriendsOnly ? SteamLobbyVisibility.FriendsOnly : SteamLobbyVisibility.Public,
            MaxPlayers = maxPlayers
        };

        Service.ConfigureLobbyOptions(options);
    }

    #endregion

    #region 事件处理

    internal void OnToggleServerMode()
    {
        
        bool isActiveServer = IsServer && networkStarted;

        if (isActiveServer)
        {
            
            Mod.StopNetwork();

            SetStatusText("[OK] " + CoopLocalization.Get("ui.server.closed"), ModernColors.Info);

            LoggerHelper.Log("主机已关闭，网络已完全停止");
        }
        else
        {
            var relayManager = RelayServerManager.Instance;
            if (relayManager == null || !relayManager.IsConnectedToRelay)
            {
                SetStatusText("[!] 请先选择中继节点", ModernColors.Warning);
                LoggerHelper.Log("[MModUI] 尝试创建主机但未连接到中继节点，打开节点选择器");
                
                var lobbyUI = OnlineLobbyUI.Instance;
                if (lobbyUI != null)
                {
                    lobbyUI.OpenNodeSelector();
                }
                return;
            }
            
            
            if (int.TryParse(manualPort, out int serverPort))
            {
                
                Mod.port = serverPort;
                Mod.StartNetwork(true);

                SetStatusText("[OK] " + CoopLocalization.Get("ui.server.created", serverPort), ModernColors.Success);

                LoggerHelper.Log($"主机创建成功，使用端口: {serverPort}");
            }
            else
            {
                
                SetStatusText("[" + CoopLocalization.Get("ui.error") + "] " + CoopLocalization.Get("ui.manualConnect.portError"), ModernColors.Error);

                LoggerHelper.LogError($"端口格式错误: {manualPort}");
                return;
            }
        }

        
        StartCoroutine(DelayedUpdateModeDisplay());
    }

    private IEnumerator DelayedUpdateModeDisplay()
    {
        
        yield return null;
        UpdateModeDisplay();
    }

    private bool CheckCanConnect()
    {
        
        if (LocalPlayerManager.Instance == null)
        {
            SetStatusText("[!] " + CoopLocalization.Get("ui.error.gameNotInitialized"), ModernColors.Error);
            return false;
        }

        var isInGame = LocalPlayerManager.Instance.ComputeIsInGame(out var sceneId);
        if (!isInGame)
        {
            SetStatusText("[!] " + CoopLocalization.Get("ui.error.mustInLevel"), ModernColors.Warning);
            LoggerHelper.LogWarning("无法连接：客户端未在游戏关卡中");
            return false;
        }

        LoggerHelper.Log($"客户端关卡检查通过，当前场景: {sceneId}");
        return true;
    }

    internal void OnManualConnect()
    {
        if (_components?.IpInputField != null) manualIP = _components.IpInputField.text;
        if (_components?.PortInputField != null) manualPort = _components.PortInputField.text;

        if (!int.TryParse(manualPort, out var p))
        {
            
            SetStatusText("[!] " + CoopLocalization.Get("ui.manualConnect.portError"), ModernColors.Error);
            return;
        }

        
        if (!CheckCanConnect())
            return;

        if (netManager == null || !netManager.IsRunning || IsServer || !networkStarted)
            Mod.StartNetwork(false);
        Mod.ConnectToHost(manualIP, p);
        
    }

    
    
    internal void CopyPlayerDatabaseToClipboard()
    {
        try
        {
            var playerDb = Utils.Database.PlayerInfoDatabase.Instance;
            var json = playerDb.ExportToJsonWithStats(indented: true);
            
            GUIUtility.systemCopyBuffer = json;
            
            LoggerHelper.Log($"[PlayerDB] 已复制玩家数据库 JSON 到剪贴板 ({playerDb.Count} 名玩家)");
            LoggerHelper.Log($"[PlayerDB] JSON 内容:\n{json}");
            
            SetStatusText($"[OK] 已复制 {playerDb.Count} 名玩家数据到剪贴板", ModernColors.Success);
        }
        catch (Exception ex)
        {
            LoggerHelper.LogError($"[PlayerDB] 复制数据库失败: {ex.Message}\n{ex.StackTrace}");
            SetStatusText("[!] 复制数据库失败", ModernColors.Error);
        }
    }

    
    
    
    internal void SendJsonMessage()
    {
        try
        {
            if (_components?.JsonInputField == null)
            {
                LoggerHelper.LogWarning("[JSON] 输入框未初始化");
                return;
            }

            var json = _components.JsonInputField.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                LoggerHelper.LogWarning("[JSON] 输入内容为空");
                SetStatusText("[!] 请输入 JSON 消息", ModernColors.Warning);
                return;
            }

            LoggerHelper.Log($"[JSON] 准备发送消息:\n{json}");

            
            if (Service != null && Service.connectedPeer != null)
            {
                
                JsonMessage.SendToHost(json, LiteNetLib.DeliveryMethod.ReliableOrdered);
                LoggerHelper.Log("[JSON] 客户端已发送 JSON 消息到主机");
                SetStatusText("[OK] JSON 消息已发送", ModernColors.Success);
            }
            else if (Service != null && Service.IsServer)
            {
                
                var writer = Service.writer;
                writer.Reset();
                writer.Put(json);
                Service.netManager.SendToAll(writer, LiteNetLib.DeliveryMethod.ReliableOrdered);
                LoggerHelper.Log("[JSON] 主机已广播 JSON 消息");
                SetStatusText("[OK] JSON 消息已广播", ModernColors.Success);
            }
            else
            {
                LoggerHelper.LogWarning("[JSON] 未连接到网络");
                SetStatusText("[!] 未连接到网络", ModernColors.Warning);
            }

            
            _components.JsonInputField.text = "";
        }
        catch (Exception ex)
        {
            LoggerHelper.LogError($"[JSON] 发送消息失败: {ex.Message}\n{ex.StackTrace}");
            SetStatusText("[!] 发送 JSON 失败", ModernColors.Error);
        }
    }

    internal void DebugPrintLootBoxes()
    {
        if (LevelManager.LootBoxInventories == null)
        {
            LoggerHelper.LogWarning("LootBoxInventories is null. Make sure you are in a game level.");
            SetStatusText("[!] " + CoopLocalization.Get("ui.error.mustInLevel"), ModernColors.Warning);
            return;
        }

        var count = 0;
        foreach (var i in LevelManager.LootBoxInventories)
        {
            try
            {
                LoggerHelper.Log($"Name {i.Value.name} DisplayNameKey {i.Value.DisplayNameKey} Key {i.Key}");
                count++;
            }
            catch (Exception ex)
            {
                LoggerHelper.LogError($"Error printing loot box: {ex.Message}");
            }
        }

        LoggerHelper.Log($"Total LootBoxes: {count}");
        SetStatusText($"[OK] " + CoopLocalization.Get("ui.debug.lootBoxCount", count), ModernColors.Success);
    }

    internal void DebugPrintRemoteCharacters()
    {
        if (Service == null)
        {
            LoggerHelper.LogWarning("[Debug] NetService 未初始化");
            SetStatusText("[!] 网络服务未初始化", ModernColors.Warning);
            return;
        }

        var isServer = Service.IsServer;
        var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        LoggerHelper.Log($"========== Network Debug Info ==========");
        LoggerHelper.Log($"Timestamp: {timestamp}");
        LoggerHelper.Log($"Role: {(isServer ? "主机 (Server)" : "客户端 (Client)")}");
        LoggerHelper.Log($"========================================");

        var debugData = new Dictionary<string, object>
        {
            ["DebugVersion"] = "v2.0",  
            ["Timestamp"] = timestamp,
            ["Role"] = isServer ? "Server" : "Client",
            ["NetworkStarted"] = Service.networkStarted,
            ["Port"] = Service.port,
            ["Status"] = Service.status,
            ["TransportMode"] = Service.TransportMode.ToString()
        };

        
        var localPlayerData = new Dictionary<string, object>();
        if (Service.localPlayerStatus != null)
        {
            var lps = Service.localPlayerStatus;
            localPlayerData["EndPoint"] = lps.EndPoint ?? "null";
            localPlayerData["PlayerName"] = lps.PlayerName ?? "null";
            localPlayerData["IsInGame"] = lps.IsInGame;
            localPlayerData["SceneId"] = lps.SceneId ?? "null";
            localPlayerData["Position"] = lps.Position.ToString();
            localPlayerData["Rotation"] = lps.Rotation.eulerAngles.ToString();
            localPlayerData["Latency"] = lps.Latency;
            localPlayerData["CustomFaceJson"] = string.IsNullOrEmpty(lps.CustomFaceJson) ? "null" : $"[{lps.CustomFaceJson.Length} chars]";
            
            
            if (!isServer && Service.connectedPeer != null)
            {
                localPlayerData["ConnectedPeerEndPoint"] = Service.connectedPeer.EndPoint?.ToString() ?? "null";
                localPlayerData["ConnectedPeerId"] = Service.connectedPeer.Id;
            }
        }
        else
        {
            localPlayerData["Status"] = "null";
        }
        debugData["LocalPlayer"] = localPlayerData;
        
        
        var localCharacterData = new Dictionary<string, object>();
        if (CharacterMainControl.Main != null)
        {
            var localGO = CharacterMainControl.Main.gameObject;
            localCharacterData["GameObjectName"] = localGO.name;
            localCharacterData["InstanceId"] = localGO.GetInstanceID();
            localCharacterData["Active"] = localGO.activeSelf;
            localCharacterData["ActiveInHierarchy"] = localGO.activeInHierarchy;
            localCharacterData["Position"] = localGO.transform.position.ToString();
            localCharacterData["Rotation"] = localGO.transform.rotation.eulerAngles.ToString();
            
            
            var path = "";
            var t = localGO.transform;
            while (t != null)
            {
                path = t.name + (string.IsNullOrEmpty(path) ? "" : "/" + path);
                t = t.parent;
            }
            localCharacterData["ScenePath"] = path;
            
            
            localCharacterData["HasRemoteReplicaTag"] = localGO.GetComponent<RemoteReplicaTag>() != null;
            
            
            var renderers = localGO.GetComponentsInChildren<Renderer>();
            var enabledRenderers = renderers.Count(r => r.enabled);
            localCharacterData["TotalRenderers"] = renderers.Length;
            localCharacterData["EnabledRenderers"] = enabledRenderers;
            
            
            var components = localGO.GetComponents<Component>();
            var componentNames = new List<string>();
            foreach (var comp in components)
            {
                if (comp != null) componentNames.Add(comp.GetType().Name);
            }
            localCharacterData["AllComponents"] = string.Join(", ", componentNames);
            localCharacterData["ComponentCount"] = componentNames.Count;
        }
        else
        {
            localCharacterData["Status"] = "null";
        }
        debugData["LocalCharacter"] = localCharacterData;
        
        
        var allCharactersData = new List<object>();
        var allCharacters = UnityEngine.Object.FindObjectsOfType<CharacterMainControl>();
        foreach (var character in allCharacters)
        {
            var charGO = character.gameObject;
            var charInfo = new Dictionary<string, object>
            {
                ["GameObjectName"] = charGO.name,
                ["InstanceId"] = charGO.GetInstanceID(),
                ["IsMain"] = character == CharacterMainControl.Main,
                ["Active"] = charGO.activeSelf,
                ["Position"] = charGO.transform.position.ToString(),
                ["HasRemoteReplicaTag"] = charGO.GetComponent<RemoteReplicaTag>() != null,
                ["HasNetInterpolator"] = charGO.GetComponent<NetInterpolator>() != null,
                ["HasAnimInterpolator"] = charGO.GetComponent<AnimParamInterpolator>() != null
            };
            
            
            if (isServer && Service.remoteCharacters != null)
            {
                charInfo["InRemoteCharacters"] = Service.remoteCharacters.Values.Contains(charGO);
            }
            else if (!isServer && Service.clientRemoteCharacters != null)
            {
                charInfo["InClientRemoteCharacters"] = Service.clientRemoteCharacters.Values.Contains(charGO);
                
                var playerId = Service.clientRemoteCharacters.FirstOrDefault(kv => kv.Value == charGO).Key;
                charInfo["PlayerId"] = playerId ?? "null";
            }
            
            allCharactersData.Add(charInfo);
        }
        debugData["AllCharactersInScene"] = new Dictionary<string, object>
        {
            ["Count"] = allCharacters.Length,
            ["Data"] = allCharactersData
        };

        
        if (isServer)
        {
            
            var remoteCharsData = new List<object>();
            if (Service.remoteCharacters != null)
            {
                var index = 1;
                foreach (var kv in Service.remoteCharacters)
                {
                    var peer = kv.Key;
                    var go = kv.Value;
                    var charData = new Dictionary<string, object>
                    {
                        ["Index"] = index++,
                        ["PeerEndPoint"] = peer?.EndPoint?.ToString() ?? "null",
                        ["PeerId"] = peer?.Id ?? -1,
                        ["GameObjectName"] = go?.name ?? "null",
                        ["GameObjectInstanceId"] = go?.GetInstanceID() ?? 0,
                        ["GameObjectActive"] = go?.activeSelf ?? false,
                        ["GameObjectActiveInHierarchy"] = go?.activeInHierarchy ?? false,
                        ["Position"] = go?.transform.position.ToString() ?? "null",
                        ["Rotation"] = go?.transform.rotation.eulerAngles.ToString() ?? "null",
                        ["LocalPosition"] = go?.transform.localPosition.ToString() ?? "null",
                        ["LocalRotation"] = go?.transform.localRotation.eulerAngles.ToString() ?? "null"
                    };

                    if (go != null)
                    {
                        
                        var path = "";
                        var t = go.transform;
                        while (t != null)
                        {
                            path = t.name + (string.IsNullOrEmpty(path) ? "" : "/" + path);
                            t = t.parent;
                        }
                        charData["ScenePath"] = path;

                        
                        var cmc = go.GetComponent<CharacterMainControl>();
                        charData["HasCharacterMainControl"] = cmc != null;
                        if (cmc != null)
                        {
                            charData["CMC_Enabled"] = cmc.enabled;
                            charData["CMC_ModelRoot"] = cmc.modelRoot?.name ?? "null";
                            charData["CMC_CharacterModel"] = cmc.characterModel?.name ?? "null";
                        }

                        
                        var health = go.GetComponentInChildren<Health>(true);
                        if (health != null)
                        {
                            charData["Health_Current"] = health.CurrentHealth;
                            charData["Health_Max"] = health.MaxHealth;
                            charData["Health_GameObject"] = health.gameObject.name;
                            charData["Health_Enabled"] = health.enabled;
                        }
                        else
                        {
                            charData["Health_Status"] = "null";
                        }

                        
                        var netInterp = go.GetComponent<NetInterpolator>();
                        charData["HasNetInterpolator"] = netInterp != null;
                        if (netInterp != null)
                        {
                            charData["NetInterp_Enabled"] = netInterp.enabled;
                        }

                        var animInterp = go.GetComponent<AnimParamInterpolator>();
                        charData["HasAnimInterpolator"] = animInterp != null;
                        if (animInterp != null)
                        {
                            charData["AnimInterp_Enabled"] = animInterp.enabled;
                        }

                        
                        charData["HasRemoteReplicaTag"] = go.GetComponent<RemoteReplicaTag>() != null;
                        charData["HasAutoRequestHealthBar"] = go.GetComponent<AutoRequestHealthBar>() != null;
                        charData["HasHostForceHealthBar"] = go.GetComponent<HostForceHealthBar>() != null;

                        
                        var rb = go.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            charData["Rigidbody_IsKinematic"] = rb.isKinematic;
                            charData["Rigidbody_Velocity"] = rb.velocity.ToString();
                        }

                        var cc = go.GetComponent<CharacterController>();
                        charData["HasCharacterController"] = cc != null;
                        if (cc != null)
                        {
                            charData["CharacterController_Enabled"] = cc.enabled;
                        }

                        
                        var components = go.GetComponents<Component>();
                        var componentNames = new List<string>();
                        foreach (var comp in components)
                        {
                            if (comp != null)
                            {
                                componentNames.Add(comp.GetType().Name);
                            }
                        }
                        charData["AllComponents"] = string.Join(", ", componentNames);
                        charData["ComponentCount"] = componentNames.Count;
                        
                        
                        var renderers = go.GetComponentsInChildren<Renderer>();
                        var enabledRenderers = renderers.Count(r => r.enabled);
                        charData["TotalRenderers"] = renderers.Length;
                        charData["EnabledRenderers"] = enabledRenderers;
                        
                        
                        charData["ParentName"] = go.transform.parent?.name ?? "null";
                        charData["SiblingIndex"] = go.transform.GetSiblingIndex();
                    }

                    remoteCharsData.Add(charData);
                }
            }
            debugData["RemoteCharacters"] = new Dictionary<string, object>
            {
                ["Count"] = Service.remoteCharacters?.Count ?? 0,
                ["Data"] = remoteCharsData
            };

            
            var playerStatusesData = new List<object>();
            if (Service.playerStatuses != null)
            {
                foreach (var kv in Service.playerStatuses)
                {
                    var peer = kv.Key;
                    var status = kv.Value;
                    playerStatusesData.Add(new Dictionary<string, object>
                    {
                        ["PeerEndPoint"] = peer?.EndPoint?.ToString() ?? "null",
                        ["PeerId"] = peer?.Id ?? -1,
                        ["PlayerName"] = status.PlayerName ?? "null",
                        ["IsInGame"] = status.IsInGame,
                        ["SceneId"] = status.SceneId ?? "null",
                        ["Latency"] = status.Latency,
                        ["Position"] = status.Position.ToString(),
                        ["EquipmentCount"] = status.EquipmentList?.Count ?? 0,
                        ["WeaponCount"] = status.WeaponList?.Count ?? 0
                    });
                }
            }
            debugData["PlayerStatuses"] = new Dictionary<string, object>
            {
                ["Count"] = Service.playerStatuses?.Count ?? 0,
                ["Data"] = playerStatusesData
            };

            
            var connectedPeers = new List<object>();
            if (Service.netManager != null && Service.netManager.ConnectedPeerList != null)
            {
                foreach (var peer in Service.netManager.ConnectedPeerList)
                {
                    connectedPeers.Add(new Dictionary<string, object>
                    {
                        ["EndPoint"] = peer?.EndPoint?.ToString() ?? "null",
                        ["Id"] = peer?.Id ?? -1,
                        ["Ping"] = peer?.Ping ?? -1,
                        ["ConnectionState"] = peer?.ConnectionState.ToString() ?? "null"
                    });
                }
            }
            debugData["ConnectedPeers"] = new Dictionary<string, object>
            {
                ["Count"] = connectedPeers.Count,
                ["Data"] = connectedPeers
            };
        }
        
        else
        {
            
            var clientRemoteCharsData = new List<object>();
            if (Service.clientRemoteCharacters != null)
            {
                var index = 1;
                foreach (var kv in Service.clientRemoteCharacters)
                {
                    var playerId = kv.Key;
                    var go = kv.Value;
                    var charData = new Dictionary<string, object>
                    {
                        ["Index"] = index++,
                        ["PlayerId"] = playerId ?? "null",
                        ["GameObjectName"] = go?.name ?? "null",
                        ["GameObjectInstanceId"] = go?.GetInstanceID() ?? 0,
                        ["GameObjectActive"] = go?.activeSelf ?? false,
                        ["GameObjectActiveInHierarchy"] = go?.activeInHierarchy ?? false,
                        ["Position"] = go?.transform.position.ToString() ?? "null",
                        ["Rotation"] = go?.transform.rotation.eulerAngles.ToString() ?? "null",
                        ["LocalPosition"] = go?.transform.localPosition.ToString() ?? "null",
                        ["LocalRotation"] = go?.transform.localRotation.eulerAngles.ToString() ?? "null"
                    };

                    if (go != null)
                    {
                        
                        var path = "";
                        var t = go.transform;
                        while (t != null)
                        {
                            path = t.name + (string.IsNullOrEmpty(path) ? "" : "/" + path);
                            t = t.parent;
                        }
                        charData["ScenePath"] = path;

                        
                        var cmc = go.GetComponent<CharacterMainControl>();
                        charData["HasCharacterMainControl"] = cmc != null;
                        if (cmc != null)
                        {
                            charData["CMC_Enabled"] = cmc.enabled;
                            charData["CMC_ModelRoot"] = cmc.modelRoot?.name ?? "null";
                            charData["CMC_CharacterModel"] = cmc.characterModel?.name ?? "null";
                        }

                        
                        var health = go.GetComponentInChildren<Health>(true);
                        if (health != null)
                        {
                            charData["Health_Current"] = health.CurrentHealth;
                            charData["Health_Max"] = health.MaxHealth;
                            charData["Health_GameObject"] = health.gameObject.name;
                            charData["Health_Enabled"] = health.enabled;
                        }
                        else
                        {
                            charData["Health_Status"] = "null";
                        }

                        
                        var netInterp = go.GetComponent<NetInterpolator>();
                        charData["HasNetInterpolator"] = netInterp != null;
                        if (netInterp != null)
                        {
                            charData["NetInterp_Enabled"] = netInterp.enabled;
                        }

                        var animInterp = go.GetComponent<AnimParamInterpolator>();
                        charData["HasAnimInterpolator"] = animInterp != null;
                        if (animInterp != null)
                        {
                            charData["AnimInterp_Enabled"] = animInterp.enabled;
                        }

                        
                        charData["HasRemoteReplicaTag"] = go.GetComponent<RemoteReplicaTag>() != null;
                        charData["HasAutoRequestHealthBar"] = go.GetComponent<AutoRequestHealthBar>() != null;

                        
                        var rb = go.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            charData["Rigidbody_IsKinematic"] = rb.isKinematic;
                            charData["Rigidbody_Velocity"] = rb.velocity.ToString();
                        }

                        var cc = go.GetComponent<CharacterController>();
                        charData["HasCharacterController"] = cc != null;
                        if (cc != null)
                        {
                            charData["CharacterController_Enabled"] = cc.enabled;
                        }

                        
                        var components = go.GetComponents<Component>();
                        var componentNames = new List<string>();
                        foreach (var comp in components)
                        {
                            if (comp != null)
                            {
                                componentNames.Add(comp.GetType().Name);
                            }
                        }
                        charData["AllComponents"] = string.Join(", ", componentNames);
                        charData["ComponentCount"] = componentNames.Count;
                        
                        
                        var renderers = go.GetComponentsInChildren<Renderer>();
                        var enabledRenderers = renderers.Count(r => r.enabled);
                        charData["TotalRenderers"] = renderers.Length;
                        charData["EnabledRenderers"] = enabledRenderers;
                        
                        
                        charData["ParentName"] = go.transform.parent?.name ?? "null";
                        charData["SiblingIndex"] = go.transform.GetSiblingIndex();
                        
                        
                        var isLocalPlayerDuplicate = false;
                        if (Service.connectedPeer != null)
                        {
                            var myNetworkId = Service.connectedPeer.EndPoint?.ToString();
                            isLocalPlayerDuplicate = playerId == myNetworkId;
                        }
                        charData["IsLocalPlayerDuplicate"] = isLocalPlayerDuplicate;
                        
                        
                        charData["IsSelfId_Check"] = Service.IsSelfId(playerId);
                    }

                    clientRemoteCharsData.Add(charData);
                }
            }
            debugData["ClientRemoteCharacters"] = new Dictionary<string, object>
            {
                ["Count"] = Service.clientRemoteCharacters?.Count ?? 0,
                ["Data"] = clientRemoteCharsData
            };

            
            var clientPlayerStatusesData = new List<object>();
            if (Service.clientPlayerStatuses != null)
            {
                foreach (var kv in Service.clientPlayerStatuses)
                {
                    var playerId = kv.Key;
                    var status = kv.Value;
                    clientPlayerStatusesData.Add(new Dictionary<string, object>
                    {
                        ["PlayerId"] = playerId ?? "null",
                        ["PlayerName"] = status.PlayerName ?? "null",
                        ["IsInGame"] = status.IsInGame,
                        ["SceneId"] = status.SceneId ?? "null",
                        ["Latency"] = status.Latency,
                        ["Position"] = status.Position.ToString(),
                        ["EquipmentCount"] = status.EquipmentList?.Count ?? 0,
                        ["WeaponCount"] = status.WeaponList?.Count ?? 0
                    });
                }
            }
            debugData["ClientPlayerStatuses"] = new Dictionary<string, object>
            {
                ["Count"] = Service.clientPlayerStatuses?.Count ?? 0,
                ["Data"] = clientPlayerStatusesData
            };

            
            var connectedPeerData = new Dictionary<string, object>();
            if (Service.connectedPeer != null)
            {
                connectedPeerData["EndPoint"] = Service.connectedPeer.EndPoint?.ToString() ?? "null";
                connectedPeerData["Id"] = Service.connectedPeer.Id;
                connectedPeerData["Ping"] = Service.connectedPeer.Ping;
                connectedPeerData["ConnectionState"] = Service.connectedPeer.ConnectionState.ToString();
            }
            else
            {
                connectedPeerData["Status"] = "null";
            }
            debugData["ConnectedPeer"] = connectedPeerData;
        }

        
        var localPlayerManagerData = new Dictionary<string, object>();
        if (LocalPlayerManager.Instance != null)
        {
            var lpm = LocalPlayerManager.Instance;
            var isInGame = lpm.ComputeIsInGame(out var currentSceneId);
            localPlayerManagerData["IsInGame"] = isInGame;
            localPlayerManagerData["CurrentSceneId"] = currentSceneId ?? "null";
            localPlayerManagerData["HasCharacterMain"] = CharacterMainControl.Main != null;
        }
        else
        {
            localPlayerManagerData["Status"] = "null";
        }
        debugData["LocalPlayerManager"] = localPlayerManagerData;
        
        
        if (!isServer)
        {
            var createRemoteData = new Dictionary<string, object>();
            
            
            if (Service.clientRemoteCharacters != null && Service.connectedPeer != null)
            {
                var myNetworkId = Service.connectedPeer.EndPoint?.ToString();
                var hasSelfDuplicate = Service.clientRemoteCharacters.ContainsKey(myNetworkId);
                createRemoteData["HasSelfDuplicate"] = hasSelfDuplicate;
                createRemoteData["MyNetworkId"] = myNetworkId ?? "null";
                createRemoteData["MyLocalPlayerId"] = Service.localPlayerStatus?.EndPoint ?? "null";
                
                
                var allPlayerIds = new List<string>();
                foreach (var kv in Service.clientRemoteCharacters)
                {
                    allPlayerIds.Add(kv.Key);
                }
                createRemoteData["AllRemotePlayerIds"] = string.Join(", ", allPlayerIds);
            }
            
            debugData["CreateRemoteInfo"] = createRemoteData;
        }

        
        if (SceneNet.Instance != null)
        {
            var sceneNetData = new Dictionary<string, object>
            {
                ["SceneReadySidSent"] = SceneNet.Instance._sceneReadySidSent ?? "null",
                ["SceneVoteActive"] = SceneNet.Instance.sceneVoteActive,
                ["SceneTargetId"] = SceneNet.Instance.sceneTargetId ?? "null",
                ["LocalReady"] = SceneNet.Instance.localReady,
                ["ParticipantCount"] = SceneNet.Instance.sceneParticipantIds?.Count ?? 0,
                ["ReadyCount"] = SceneNet.Instance.sceneReady?.Count ?? 0
            };

            if (isServer)
            {
                sceneNetData["SrvSceneGateOpen"] = SceneNet.Instance._srvSceneGateOpen;
                sceneNetData["SrvGateReadyPidsCount"] = SceneNet.Instance._srvGateReadyPids?.Count ?? 0;
            }
            else
            {
                sceneNetData["CliSceneGateReleased"] = SceneNet.Instance._cliSceneGateReleased;
            }

            debugData["SceneNet"] = sceneNetData;
        }

        
        LoggerHelper.Log($"--- Summary ---");
        LoggerHelper.Log($"  Role: {debugData["Role"]}");
        LoggerHelper.Log($"  NetworkStarted: {debugData["NetworkStarted"]}");
        LoggerHelper.Log($"  LocalPlayer: {(Service.localPlayerStatus != null ? Service.localPlayerStatus.EndPoint : "null")}");
        
        if (isServer)
        {
            LoggerHelper.Log($"  RemoteCharacters: {Service.remoteCharacters?.Count ?? 0}");
            LoggerHelper.Log($"  PlayerStatuses: {Service.playerStatuses?.Count ?? 0}");
            LoggerHelper.Log($"  ConnectedPeers: {Service.netManager?.ConnectedPeerList?.Count ?? 0}");
        }
        else
        {
            LoggerHelper.Log($"  ClientRemoteCharacters: {Service.clientRemoteCharacters?.Count ?? 0}");
            LoggerHelper.Log($"  ClientPlayerStatuses: {Service.clientPlayerStatuses?.Count ?? 0}");
            LoggerHelper.Log($"  ConnectedPeer: {(Service.connectedPeer != null ? "Connected" : "null")}");
        }

        
        try
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(debugData, Newtonsoft.Json.Formatting.None);
            LoggerHelper.Log($"========== Complete Network State JSON ==========");
            LoggerHelper.Log(json);
            LoggerHelper.Log($"=================================================");
        }
        catch (Exception ex)
        {
            LoggerHelper.LogError($"[Debug] JSON 序列化失败: {ex.Message}");
            LoggerHelper.LogError($"[Debug] 堆栈: {ex.StackTrace}");
        }

        var summary = isServer 
            ? $"主机: {Service.remoteCharacters?.Count ?? 0} 个远程玩家" 
            : $"客户端: {Service.clientRemoteCharacters?.Count ?? 0} 个远程玩家";
        SetStatusText($"[OK] 已输出网络状态 ({summary})", ModernColors.Success);
    }

    internal void OnTransportModeChanged(NetworkTransportMode newMode)
    {
        if (Service == null) return;

        Service.SetTransportMode(newMode);
        UpdateTransportModePanels();

        if (newMode == NetworkTransportMode.SteamP2P && LobbyManager != null)
        {
            
            _displayedSteamLobbies.Clear();
            LobbyManager.RequestLobbyList();
        }
    }

    private void OnSteamCreateOrLeave()
    {
        var manager = LobbyManager;
        if (manager == null)
        {
            SetStatusText("[!] " + CoopLocalization.Get("ui.steam.error.notInitialized"), ModernColors.Error);
            return;
        }

        if (manager.IsInLobby)
        {
            
            NetService.Instance?.StopNetwork();
            manager.LeaveLobby();  

            SetStatusText("[OK] " + CoopLocalization.Get("ui.steam.lobby.left"), ModernColors.Info);
        }
        else
        {
            
            UpdateLobbyOptionsFromUI();
            NetService.Instance?.StartNetwork(true);
            SetStatusText("[*] " + CoopLocalization.Get("ui.steam.lobby.creating"), ModernColors.Info);
        }
    }

    private void UpdateSteamLobbyList()
    {
        if (_components?.SteamLobbyListContent == null || TransportMode != NetworkTransportMode.SteamP2P)
            return;

        
        var currentLobbies = new HashSet<ulong>(_steamLobbyInfos.Select(l => l.LobbyId));

        
        if (_displayedSteamLobbies.SetEquals(currentLobbies))
            return;

        
        LoggerHelper.Log($"[MModUI] Steam房间列表已更新，重建UI (当前: {currentLobbies.Count}, 之前: {_displayedSteamLobbies.Count})");

        
        foreach (Transform child in _components.SteamLobbyListContent)
            Destroy(child.gameObject);

        
        _displayedSteamLobbies.Clear();
        foreach (var id in currentLobbies)
            _displayedSteamLobbies.Add(id);

        if (_steamLobbyInfos.Count == 0)
        {
            CreateText("EmptyHint", _components.SteamLobbyListContent, CoopLocalization.Get("ui.steam.lobbiesEmpty"), 14, ModernColors.TextTertiary, TextAlignmentOptions.Center);
            return;
        }

        
        foreach (var lobby in _steamLobbyInfos)
        {
            CreateSteamLobbyEntry(lobby);
        }
    }

    private void CreateSteamLobbyEntry(SteamLobbyManager.LobbyInfo lobby)
    {
        var entry = CreateModernCard(_components.SteamLobbyListContent, $"Lobby_{lobby.LobbyId}");
        var entryLayout = entry.GetComponent<LayoutElement>();
        entryLayout.preferredHeight = 120;  
        entryLayout.minHeight = 120;

        
        var entryImage = entry.GetComponent<Image>();
        if (entryImage != null)
        {
            entryImage.raycastTarget = false;
        }

        
        var cardLayout = entry.GetComponent<VerticalLayoutGroup>();
        if (cardLayout != null)
        {
            cardLayout.spacing = 10;  
            cardLayout.padding = new RectOffset(15, 15, 15, 15);  
        }

        
        var nameRow = CreateHorizontalGroup(entry.transform, "NameRow");
        var nameRowLayout = nameRow.GetComponent<HorizontalLayoutGroup>();
        nameRowLayout.spacing = 12;  

        var lobbyNameText = CreateText("LobbyName", nameRow.transform, lobby.LobbyName, 16, ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);
        lobbyNameText.raycastTarget = false;  

        if (lobby.RequiresPassword)
        {
            CreateBadge(nameRow.transform, "[密]", ModernColors.Warning);
        }

        
        var infoRow = CreateHorizontalGroup(entry.transform, "InfoRow");
        var playerCountText = CreateText("PlayerCount", infoRow.transform, CoopLocalization.Get("ui.steam.playerCount", lobby.MemberCount, lobby.MaxMembers), 13, ModernColors.TextSecondary);
        playerCountText.raycastTarget = false;  

        CreateDivider(entry.transform);

        
        var joinButton = CreateModernButton("JoinBtn", entry.transform, CoopLocalization.Get("ui.steam.joinButton"), () =>
        {
            LoggerHelper.Log($"[MModUI] 加入按钮被点击！房间: {lobby.LobbyName}");
            AttemptSteamLobbyJoin(lobby);
        }, -1, ModernColors.Primary, 40, 15);

        
        var joinButtonImage = joinButton.GetComponent<Image>();
        if (joinButtonImage != null)
        {
            joinButtonImage.raycastTarget = true;  
            LoggerHelper.Log($"[MModUI] 创建加入按钮: {lobby.LobbyName}, raycastTarget={joinButtonImage.raycastTarget}");
        }
    }

    private void AttemptSteamLobbyJoin(SteamLobbyManager.LobbyInfo lobby)
    {
        LoggerHelper.Log($"[MModUI] 尝试加入Steam房间: {lobby.LobbyName} (ID: {lobby.LobbyId})");

        var manager = LobbyManager;
        if (manager == null)
        {
            LoggerHelper.LogError("[MModUI] Steam Lobby Manager 未初始化");
            SetStatusText("[!] " + CoopLocalization.Get("ui.steam.error.notInitialized"), ModernColors.Error);
            return;
        }

        
        if (!CheckCanConnect())
        {
            LoggerHelper.LogWarning("[MModUI] 关卡检查失败，无法加入房间");
            return;
        }

        LoggerHelper.Log("[MModUI] 关卡检查通过，准备加入房间");

        
        if (netManager == null || !netManager.IsRunning || IsServer || !networkStarted)
        {
            LoggerHelper.Log("[MModUI] 启动客户端网络模式");
            NetService.Instance?.StartNetwork(false);
        }

        var password = lobby.RequiresPassword ? _steamJoinPassword : string.Empty;
        LoggerHelper.Log($"[MModUI] 调用 TryJoinLobbyWithPassword, 需要密码: {lobby.RequiresPassword}");

        if (manager.TryJoinLobbyWithPassword(lobby.LobbyId, password, out var error))
        {
            LoggerHelper.Log($"[MModUI] 加入请求已发送，等待Steam响应");
            SetStatusText("[*] " + CoopLocalization.Get("ui.status.connecting"), ModernColors.Info);
            return;
        }

        
        LoggerHelper.LogError($"[MModUI] 加入房间失败: {error}");
        string errorMsg = error switch
        {
            SteamLobbyManager.LobbyJoinError.SteamNotInitialized => "[!] " + CoopLocalization.Get("ui.steam.error.notInitialized"),
            SteamLobbyManager.LobbyJoinError.LobbyMetadataUnavailable => "[!] " + CoopLocalization.Get("ui.steam.error.metadata"),
            SteamLobbyManager.LobbyJoinError.IncorrectPassword => "[!] " + CoopLocalization.Get("ui.steam.error.password"),
            _ => "[!] " + CoopLocalization.Get("ui.steam.error.generic")
        };

        SetStatusText(errorMsg, ModernColors.Error);
    }

    #endregion
}

#region 辅助组件


public class UIDragger : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    private RectTransform _rectTransform;
    private Vector2 _dragOffset;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out var localPoint);
        _dragOffset = _rectTransform.anchoredPosition - localPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out var localPoint))
            _rectTransform.anchoredPosition = localPoint + _dragOffset;
    }
}


public class ButtonHoverAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform _rectTransform;
    private Vector3 _originalScale;
    private Coroutine _scaleCoroutine;
    private bool _isPressed;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _originalScale = _rectTransform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isPressed)
        {
            if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScaleToWithEasing(Vector3.one * 1.05f, 0.2f, EaseOutBack));
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPressed = false;
        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        _scaleCoroutine = StartCoroutine(ScaleToWithEasing(_originalScale, 0.2f, EaseOutCubic));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        _scaleCoroutine = StartCoroutine(ScaleToWithEasing(Vector3.one * 0.95f, 0.1f, EaseOutCubic));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        _scaleCoroutine = StartCoroutine(ScaleToWithEasing(Vector3.one * 1.05f, 0.15f, EaseOutBack));
    }

    private IEnumerator ScaleToWithEasing(Vector3 targetScale, float duration, System.Func<float, float> easingFunction)
    {
        Vector3 startScale = _rectTransform.localScale;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            float easedT = easingFunction(t);
            _rectTransform.localScale = Vector3.Lerp(startScale, targetScale, easedT);
            yield return null;
        }

        _rectTransform.localScale = targetScale;
    }

    
    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    


}


public class InputFieldFocusHandler : MonoBehaviour
{
    public TMP_InputField inputField;
    public Outline outline;

    private void Start()
    {
        if (inputField != null)
        {
            inputField.onSelect.AddListener(OnSelect);
            inputField.onDeselect.AddListener(OnDeselect);
        }
    }

    private void OnSelect(string text)
    {
        if (outline != null)
            outline.effectColor = MModUI.ModernColors.InputFocus;
    }

    private void OnDeselect(string text)
    {
        if (outline != null)
            outline.effectColor = MModUI.ModernColors.InputBorder;
    }

    private void OnDestroy()
    {
        if (inputField != null)
        {
            inputField.onSelect.RemoveListener(OnSelect);
            inputField.onDeselect.RemoveListener(OnDeselect);
        }
    }
}



#endregion