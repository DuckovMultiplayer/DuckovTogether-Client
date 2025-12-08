namespace EscapeFromDuckovCoopMod;

/// <summary>
/// 阻止客户端玩家死亡时掉落所有物品
/// 参考NoDeathDrops模组的实现方式
/// </summary>
[HarmonyPatch(typeof(CharacterMainControl), "DropAllItems")]
internal static class PreventClientPlayerDropAllItemsPatch
{
    [HarmonyPrefix]
    private static bool PreventPlayerDropAllItems(CharacterMainControl __instance)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted)
        {
            Debug.Log("[COOP] DropAllItems - 非联机模式，允许正常执行");
            return true;
        }

        if (__instance == CharacterMainControl.Main)
        {
            Debug.Log("[COOP] 阻止客户端玩家死亡时掉落所有物品");
            return false;
        }

        return true;
    }
}

/// <summary>
/// 阻止客户端玩家死亡时销毁所有物品
/// </summary>
[HarmonyPatch(typeof(CharacterMainControl), "DestroyAllItem")]
internal static class PreventClientPlayerDestroyAllItemsPatch
{
    [HarmonyPrefix]
    private static bool PreventPlayerDestroyAllItems(CharacterMainControl __instance)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted)
        {
            Debug.Log("[COOP] DestroyAllItem - 非联机模式，允许正常执行");
            return true;
        }

        if (__instance == CharacterMainControl.Main)
        {
            Debug.Log("[COOP] 阻止客户端玩家死亡时销毁所有物品");
            return false;
        }

        return true;
    }
}

/// <summary>
/// 阻止客户端玩家的OnDead方法执行，防止物品被清空
/// 这是最关键的补丁，因为OnDead方法中包含了物品清空逻辑
/// </summary>
[HarmonyPatch(typeof(CharacterMainControl), "OnDead")]
internal static class PreventClientPlayerOnDeadPatch
{
    [HarmonyPrefix]
    private static bool PreventPlayerOnDead(CharacterMainControl __instance, DamageInfo dmgInfo)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted)
        {
            Debug.Log("[COOP] OnDead - 非联机模式，允许正常执行");
            return true;
        }

        if (__instance == CharacterMainControl.Main)
        {
            Debug.Log("[COOP] 阻止客户端玩家OnDead执行，保留物品");

            try
            {
                if (Spectator.Instance != null)
                {
                    Spectator.Instance.TryEnterSpectatorOnDeath(dmgInfo);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[COOP] 触发观战模式失败: {e}");
            }

            return false;
        }

        return true;
    }
}

/// <summary>
/// ✅ 已移除：允许正常生成墓碑
/// 玩家死亡时必须创建墓碑，物品正常放进墓碑里
/// </summary>
// [HarmonyPatch(typeof(LevelConfig), "get_SpawnTomb")]
// internal static class PreventTombSpawnPatch
// {
//     [HarmonyPrefix]
//     private static bool PreventTombSpawn(ref bool __result)
//     {
//         // 已禁用：允许游戏正常生成墓碑
//         return true;
//     }
// }

/// <summary>
/// 阻止客户端的死亡事件补发，这是联机模组触发死亡的另一个路径
/// </summary>
[HarmonyPatch(typeof(LocalPlayerManager), "Client_EnsureSelfDeathEvent")]
internal static class PreventClientEnsureSelfDeathEventPatch
{
    [HarmonyPrefix]
    private static bool PreventEnsureSelfDeathEvent(
        LocalPlayerManager __instance,
        Health h,
        CharacterMainControl cmc
    )
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted)
        {
            Debug.Log("[COOP] Client_EnsureSelfDeathEvent - 非联机模式，允许正常执行");
            return true;
        }

        if (cmc == CharacterMainControl.Main)
        {
            Debug.Log("[COOP] 阻止客户端死亡事件补发，保留物品");
            return false;
        }

        return true;
    }
}

