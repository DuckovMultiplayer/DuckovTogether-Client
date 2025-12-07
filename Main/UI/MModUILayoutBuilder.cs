















using TMPro;
using UnityEngine;
using UnityEngine.UI;
using EscapeFromDuckovCoopMod.Net.Relay;

namespace EscapeFromDuckovCoopMod;




public class MModUILayoutBuilder
{
    private readonly MModUI _ui;
    private readonly MModUIComponents _components;

    public MModUILayoutBuilder(MModUI ui, MModUIComponents components)
    {
        _ui = ui;
        _components = components;
    }

    
    
    
    public void BuildMainPanel(Transform canvasTransform)
    {
        
        _components.MainPanel = _ui.CreateModernPanel("MainPanel", canvasTransform, new Vector2(1010, 784), new Vector2(260, 90));
        _ui.MakeDraggable(_components.MainPanel);

        var mainLayout = _components.MainPanel.AddComponent<VerticalLayoutGroup>();
        mainLayout.padding = new RectOffset(0, 0, 0, 0);
        mainLayout.spacing = 0;
        mainLayout.childForceExpandHeight = false;
        mainLayout.childControlHeight = false;

        
        BuildTitleBar(_components.MainPanel.transform);

        
        var contentArea = BuildContentArea(_components.MainPanel.transform);

        
        BuildLeftListContainer(contentArea.transform);

        
        BuildRightPanel(contentArea.transform);
    }

    
    
    
    private void BuildTitleBar(Transform parent)
    {
        var titleBar = _ui.CreateTitleBar(parent);
        _ui.CreateText("Title", titleBar.transform, CoopLocalization.Get("ui.window.title"), 24, MModUI.ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);

        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(titleBar.transform, false);
        var spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleWidth = 1;

        
        var indicatorObj = new GameObject("Indicator");
        indicatorObj.transform.SetParent(titleBar.transform, false);
        var indicatorLayout = indicatorObj.AddComponent<LayoutElement>();
        indicatorLayout.preferredWidth = 12;
        indicatorLayout.preferredHeight = 12;
        _components.ModeIndicator = indicatorObj.AddComponent<Image>();
        _components.ModeIndicator.color = MModUI.ModernColors.Success;

        _components.ModeText = _ui.CreateText("ModeText", titleBar.transform,
            _ui.IsServer ? CoopLocalization.Get("ui.mode.server") : CoopLocalization.Get("ui.mode.client"), 16, MModUI.ModernColors.TextSecondary, TextAlignmentOptions.Left);

        _ui.CreateIconButton("CloseBtn", titleBar.transform, "x", () =>
        {
            
            var ui = MModUI.Instance;
            if (ui != null && ui.showUI)
            {
                ui.showUI = false;
                var panel = UnityEngine.GameObject.Find("MainPanel");
                if (panel != null)
                {
                    ui.StartCoroutine(ui.AnimatePanel(panel, false));
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[MModUILayoutBuilder] MainPanel not found");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[MModUILayoutBuilder] UI not available (Instance={ui != null}, showUI={ui?.showUI})");
            }
        }, 36, MModUI.ModernColors.Error);
    }

    
    
    
    private GameObject BuildContentArea(Transform parent)
    {
        var contentArea = new GameObject("ContentArea");
        contentArea.transform.SetParent(parent, false);
        var contentLayout = contentArea.AddComponent<HorizontalLayoutGroup>();
        contentLayout.padding = new RectOffset(20, 20, 15, 15);
        contentLayout.spacing = 20;
        contentLayout.childForceExpandWidth = false;
        contentLayout.childForceExpandHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;

        var contentLayoutElement = contentArea.AddComponent<LayoutElement>();
        contentLayoutElement.flexibleWidth = 1;
        contentLayoutElement.flexibleHeight = 1;

        return contentArea;
    }

    
    
    
    private void BuildLeftListContainer(Transform parent)
    {
        var leftListContainer = new GameObject("LeftListContainer");
        leftListContainer.transform.SetParent(parent, false);
        var leftListLayout = leftListContainer.AddComponent<VerticalLayoutGroup>();
        leftListLayout.padding = new RectOffset(0, 0, 0, 0);
        leftListLayout.spacing = 0;
        leftListLayout.childForceExpandHeight = false;
        leftListLayout.childControlHeight = true;

        var leftListLayoutElement = leftListContainer.AddComponent<LayoutElement>();
        leftListLayoutElement.preferredWidth = 520;
        leftListLayoutElement.minHeight = 480;

        BuildUnifiedServerListArea(leftListContainer.transform);
    }

