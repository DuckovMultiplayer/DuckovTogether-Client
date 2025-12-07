















using EscapeFromDuckovCoopMod.Utils;
using EscapeFromDuckovCoopMod.Utils.NetHelper;
using EscapeFromDuckovCoopMod.Utils.Logger.Tools;

namespace EscapeFromDuckovCoopMod;

public class ModBehaviour : Duckov.Modding.ModBehaviour
{
    static ModBehaviour()
    {
        Debug.Log("##########################################################");
        Debug.Log("#### [LOADER_DLL_LOADED] Loader类已加载 - 2025-11-17 ####");
        Debug.Log("##########################################################");
    }

    public Harmony Harmony;

    public void OnEnable()
    {
        Debug.Log("[ModBehaviour] OnEnable() START");
        Harmony = new Harmony("DETF_COOP");
        Debug.Log("[ModBehaviour] Harmony instance created");
        Harmony.PatchAll();
        Debug.Log("[ModBehaviour] Harmony.PatchAll() completed");

        var go = new GameObject("COOP_MOD_1");
        DontDestroyOnLoad(go);
        Debug.Log("[ModBehaviour] COOP_MOD_1 GameObject created");

        go.AddComponent<NetService>();
        Debug.Log("[ModBehaviour] NetService added");
        COOPManager.InitManager();
        Debug.Log("[ModBehaviour] COOPManager.InitManager() completed");
        go.AddComponent<ModBehaviourF>();
        Debug.Log("[ModBehaviour] ModBehaviourF added");
        Debug.Log("[ModBehaviour] About to call Loader()");
        Loader();
        Debug.Log("[ModBehaviour] OnEnable() COMPLETE");
    }

    public void Loader()
    {
        Debug.Log("[Loader] Loader() START");
        CoopLocalization.Initialize();
        Debug.Log("[Loader] CoopLocalization initialized");

        var go = new GameObject("COOP_MOD_");
        DontDestroyOnLoad(go);
        Debug.Log("[Loader] COOP_MOD_ GameObject created");

        go.AddComponent<SteamP2PLoader>();
        Debug.Log("[Loader] SteamP2PLoader added");
        go.AddComponent<AIRequest>();
        Debug.Log("[Loader] AIRequest added");
        go.AddComponent<Send_ClientStatus>();
        Debug.Log("[Loader] Send_ClientStatus added");
        go.AddComponent<HealthM>();
        Debug.Log("[Loader] HealthM added");
        go.AddComponent<LocalPlayerManager>();
        Debug.Log("[Loader] LocalPlayerManager added");
        go.AddComponent<SendLocalPlayerStatus>();
        Debug.Log("[Loader] SendLocalPlayerStatus added");
        go.AddComponent<Spectator>();
        Debug.Log("[Loader] Spectator added");
        go.AddComponent<DeadLootBox>();
        Debug.Log("[Loader] DeadLootBox added");
        go.AddComponent<LootManager>();
        Debug.Log("[Loader] LootManager added");
        go.AddComponent<SceneNet>();
        Debug.Log("[Loader] SceneNet added");
        go.AddComponent<MModUI>();
        Debug.Log("[Loader] MModUI added");
        Debug.Log("===== Loader: 消息消费者初始化完成 =====");
        
        Net.HybridNet.HybridNetIntegration.Initialize();
        Debug.Log("===== Loader: HybridNet混合框架初始化完成 =====");
        
        
        go.AddComponent<JsonMessageRouter>();
        Debug.Log("[Loader] JsonMessageRouter added");
        go.AddComponent<EscapeFromDuckovCoopMod.Utils.LootContainerRegistry>();
        Debug.Log("[Loader] LootContainerRegistry added");
        go.AddComponent<SceneInitManager>(); 
        Debug.Log("[Loader] SceneInitManager added");
        go.AddComponent<EscapeFromDuckovCoopMod.Utils.BackgroundTaskManager>(); 
        Debug.Log("[Loader] BackgroundTaskManager added");
        go.AddComponent<EscapeFromDuckovCoopMod.Jobs.JobSystemManager>(); 
        Debug.Log("[Loader] JobSystemManager added");
        go.AddComponent<WaitingSynchronizationUI>(); 
        Debug.Log("[Loader] WaitingSynchronizationUI added");
        go.AddComponent<GameObjectCacheManager>(); 
        Debug.Log("[Loader] GameObjectCacheManager added");
        go.AddComponent<Main.Teleport.TeleportManager>(); 
        Debug.Log("[Loader] TeleportManager added");
        go.AddComponent<Net.Relay.RelayServerManager>(); 
        Debug.Log("[Loader] RelayServerManager added");
        go.AddComponent<OnlineLobbyUI>(); 
        Debug.Log("[Loader] OnlineLobbyUI added");
        go.AddComponent<EscapeFromDuckovCoopMod.Utils.AsyncMessageQueue>(); 
        Debug.Log("[Loader] AsyncMessageQueue added");
        CoopTool.Init();
        Debug.Log("[Loader] CoopTool.Init() completed");

        Debug.Log("[Loader] About to call DeferredInit()");
        LoggerHelper.Log("[Loader] ========== About to call DeferredInit() ==========");
        DeferredInit();
        Debug.Log("[Loader] Loader() COMPLETE");
        LoggerHelper.Log("[Loader] ========== Loader() COMPLETE ==========");
    }

    private void DeferredInit()
    {
        Debug.Log("[DeferredInit] START");
        SafeInit<SteamP2PLoader>(s => s.Init());
        Debug.Log("[DeferredInit] SteamP2PLoader.Init() called");
        SafeInit<SceneNet>(sn => sn.Init());
        Debug.Log("[DeferredInit] SceneNet.Init() called");
        SafeInit<LootManager>(lm => lm.Init());
        Debug.Log("[DeferredInit] LootManager.Init() called");
        SafeInit<LocalPlayerManager>(lpm => lpm.Init());
        Debug.Log("[DeferredInit] LocalPlayerManager.Init() called");
        SafeInit<HealthM>(hm => hm.Init());
        Debug.Log("[DeferredInit] HealthM.Init() called");
        SafeInit<SendLocalPlayerStatus>(s => s.Init());
        Debug.Log("[DeferredInit] SendLocalPlayerStatus.Init() called");
        SafeInit<Spectator>(s => s.Init());
        Debug.Log("[DeferredInit] Spectator.Init() called");
        SafeInit<MModUI>(ui => ui.Init());
        Debug.Log("[DeferredInit] MModUI.Init() called");
        SafeInit<AIRequest>(a => a.Init());
        Debug.Log("[DeferredInit] AIRequest.Init() called");
        SafeInit<Send_ClientStatus>(s => s.Init());
        Debug.Log("[DeferredInit] Send_ClientStatus.Init() called");
        SafeInit<DeadLootBox>(s => s.Init());
        Debug.Log("[DeferredInit] DeadLootBox.Init() called");
        Debug.Log("[DeferredInit] COMPLETE");
    }

    private void SafeInit<T>(Action<T> init) where T : Component
    {
        var typeName = typeof(T).Name;
        Debug.Log($"[SafeInit] Looking for {typeName}");
        var c = FindObjectOfType<T>();
        if (c == null)
        {
            Debug.LogWarning($"[SafeInit] {typeName} not found!");
            return;
        }
        Debug.Log($"[SafeInit] {typeName} found, calling init");
        try
        {
            init(c);
            Debug.Log($"[SafeInit] {typeName} init completed");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SafeInit] {typeName} init FAILED: {ex.Message}\n{ex.StackTrace}");
        }
    }

}