
















#define USE_NEW_OP_NETMESSAGECONSUMER

using Duckov.UI;
using EscapeFromDuckovCoopMod.Net;
using EscapeFromDuckovCoopMod.Utils; 
using EscapeFromDuckovCoopMod.Utils.Logger.Core;
using EscapeFromDuckovCoopMod.Utils.Logger.Tools;
using EscapeFromDuckovCoopMod.Utils.NetHelper;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using UnityEngine.SceneManagement;
using static EscapeFromDuckovCoopMod.LootNet;
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

internal static class ServerTuning
{
    
    public const float RemoteMeleeCharScale = 1.00f; 
    public const float RemoteMeleeEnvScale = 1.5f; 

    
    public const bool UseNullAttackerForEnv = true;
}


public partial class ModBehaviourF : MonoBehaviour
{
    static ModBehaviourF()
    {
        Debug.Log("##########################################################");
        Debug.Log("##### [MOD_DLL_LOADED] 新DLL已加载 - 2025-11-17 09:30 ####");
        Debug.Log($"##### BuildInfo.ModVersion = {BuildInfo.ModVersion} ####");
        Debug.Log("##########################################################");
    }

    private const float SELF_ACCEPT_WINDOW = 0.30f;
    private const float EnsureRemoteInterval = 1.0f; 
    private const float ENV_SYNC_INTERVAL = 1.0f; 
    private const float AI_TF_INTERVAL = 0.05f;
    private const float AI_ANIM_INTERVAL = 0.10f; 
    private const float AI_NAMEICON_INTERVAL = 10f;

    private const float SELF_MUTE_SEC = 0.10f;
    public static ModBehaviourF Instance; 

    public static CustomFaceSettingData localPlayerCustomFace;

    public static bool LogAiHpDebug = false; 

    public static bool LogAiLoadoutDebug = true;

    
    private static readonly AccessTools.FieldRef<CharacterRandomPreset, bool> FR_UsePlayerPreset =
        AccessTools.FieldRefAccess<CharacterRandomPreset, bool>("usePlayerPreset");

    private static readonly AccessTools.FieldRef<
        CharacterRandomPreset,
        CustomFacePreset
    > FR_FacePreset = AccessTools.FieldRefAccess<CharacterRandomPreset, CustomFacePreset>(
        "facePreset"
    );

    private static readonly AccessTools.FieldRef<
        CharacterRandomPreset,
        CharacterModel
    > FR_CharacterModel = AccessTools.FieldRefAccess<CharacterRandomPreset, CharacterModel>(
        "characterModel"
    );

    private static readonly AccessTools.FieldRef<
        CharacterRandomPreset,
        CharacterIconTypes
    > FR_IconType = AccessTools.FieldRefAccess<CharacterRandomPreset, CharacterIconTypes>(
        "characterIconType"
    );

    public static readonly Dictionary<int, Pending> map = new();

    private static Transform _fallbackMuzzleAnchor;
    public float broadcastTimer;
    public float syncTimer;

    public bool Pausebool;
    public int _clientLootSetupDepth;

    public GameObject aiTelegraphFx;
    public DamageInfo _lastDeathInfo;

    
    public bool Client_ForceShowAllRemoteAI = true;

    
    private readonly Dictionary<string, string> _cliPendingFace = new();

    
    private readonly Dictionary<int, (Vector3 pos, Vector3 dir)> _lastAiSent = new();

    
    private readonly Dictionary<int, AiAnimState> _pendingAiAnims = new();

    private readonly Queue<(int id, Vector3 p, Vector3 f)> _pendingAiTrans = new();

    private readonly Dictionary<
        int,
        (int capacity, List<(int pos, ItemSnapshot snap)>)
    > _pendingLootStates = new();

    private readonly KeyCode readyKey = KeyCode.J;

    
    private float _aiAnimTimer;

    private float _aiNameIconTimer;

    private float _aiTfTimer;

    private float _ensureRemoteTick = 0f;
    private bool _envReqOnce = false;
    private string _envReqSid;

    private float _envSyncTimer;