    private void BuildUnifiedServerListArea(Transform parent)
    {
        var serverListArea = new GameObject("ServerListArea");
        serverListArea.transform.SetParent(parent, false);
        var listLayout = serverListArea.AddComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(0, 0, 0, 0);
        listLayout.spacing = 12;
        listLayout.childForceExpandHeight = false;
        listLayout.childControlHeight = true;
        var listLayoutElement = serverListArea.AddComponent<LayoutElement>();
        listLayoutElement.flexibleWidth = 1;
        listLayoutElement.flexibleHeight = 1;

        _components.DirectServerListArea = serverListArea;
        _components.SteamServerListArea = serverListArea;

        var lanHeader = _ui.CreateModernCard(serverListArea.transform, "LANHeader");
        var lanHeaderLayout = lanHeader.GetComponent<LayoutElement>();
        lanHeaderLayout.preferredHeight = 60;
        lanHeaderLayout.minHeight = 60;
        lanHeaderLayout.flexibleHeight = 0;

        var lanHeaderGroup = _ui.CreateHorizontalGroup(lanHeader.transform, "HeaderGroup");
        lanHeaderGroup.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);

        _ui.CreateText("ListTitle", lanHeaderGroup.transform, CoopLocalization.Get("ui.hostList.title"), 20, MModUI.ModernColors.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);

        _components.OnlineRoomCountText = _ui.CreateText("RoomCount", lanHeaderGroup.transform, "在线房间: 0", 14, MModUI.ModernColors.TextSecondary);

        var lanHeaderSpacer = new GameObject("Spacer");
        lanHeaderSpacer.transform.SetParent(lanHeaderGroup.transform, false);
        var lanHeaderSpacerLayout = lanHeaderSpacer.AddComponent<LayoutElement>();
        lanHeaderSpacerLayout.flexibleWidth = 1;
        
        _ui.CreateModernButton("SelectNodeBtn", lanHeaderGroup.transform, "连接节点", () =>
        {
            var lobbyUI = OnlineLobbyUI.Instance;
            if (lobbyUI != null)
            {
                lobbyUI.OpenNodeSelector();
            }
        }, 90, MModUI.ModernColors.Primary, 38, 14);

        _ui.CreateModernButton("RefreshBtn", lanHeaderGroup.transform, CoopLocalization.Get("ui.steam.refresh"), () =>
        {
            var relayManager = RelayServerManager.Instance;
            if (relayManager != null && relayManager.IsConnectedToRelay)
            {
                relayManager.RequestRoomList();
            }
            
            if (_ui.LobbyManager != null && _ui.TransportMode == NetworkTransportMode.SteamP2P)
            {
                _ui.LobbyManager.RequestLobbyList();
            }
            else if (_ui.Service != null && !_ui.IsServer)
            {
                CoopTool.SendBroadcastDiscovery();
            }
        }, 90, MModUI.ModernColors.Primary, 38, 14);

        var onlineScrollView = _ui.CreateModernScrollView("OnlineRoomListScroll", serverListArea.transform, 445);
        var onlineScrollLayout = onlineScrollView.GetComponent<LayoutElement>();
        onlineScrollLayout.flexibleHeight = 1;
        _components.OnlineRoomListContent = onlineScrollView.transform.Find("Viewport/Content");
        _components.HostListContent = _components.OnlineRoomListContent;
        _components.SteamLobbyListContent = _components.OnlineRoomListContent;

