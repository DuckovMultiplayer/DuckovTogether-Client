using HarmonyLib;
using ItemStatsSystem;
using System.Reflection;

namespace EscapeFromDuckovCoopMod;

[HarmonyPatch]
internal static class Patch_LootContainer_AutoRegister
{
    private static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("InteractableLootbox");
        if (type == null) return null;
        return AccessTools.Method(type, "Awake");
    }
    
    private static bool Prepare()
    {
        var type = AccessTools.TypeByName("InteractableLootbox");
        return type != null && AccessTools.Method(type, "Awake") != null;
    }
    
    private static void Postfix(object __instance)
    {
        try
        {
            var registry = Utils.LootContainerRegistry.Instance;
            if (registry != null && __instance != null)
            {
                registry.RegisterContainer(__instance);
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[LootPatch] Awake Postfix error: {ex.Message}\n{ex.StackTrace}");
        }
    }
}

[HarmonyPatch]
internal static class Patch_LootContainer_AutoUnregister
{
    private static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("InteractableLootbox");
        if (type == null) return null;
        return AccessTools.Method(type, "OnDestroy");
    }
    
    private static bool Prepare()
    {
        var type = AccessTools.TypeByName("InteractableLootbox");
        return type != null && AccessTools.Method(type, "OnDestroy") != null;
    }
    
    private static void Prefix(object __instance)
    {
        var registry = Utils.LootContainerRegistry.Instance;
        if (registry != null && __instance != null)
        {
            registry.UnregisterContainer(__instance);
        }
    }
}

// ❌ 禁用：在Inventory属性getter上打补丁会导致每次访问属性时都触发，造成性能崩溃
// 原因：游戏在一帧内可能访问Inventory属性数百次，导致日志爆炸和卡死
// 解决方案：在Awake时已经注册容器，Inventory映射会在那时建立
//[HarmonyPatch]
//internal static class Patch_LootContainer_UpdateInventoryMapping
//{
//    private static MethodBase TargetMethod()
//    {
//        var type = AccessTools.TypeByName("InteractableLootbox");
//        if (type == null) return null;
//        return AccessTools.PropertyGetter(type, "Inventory");
//    }
//    
//    private static bool Prepare()
//    {
//        var type = AccessTools.TypeByName("InteractableLootbox");
//        return type != null && AccessTools.PropertyGetter(type, "Inventory") != null;
//    }
//    
//    private static void Postfix(object __instance, Inventory __result)
//    {
//        // 已禁用
//    }
//}