    private int _spectateIdx = -1;
    private float _spectateNextSwitchTime;

    private bool isinit; 

    private bool isinit2;
    private NetService Service => NetService.Instance;
    [System.Obsolete("Client is never server. Headless server handles all server logic.")]
    public bool IsServer => false;
    public NetManager netManager => Service?.netManager;
    public NetDataWriter writer => Service?.writer;
    public NetPeer connectedPeer => Service?.connectedPeer;
    public PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    public bool networkStarted => Service != null && Service.networkStarted;
    public string manualIP => Service?.manualIP;
    public List<string> hostList => Service?.hostList;
    public HashSet<string> hostSet => Service?.hostSet;
    public bool isConnecting => Service != null && Service.isConnecting;
    public string manualPort => Service?.manualPort;
    public string status => Service?.status;
    public int port => Service?.port ?? 0;
    public float broadcastInterval => Service?.broadcastInterval ?? 5f;
    public float syncInterval => Service?.syncInterval ?? 0.015f; 

    public Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    public Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
    public Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;
    public Dictionary<string, PlayerStatus> clientPlayerStatuses => Service?.clientPlayerStatuses;
    public bool ClientLootSetupActive => networkStarted && _clientLootSetupDepth > 0;

    public bool IsClient => networkStarted;

    

    private bool _hasLoggedVersion = false; 
    private static bool _modBehaviourCreationAttempted = false; 
    private static int _instanceCount = 0; 