        var passCard = _ui.CreateModernCard(serverListArea.transform, "JoinPassCard");
        var passCardLayout = passCard.GetComponent<LayoutElement>();
        passCardLayout.preferredHeight = 80;
        passCardLayout.minHeight = 80;

        var passRow = _ui.CreateHorizontalGroup(passCard.transform, "JoinPassRow");
        _ui.CreateText("JoinPassLabel", passRow.transform, "房间密码", 14, MModUI.GlassTheme.TextSecondary).gameObject.GetComponent<LayoutElement>().preferredWidth = 80;
        var joinPassInput = _ui.CreateModernInputField("JoinPass", passRow.transform, "输入房间密码（如需要）", _ui._onlineJoinPassword);
        joinPassInput.contentType = TMP_InputField.ContentType.Password;
        joinPassInput.onValueChanged.AddListener(value => _ui._onlineJoinPassword = value);

        var lanStatusBar = _ui.CreateStatusBar(serverListArea.transform);
        _components.StatusText = _ui.CreateText("Status", lanStatusBar.transform, $"[*] {_ui.status}", 14, MModUI.ModernColors.TextSecondary);

        var lanStatusSpacer = new GameObject("Spacer");
        lanStatusSpacer.transform.SetParent(lanStatusBar.transform, false);
        var lanStatusSpacerLayout = lanStatusSpacer.AddComponent<LayoutElement>();
        lanStatusSpacerLayout.flexibleWidth = 1;

