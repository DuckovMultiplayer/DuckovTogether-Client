using EscapeFromDuckovCoopMod.DuckovNet.Core;
using EscapeFromDuckovCoopMod.DuckovNet.Services;
using EscapeFromDuckovCoopMod.Utils.Logger.Tools;

namespace EscapeFromDuckovCoopMod.DuckovNet;

public static class DuckovNetBootstrap
{
    private static bool _isInitialized;

    public static void Initialize(GameObject hostObject, NetService netService)
    {
        if (_isInitialized)
        {
            LoggerHelper.LogWarning("[DuckovNet] Already initialized");
            return;
        }

        LoggerHelper.LogInfo("[DuckovNet] Framework initialization started");

        var runtime = hostObject.AddComponent<DuckovNetRuntime>();
        runtime.Initialize(netService);

        hostObject.AddComponent<PlayerSyncService>();
        hostObject.AddComponent<CombatSyncService>();
        hostObject.AddComponent<WorldSyncService>();
        hostObject.AddComponent<AiSyncService>();
        hostObject.AddComponent<SceneSyncService>();

        _isInitialized = true;

        LoggerHelper.LogInfo("[DuckovNet] Framework initialization completed");
    }

    public static void Shutdown()
    {
        _isInitialized = false;
        LoggerHelper.LogInfo("[DuckovNet] Framework shutdown");
    }

    public static bool IsInitialized => _isInitialized;
}