    private void Awake()
    {
        _instanceCount++;
        Debug.Log("##########################################################");
        Debug.Log($"[ModBehaviourF] Awake() #{_instanceCount} - Version: {BuildInfo.ModVersion}");
        Debug.Log("##########################################################");
        
        
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[ModBehaviourF] 检测到重复实例 #{_instanceCount}，销毁重复的GameObject");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;

        var syncObj = new GameObject("AIInstanceSync");
        syncObj.AddComponent<Main.AI.AIInstanceSync>();
        DontDestroyOnLoad(syncObj);

        var voiceObj = new GameObject("VoiceManager");
        var voiceManager = voiceObj.AddComponent<Main.Voice.VoiceManager>();
        voiceManager.Init();
        DontDestroyOnLoad(voiceObj);

        var voiceUIObj = new GameObject("VoiceOverlayUI");
        voiceUIObj.AddComponent<Main.UI.VoiceOverlayUI>();
        DontDestroyOnLoad(voiceUIObj);

        LoggerHelper.Log(LogLevel.None, "【测试】None 等级日志 - 无颜色");
        LoggerHelper.LogInfo("【测试】Info 等级日志 - 信息颜色");
        LoggerHelper.LogWarning("【测试】Warning 等级日志 - 警告颜色");
        LoggerHelper.LogError("【测试】Error 等级日志 - 错误颜色");
        LoggerHelper.LogFatal("【测试】Fatal 等级日志 - 致命错误颜色");

        InitializePlayerDatabase();
    }

    
    
    
    private void InitializePlayerDatabase()
    {
        try
        {
            var playerDb = Utils.Database.PlayerInfoDatabase.Instance;

            
            if (Steamworks.SteamAPI.IsSteamRunning())
            {
                var steamId = Steamworks.SteamUser.GetSteamID().ToString();
                var playerName = Steamworks.SteamFriends.GetPersonaName();

                
                var avatarHandle = Steamworks.SteamFriends.GetLargeFriendAvatar(Steamworks.SteamUser.GetSteamID());
                string avatarUrl = null;

                if (avatarHandle > 0)
                {
                    avatarUrl = $"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/{avatarHandle}";
                }

                
                playerDb.AddOrUpdatePlayer(
                    steamId: steamId,
                    playerName: playerName,
                    avatarUrl: avatarUrl,
                    isLocal: true,
                    lastUpdate: DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                );

                Debug.Log($"[PlayerDB] 本地玩家信息已缓存:");
                Debug.Log($"  SteamID: {steamId}");
                Debug.Log($"  名字: {playerName}");
                Debug.Log($"  头像URL: {avatarUrl ?? "未获取"}");

                
                var json = playerDb.ExportToJsonWithStats(indented: true);
                Debug.Log($"[PlayerDB] 玩家数据库:\n{json}");
            }
            else
            {
                Debug.LogWarning("[PlayerDB] Steam 未初始化，无法获取玩家信息");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerDB] 初始化玩家数据库失败: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void Update()
    {
        
        if (!_hasLoggedVersion)
        {
            _hasLoggedVersion = true;
            Debug.Log("==========================================================");
            Debug.Log($"[ModBehaviourF] *** FIRST UPDATE *** DLL Version: {BuildInfo.ModVersion}");
            Debug.Log("==========================================================");
            
            
            bool needsReinit = false;
            
            if (MModUI.Instance != null)
            {
                if (MModUI.Instance.Canvas == null)
                {
                    needsReinit = true;
                }
                else
                {
                    Debug.Log($"[ModBehaviourF] MModUI is fully initialized and ready!");
                }
                
                if (needsReinit)
                {
                    Debug.Log("[ModBehaviourF] Calling MModUI.Instance.Init() now...");
                    try
                    {
                        MModUI.Instance.Init();
                        Debug.Log("[ModBehaviourF] MModUI.Init() completed successfully!");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ModBehaviourF] MModUI.Init() FAILED: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
            else if (!_modBehaviourCreationAttempted)
            {
                _modBehaviourCreationAttempted = true;
                Debug.LogWarning("[ModBehaviourF] MModUI.Instance is null! Main mod initialization may have been skipped.");
                Debug.LogWarning("[ModBehaviourF] Attempting manual initialization...");
                
                try
                {
                    var modGO = GameObject.Find("[COOP_ModBehaviour]");
                    if (modGO == null)
                    {
                        Debug.Log("[ModBehaviourF] Creating ModBehaviour GameObject (from Update)");
                        modGO = new GameObject("[COOP_ModBehaviour]");
                        DontDestroyOnLoad(modGO);
                        modGO.AddComponent<ModBehaviour>();
                        Debug.Log("[ModBehaviourF] ModBehaviour created, OnEnable will be called automatically");
                    }
                    else
                    {
                        Debug.Log("[ModBehaviourF] ModBehaviour GameObject already exists but MModUI is still null");
                        Debug.LogWarning("[ModBehaviourF] This suggests ModBehaviour.OnEnable() failed or didn't run");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ModBehaviourF] Manual initialization failed: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        if (!networkStarted)
            return;

        if (CharacterMainControl.Main != null && !isinit)
        {
            isinit = true;
            Traverse
                .Create(CharacterMainControl.Main.EquipmentController)
                .Field<Slot>("armorSlot")
                .Value.onSlotContentChanged += LocalPlayerManager
                .Instance
                .ModBehaviour_onSlotContentChanged;
            Traverse
                .Create(CharacterMainControl.Main.EquipmentController)
                .Field<Slot>("helmatSlot")
                .Value.onSlotContentChanged += LocalPlayerManager
                .Instance
                .ModBehaviour_onSlotContentChanged;
            Traverse
                .Create(CharacterMainControl.Main.EquipmentController)
                .Field<Slot>("faceMaskSlot")
                .Value.onSlotContentChanged += LocalPlayerManager
                .Instance
                .ModBehaviour_onSlotContentChanged;
            Traverse
                .Create(CharacterMainControl.Main.EquipmentController)
                .Field<Slot>("backpackSlot")
                .Value.onSlotContentChanged += LocalPlayerManager
                .Instance
                .ModBehaviour_onSlotContentChanged;
            Traverse
                .Create(CharacterMainControl.Main.EquipmentController)
                .Field<Slot>("headsetSlot")
                .Value.onSlotContentChanged += LocalPlayerManager
                .Instance
                .ModBehaviour_onSlotContentChanged;

            CharacterMainControl.Main.OnHoldAgentChanged += LocalPlayerManager
                .Instance
                .Main_OnHoldAgentChanged;
        }

        
        if (Pausebool)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (CharacterMainControl.Main == null)
            isinit = false;

        

        if (networkStarted)
        {
            netManager.PollEvents();
            SceneNet.Instance.TrySendSceneReadyOnce();
            if (!isinit2)
            {
                isinit2 = true;
                HealthM.Instance.Client_ReportSelfHealth_IfReadyOnce();
            }

            

            if (!isConnecting)
            {
                broadcastTimer += Time.deltaTime;
                if (broadcastTimer >= broadcastInterval)
                {
                    CoopTool.SendBroadcastDiscovery();
                    broadcastTimer = 0f;
                }
            }

            syncTimer += Time.deltaTime;
            if (syncTimer >= syncInterval)
            {
                SendLocalPlayerStatus.Instance.SendPositionUpdate();
                SendLocalPlayerStatus.Instance.SendAnimationStatus();
                syncTimer = 0f;

                
                
                
                
                
                
                
                
                
                
                
                
                
                
                
            }

            if (!string.IsNullOrEmpty(SceneNet.Instance._sceneReadySidSent)
                && _envReqSid != SceneNet.Instance._sceneReadySidSent)
            {
                _envReqSid = SceneNet.Instance._sceneReadySidSent; 
                COOPManager.Weather.Client_RequestEnvSync(); 
            }


            var burst = 64; 
            while (AITool._aiSceneReady && _pendingAiTrans.Count > 0 && burst-- > 0)
            {
                var (id, p, f) = _pendingAiTrans.Dequeue();
                AITool.ApplyAiTransform(id, p, f);
            }

            if (NetService.Instance.netManager != null)
            {
                if (!SteamP2PLoader.Instance._isOptimized && SteamP2PLoader.Instance.UseSteamP2P)
                {
                    NetService.Instance.netManager.UpdateTime = 1;
                    SteamP2PLoader.Instance._isOptimized = true;
                    Debug.Log("[SteamP2P扩展] ✓ LiteNetLib网络线程已优化 (1ms 更新周期)");
                }
            }
        }


        LocalPlayerManager.Instance.UpdatePlayerStatuses();
        LocalPlayerManager.Instance.UpdateRemoteCharacters();

        

        COOPManager.GrenadeM.ProcessPendingGrenades();

        if (CoopTool._cliSelfHpPending && CharacterMainControl.Main != null)
        {
            HealthM.Instance.ApplyHealthAndEnsureBar(
                CharacterMainControl.Main.gameObject,
                CoopTool._cliSelfHpMax,
                CoopTool._cliSelfHpCur
            );
            CoopTool._cliSelfHpPending = false;
        }

        CoopTool.Client_ApplyPendingSelfIfReady();
        HealthM.Instance.Client_ReportSelfHealth_IfReadyOnce();

        
        if (SceneNet.Instance.sceneVoteActive)
        {
            if (Input.GetKeyDown(readyKey))
            {
                Debug.Log($"[VOTE-KEY] 检测到按键 {readyKey}，当前准备状态: {SceneNet.Instance.localReady}");
                SceneNet.Instance.localReady = !SceneNet.Instance.localReady;
                SceneVoteMessage.Client_ToggleReady(SceneNet.Instance.localReady);
            }
        }

        if (networkStarted)
        {
            SceneNet.Instance.TrySendSceneReadyOnce();
            if (_envReqSid != SceneNet.Instance._sceneReadySidSent)
            {
                _envReqSid = SceneNet.Instance._sceneReadySidSent;
                COOPManager.Weather.Client_RequestEnvSync();
            }

            if (!HealthTool._cliInitHpReported)
                HealthM.Instance.Client_ReportSelfHealth_IfReadyOnce();

            HealthTool.Client_HookSelfHealth();
        }

        if (Spectator.Instance._spectatorActive)
        {
            ClosureView.Instance.gameObject.SetActive(false);
            
            Spectator.Instance._spectateList = Spectator
                .Instance._spectateList.Where(c =>
                {
                    if (!LocalPlayerManager.Instance.IsAlive(c))
                        return false;

                    var mySceneId = localPlayerStatus != null ? localPlayerStatus.SceneId : null;
                    if (string.IsNullOrEmpty(mySceneId))
                        LocalPlayerManager.Instance.ComputeIsInGame(out mySceneId);

                    
                    string peerScene = null;
                    foreach (var kv in clientRemoteCharacters)
                        if (kv.Value != null && kv.Value.GetComponent<CharacterMainControl>() == c)
                        {
                            if (clientPlayerStatuses.TryGetValue(kv.Key, out var st))
                                peerScene = st?.SceneId;
                            break;
                        }

                    return Spectator.AreSameMap(mySceneId, peerScene);
                })
                .ToList();

            
            if (Spectator.Instance._spectateList.Count == 0 || SceneM.AllPlayersDead())
            {
                Spectator.Instance.EndSpectatorAndShowClosure();
                return;
            }

            if (_spectateIdx < 0 || _spectateIdx >= Spectator.Instance._spectateList.Count)
                _spectateIdx = 0;

            
            if (
                !LocalPlayerManager.Instance.IsAlive(Spectator.Instance._spectateList[_spectateIdx])
            )
                Spectator.Instance.SpectateNext();

            
            if (Time.unscaledTime >= _spectateNextSwitchTime)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Spectator.Instance.SpectateNext();
                    _spectateNextSwitchTime = Time.unscaledTime + 0.15f;
                }

                if (Input.GetMouseButtonDown(1))
                {
                    Spectator.Instance.SpectatePrev();
                    _spectateNextSwitchTime = Time.unscaledTime + 0.15f;
                }
            }
        }
    }

    private void OnEnable()
    {
        Debug.Log("##########################################################");
        Debug.Log($"[ModBehaviourF] OnEnable() START - DLL Version: {BuildInfo.ModVersion}");
        Debug.Log("##########################################################");
        
        SceneManager.sceneLoaded += OnSceneLoaded_IndexDestructibles;
        Debug.Log("[ModBehaviourF] Registered SceneLoaded handler");
        SceneManager.sceneUnloaded += OnSceneUnloaded_Cleanup; 
        Debug.Log("[ModBehaviourF] Registered SceneUnloaded handler");
        LevelManager.OnAfterLevelInitialized += LevelManager_OnAfterLevelInitialized;
        Debug.Log("[ModBehaviourF] Registered OnAfterLevelInitialized handler");
        LevelManager.OnLevelInitialized += OnLevelInitialized_IndexDestructibles;
        Debug.Log("[ModBehaviourF] Registered OnLevelInitialized (IndexDestructibles) handler");

        SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        Debug.Log("[ModBehaviourF] Registered SceneLoaded (second) handler");
        LevelManager.OnLevelInitialized += LevelManager_OnLevelInitialized;
        Debug.Log("[ModBehaviourF] Registered LevelManager_OnLevelInitialized handler");
        Debug.Log("[ModBehaviourF] ========== OnEnable() COMPLETE ==========");
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded_IndexDestructibles;
        SceneManager.sceneUnloaded -= OnSceneUnloaded_Cleanup; 
        LevelManager.OnLevelInitialized -= OnLevelInitialized_IndexDestructibles;
        

        SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
        LevelManager.OnLevelInitialized -= LevelManager_OnLevelInitialized;
    }

    private void OnDestroy()
    {
        NetService.Instance.StopNetwork();
    }

    
    
    
    
    private void OnSceneUnloaded_Cleanup(Scene scene)
    {
        Debug.Log($"####### DLL_VERSION: {BuildInfo.ModVersion} #######");
        Debug.Log($"[MOD] ========== 场景卸载清理开始: {scene.name} ==========");

        try
        {
            
            Debug.Log("[MOD-Cleanup] 清理 AI 数据...");
            AITool.ResetAiSerials();
            if (COOPManager.AIHandle != null)
            {
               
            }

            
            Debug.Log("[MOD-Cleanup] 清理战利品数据...");
            if (LootManager.Instance != null)
            {
                LootManager.Instance.ClearCaches();
                LootManager.Instance._srvLootByUid.Clear();
                LootManager.Instance._pendingLootStatesByUid.Clear();
                LootManager.Instance._srvLootMuteUntil.Clear();
                LootManager.Instance._cliLootByUid.Clear();
                LootManager.Instance._cliPendingTake.Clear();
            }

            
            LootboxDetectUtil.ClearInventoryCaches();

            
            Debug.Log("[MOD-Cleanup] 清理掉落物品...");
            ItemTool.serverDroppedItems.Clear();
            ItemTool.clientDroppedItems.Clear();

            
            Debug.Log("[MOD-Cleanup] 清理游戏对象缓存...");
            if (Utils.GameObjectCacheManager.Instance != null)
            {
                Utils.GameObjectCacheManager.Instance.ClearAllCaches();
            }

            
            Debug.Log("[MOD-Cleanup] 清空异步消息队列...");
            if (Utils.AsyncMessageQueue.Instance != null)
            {
                Utils.AsyncMessageQueue.Instance.ClearQueue();
                Utils.AsyncMessageQueue.Instance.DisableBulkMode();
            }

            
            Debug.Log("[MOD-Cleanup] 清理可破坏物数据...");
            if (COOPManager.destructible != null)
            {
                COOPManager.destructible.ClearDestructibles();
            }

            
            if (!string.IsNullOrEmpty(scene.name) && scene.name != "MainMenu" && scene.name != "LoadingScreen")
            {
                Debug.Log("[MOD-Cleanup] 清理远程玩家数据...");
                if (clientRemoteCharacters != null)
                {
                    foreach (var kv in clientRemoteCharacters.ToList())
                    {
                        if (kv.Value != null)
                        {
                            try { Object.Destroy(kv.Value); } catch { }
                        }
                    }
                    clientRemoteCharacters.Clear();
                }
            }

            
            Debug.Log("[MOD-Cleanup] 强制关闭同步UI...");
            if (WaitingSynchronizationUI.Instance != null)
            {
                WaitingSynchronizationUI.Instance.ForceCloseIfVisible("场景卸载");
            }

            
            if (scene.buildIndex > 0) 
            {
                Debug.Log("[MOD-Cleanup] 触发垃圾回收...");
                System.GC.Collect();
            }

            Debug.Log($"[MOD] ========== 场景卸载清理完成: {scene.name} ==========");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MOD] 场景卸载清理失败: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void LevelManager_OnAfterLevelInitialized()
    {
    }

    private void LevelManager_OnLevelInitialized()
    {
        WaitingSynchronizationUI syncUI = null;
        
        try
        {
            Debug.Log("[MOD] LevelManager_OnLevelInitialized START");
            
            Debug.Log("[MOD] LevelManager_OnLevelInitialized 开始，准备显示同步UI");
            syncUI = WaitingSynchronizationUI.Instance;
            if (syncUI != null)
            {
                Debug.Log("[MOD] 找到同步UI实例，开始显示");
                syncUI.Show();
                Debug.Log("[MOD] 同步UI Show完成，更新玩家列表");
                syncUI.UpdatePlayerList();
                Debug.Log("[MOD] 同步UI UpdatePlayerList完成");
            }
            else
            {
                Debug.LogWarning("[MOD] 同步UI实例为null！");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MOD] LevelManager_OnLevelInitialized ERROR (stage 1): {ex.Message}\n{ex.StackTrace}");
        }

        
        AITool.ResetAiSerials();

        
        if (GameObjectCacheManager.Instance != null)
        {
            GameObjectCacheManager.Instance.RefreshAllCaches();
        }

        
        if (LootManager.Instance != null)
        {
            LootManager.Instance.ClearCaches();
        }

        
        if (Utils.AsyncMessageQueue.Instance != null)
        {
            Utils.AsyncMessageQueue.Instance.EnableBulkMode();
        }

        SceneNet.Instance._cliSceneGateReleased = false;

        

        if (syncUI != null)
        {
            Debug.Log("[MOD] 注册同步任务");
            
            syncUI.RegisterTask("weather", "环境同步");
            syncUI.RegisterTask("player_health", "玩家状态同步");
            syncUI.RegisterTask("ai_loadouts", "AI装备接收"); 
            syncUI.RegisterTask("ai_names", "AI名称初始化");
        }

        HealthM.Instance.Client_ReportSelfHealth_IfReadyOnce();
        if (syncUI != null)
            syncUI.CompleteTask("player_health");

        SceneNet.Instance.TrySendSceneReadyOnce();


        
        var initManager = SceneInitManager.Instance;
        if (initManager != null)
        {
            
            initManager.EnqueueDelayedTask(
                () =>
                {
                    COOPManager.Weather.Client_RequestEnvSync();
                    var ui = WaitingSynchronizationUI.Instance;
                    if (ui != null)
                        ui.CompleteTask("weather", "完成");
                },
                1.0f,
                "Weather_EnvSync"
            );

            
            initManager.EnqueueDelayedTask(
                () =>
                {
                

                    var ui = WaitingSynchronizationUI.Instance;
                    if (ui != null)
                        ui.CompleteTask("ai_names", "完成");
                },
                1.0f,
                "AI_Names"
            );
        }

        COOPManager.Weather.Client_RequestEnvSync();
        AIName.Client_ResetNameIconSeal_OnLevelInit();
    }

    
    private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        try
        {
            Debug.Log($"[MOD] SceneManager_sceneLoaded: {arg0.name}, mode: {arg1}");
            SceneNet.Instance.TrySendSceneReadyOnce();
            COOPManager.Weather.Client_RequestEnvSync();
            Debug.Log($"[MOD] SceneManager_sceneLoaded completed for: {arg0.name}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MOD] SceneManager_sceneLoaded error for {arg0.name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public void OnNetworkReceive(
        NetPeer peer,
        NetPacketReader reader,
        byte channelNumber,
        DeliveryMethod deliveryMethod
    )
    {
        if (reader.AvailableBytes <= 0)
        {
            reader.Recycle();
            return;
        }

        
        Net.HybridNet.HybridNetCore.HandleIncoming(reader, peer);
    }

    private void OnSceneLoaded_IndexDestructibles(Scene s, LoadSceneMode m)
    {
        try
        {
            Debug.Log($"[MOD] OnSceneLoaded_IndexDestructibles: {s.name}");
            if (!networkStarted)
            {
                Debug.Log("[MOD] Network not started, skipping OnSceneLoaded_IndexDestructibles");
                return;
            }

            COOPManager.destructible.BuildDestructibleIndex();

            HealthTool._cliHookedSelf = false;

            HealthTool._cliInitHpReported = false;
            HealthM.Instance.Client_ReportSelfHealth_IfReadyOnce();

            if (!networkStarted || localPlayerStatus == null)
                return;

            var ok = LocalPlayerManager.Instance.ComputeIsInGame(out var sid);
            localPlayerStatus.SceneId = sid;
            localPlayerStatus.IsInGame = ok;

            Send_ClientStatus.Instance.SendClientStatusUpdate();
                
            Debug.Log($"[MOD] OnSceneLoaded_IndexDestructibles completed for: {s.name}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MOD] OnSceneLoaded_IndexDestructibles error for {s.name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    
    
    
    
    private void OnLevelInitialized_IndexDestructibles()
    {
        if (!networkStarted)
            return;

        
        
        

        Debug.Log("[MOD] OnLevelInitialized_IndexDestructibles: 跳过重复的 BuildDestructibleIndex 调用");
    }

    
    
    
    
    private System.Collections.IEnumerator SendLootFullSyncDelayed(NetPeer peer)
    {
        
        yield return null;

        
        
        Debug.Log($"[MOD-GATE] 战利品全量同步已禁用（避免大型地图网络IO阻塞） → {peer.EndPoint}");
        Debug.Log($"[MOD-GATE] 战利品将通过增量同步（玩家交互时）自动同步");

        yield break;

        
    }

    public struct Pending
    {
        public Inventory inv;
        public int srcPos;
        public int count;
    }
}