        _ui.CreateText("Hint", lanStatusBar.transform, CoopLocalization.Get("ui.hint.toggleUI", "="), 12, MModUI.ModernColors.TextTertiary, TextAlignmentOptions.Right);
    }

    
    
    
    private void BuildRightPanel(Transform parent)
    {
        
        var rightPanel = new GameObject("RightPanel");
        rightPanel.transform.SetParent(parent, false);

        
        var rightLayout = rightPanel.AddComponent<VerticalLayoutGroup>();
        rightLayout.padding = new RectOffset(0, 0, 0, 0);
        rightLayout.spacing = 0;
        rightLayout.childForceExpandHeight = true;
        rightLayout.childForceExpandWidth = true;
        rightLayout.childControlHeight = true;
        rightLayout.childControlWidth = true;

        var rightLayoutElement = rightPanel.AddComponent<LayoutElement>();
        rightLayoutElement.preferredWidth = 320;  
        rightLayoutElement.flexibleHeight = 1;

        
        var scrollView = _ui.CreateModernScrollView("RightPanelScroll", rightPanel.transform, 660);
        var scrollLayout = scrollView.GetComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1;
        scrollLayout.flexibleWidth = 1;

        
        var scrollContent = scrollView.transform.Find("Viewport/Content");

        
        BuildTransportModeSelector(scrollContent);

        
        BuildModeToggleCard(scrollContent);

        
        BuildServerInfoCard(scrollContent);

        
        BuildDirectModePanel(scrollContent);

        
        BuildSteamModePanel(scrollContent);

        
        BuildActionsCard(scrollContent);
    }

    
    
    
    private void BuildTransportModeSelector(Transform parent)
    {
        var transportCard = _ui.CreateModernCard(parent, "TransportModeCard");
        var transportCardLayout = transportCard.GetComponent<LayoutElement>();
        transportCardLayout.preferredHeight = 95;  
        transportCardLayout.minHeight = 95;

        _ui.CreateSectionHeader(transportCard.transform, CoopLocalization.Get("ui.transport.label"));

        var transportButtonsRow = _ui.CreateHorizontalGroup(transportCard.transform, "TransportButtons");
        var transportRowLayout = transportButtonsRow.GetComponent<HorizontalLayoutGroup>();
        transportRowLayout.spacing = 10;
        transportRowLayout.padding = new RectOffset(0, 0, 0, 0);

        var directBtn = _ui.CreateModernButton("DirectMode", transportButtonsRow.transform, CoopLocalization.Get("ui.transport.mode.direct"),
            () => _ui.OnTransportModeChanged(NetworkTransportMode.Direct),
            -1, _ui.TransportMode == NetworkTransportMode.Direct ? MModUI.ModernColors.Primary : MModUI.GlassTheme.ButtonBg, 40, 14);

        var steamBtn = _ui.CreateModernButton("SteamMode", transportButtonsRow.transform, CoopLocalization.Get("ui.transport.mode.steam"),
            () => _ui.OnTransportModeChanged(NetworkTransportMode.SteamP2P),
            -1, _ui.TransportMode == NetworkTransportMode.SteamP2P ? MModUI.ModernColors.Primary : MModUI.GlassTheme.ButtonBg, 40, 14);
    }

    
    
    
    private void BuildModeToggleCard(Transform parent)
    {
        var modeCard = _ui.CreateModernCard(parent, "ModeCard");
        var modeCardLayout = modeCard.GetComponent<LayoutElement>();
        modeCardLayout.preferredHeight = 120;  
        modeCardLayout.minHeight = 120;

        _ui.CreateSectionHeader(modeCard.transform, CoopLocalization.Get("ui.server.management"));
        _components.ModeInfoText = _ui.CreateText("ModeInfo", modeCard.transform,
            _ui.IsServer ? CoopLocalization.Get("ui.server.hint.waiting") : CoopLocalization.Get("ui.client.hint.browse"),
            14, MModUI.ModernColors.TextSecondary);

        _components.ModeToggleButton = _ui.CreateModernButton("ToggleMode", modeCard.transform,
            _ui.IsServer ? CoopLocalization.Get("ui.server.close") : CoopLocalization.Get("ui.server.create"),
            _ui.OnToggleServerMode, -1, _ui.IsServer ? MModUI.ModernColors.Error : MModUI.ModernColors.Success, 45, 17);

        
        _components.ModeToggleButtonText = _components.ModeToggleButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    
    
    
    private void BuildServerInfoCard(Transform parent)
    {
        var serverInfoCard = _ui.CreateModernCard(parent, "ServerInfoCard");
        var serverInfoCardLayout = serverInfoCard.GetComponent<LayoutElement>();
        serverInfoCardLayout.preferredHeight = 140;  
        serverInfoCardLayout.minHeight = 140;

        _ui.CreateSectionHeader(serverInfoCard.transform, CoopLocalization.Get("ui.server.info"));
        _components.ServerPortText = _ui.CreateInfoRow(serverInfoCard.transform, CoopLocalization.Get("ui.server.port"), $"{_ui.port}");
        _components.ConnectionCountText = _ui.CreateInfoRow(serverInfoCard.transform, CoopLocalization.Get("ui.server.connections"), "0");
        _ui.CreateInfoRow(serverInfoCard.transform, CoopLocalization.Get("ui.playerStatus.latency"), "0 ms");
    }

    
    
    
    private void BuildDirectModePanel(Transform parent)
    {
        _components.DirectModePanel = new GameObject("DirectModePanel");
        _components.DirectModePanel.transform.SetParent(parent, false);
        var directLayout = _components.DirectModePanel.AddComponent<VerticalLayoutGroup>();
        directLayout.spacing = 15;
        directLayout.childForceExpandHeight = false;
        directLayout.childControlHeight = true;
        var directLayoutElement = _components.DirectModePanel.AddComponent<LayoutElement>();
        directLayoutElement.flexibleWidth = 1;

        
        var connectCard = _ui.CreateModernCard(_components.DirectModePanel.transform, "ConnectCard");
        var connectCardLayout = connectCard.GetComponent<LayoutElement>();
        connectCardLayout.preferredHeight = 220;  
        connectCardLayout.minHeight = 220;

        _ui.CreateSectionHeader(connectCard.transform, CoopLocalization.Get("ui.manualConnect.title"));
        _components.IpInputField = _ui.CreateModernInputField("IPInput", connectCard.transform, CoopLocalization.Get("ui.manualConnect.ip"), _ui.manualIP);
        _components.IpInputField.onValueChanged.AddListener((value) => _ui.manualIP = value);

        _components.PortInputField = _ui.CreateModernInputField("PortInput", connectCard.transform, CoopLocalization.Get("ui.manualConnect.port"), _ui.manualPort);
        _components.PortInputField.onValueChanged.AddListener((value) => _ui.manualPort = value);

        _ui.CreateModernButton("ManualConnect", connectCard.transform, CoopLocalization.Get("ui.manualConnect.button"), _ui.OnManualConnect, -1, MModUI.ModernColors.Primary, 45, 17);

        _ui.CreateText("ConnectHint", connectCard.transform, CoopLocalization.Get("ui.manualConnect.hint"), 12, MModUI.ModernColors.TextTertiary, TextAlignmentOptions.Center);
    }

    
    
    
    private void BuildSteamModePanel(Transform parent)
    {
        _components.SteamModePanel = new GameObject("SteamModePanel");
        _components.SteamModePanel.transform.SetParent(parent, false);
        var steamLayout = _components.SteamModePanel.AddComponent<VerticalLayoutGroup>();
        steamLayout.spacing = 15;
        steamLayout.childForceExpandHeight = false;
        steamLayout.childControlHeight = true;
        var steamLayoutElement = _components.SteamModePanel.AddComponent<LayoutElement>();
        steamLayoutElement.flexibleWidth = 1;

        _ui.CreateSteamControlPanel(_components.SteamModePanel.transform);
    }

    
    
    
    private void BuildActionsCard(Transform parent)
    {
        var actionsCard = _ui.CreateModernCard(parent, "ActionsCard");
        var actionsCardLayout = actionsCard.GetComponent<LayoutElement>();
        actionsCardLayout.preferredHeight = 450;  
        actionsCardLayout.minHeight = 450;

        _ui.CreateSectionHeader(actionsCard.transform, CoopLocalization.Get("ui.actions.quickActions"));

        _ui.CreateModernButton("PlayerStatus", actionsCard.transform, CoopLocalization.Get("ui.playerStatus.toggle", _ui.togglePlayerStatusKey), () =>
        {
            _ui.showPlayerStatusWindow = !_ui.showPlayerStatusWindow;
            _ui.StartCoroutine(_ui.AnimatePanel(_components.PlayerStatusPanel, _ui.showPlayerStatusWindow));
        }, -1, MModUI.ModernColors.Info, 40, 15);

        _ui.CreateModernButton("VoiceSettings", actionsCard.transform, CoopLocalization.Get("ui.voice.openSettings"), () =>
        {
            Debug.Log("[MModUI] 语音设置按钮被点击");
            _ui.showVoiceSettingsWindow = !_ui.showVoiceSettingsWindow;
        }, -1, MModUI.ModernColors.Success, 40, 15);

        _ui.CreateModernButton("DebugLootBoxes", actionsCard.transform, CoopLocalization.Get("ui.debug.printLootBoxes"), _ui.DebugPrintLootBoxes, -1, MModUI.ModernColors.Warning, 40, 15);

        _ui.CreateModernButton("DebugNetworkState", actionsCard.transform, "Debug: 网络状态 JSON", _ui.DebugPrintRemoteCharacters, -1, MModUI.ModernColors.Warning, 40, 15);

        
        _ui.CreateModernButton("CopyPlayerDB", actionsCard.transform, "复制玩家数据库 JSON", _ui.CopyPlayerDatabaseToClipboard, -1, MModUI.ModernColors.Success, 40, 15);

        
        var jsonInputCard = _ui.CreateModernCard(actionsCard.transform, "JSONInputCard");
        _ui.CreateSectionHeader(jsonInputCard.transform, "JSON 消息发送");
        _components.JsonInputField = _ui.CreateModernInputField("JSONInput", jsonInputCard.transform, "输入 JSON 消息...", "");
        _ui.CreateModernButton("SendJSON", jsonInputCard.transform, "发送 JSON", _ui.SendJsonMessage, -1, MModUI.ModernColors.Primary, 40, 15);
    }
}

