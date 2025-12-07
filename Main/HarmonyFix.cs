















using System.Reflection;
using Duckov.Utilities;
using FX;

namespace EscapeFromDuckovCoopMod;

public sealed class LocalMeleeOncePerFrame : MonoBehaviour
{
    public int lastFrame;
}

public static class MeleeLocalGuard
{
    [ThreadStatic] public static bool LocalMeleeTryingToHurt;
}

[HarmonyPatch(typeof(DamageReceiver), "Hurt")]
internal static class Patch_ClientReportMeleeHit
{
    private static bool Prefix(DamageReceiver __instance, ref DamageInfo __0)
    {
        var mod = ModBehaviourF.Instance;

        
        if (mod == null || !mod.networkStarted || mod.IsServer || !MeleeLocalGuard.LocalMeleeTryingToHurt)
            return true;

        if (mod.connectedPeer == null)
        {
            Debug.LogWarning("[CLIENT] MELEE_HIT_REPORT aborted: connectedPeer==null, fallback to local Hurt");
            return true; 
        }

        try
        {
            var msg = new Net.HybridNet.MeleeHitReportMessage
            {
                PlayerId = mod.localPlayerStatus?.EndPoint ?? "",
                Damage = __0.damageValue,
                ArmorPiercing = __0.armorPiercing,
                CritDamageFactor = __0.critDamageFactor,
                CritRate = __0.critRate,
                IsCrit = __0.crit > 0,
                HitPoint = __0.damagePoint,
                HitNormal = __0.damageNormal,
                WeaponItemId = __0.fromWeaponItemID,
                BleedChance = __0.bleedChance,
                IsExplosion = __0.isExplosion
            };

            Net.HybridNet.HybridNetCore.Send(msg, mod.connectedPeer);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] Melee hit report failed: " + e);
            return true; 
        }

        try
        {
            if (PopText.instance)
            {
                
                var look = GameplayDataSettings.UIStyle
                    .GetElementDamagePopTextLook(ElementTypes.physics);

                
                var pos = (__0.damagePoint.sqrMagnitude > 1e-6f ? __0.damagePoint : __instance.transform.position)
                          + Vector3.up * 2f;

                
                var size = __0.crit > 0 ? look.critSize : look.normalSize;
                var sprite = __0.crit > 0 ? GameplayDataSettings.UIStyle.CritPopSprite : null;

                
                var text = __0.damageValue > 0f ? __0.damageValue.ToString("F1") : "HIT";

                PopText.Pop(text, pos, look.color, size, sprite);
            }
        }
        catch
        {
        }


        
        return false;
    }
}

public sealed class RemoteReplicaTag : MonoBehaviour
{
}

[HarmonyPatch(typeof(SetActiveByPlayerDistance), "FixedUpdate")]
internal static class Patch_SABPD_FixedUpdate_AllPlayersUnion
{
    private static NetService Service => NetService.Instance;
    private static Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;

    private static bool Prefix(SetActiveByPlayerDistance __instance)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return true; 

        var tr = Traverse.Create(__instance);

        
        var list = tr.Field<List<GameObject>>("cachedListRef").Value;
        if (list == null) return false;

        
        float dist;
        var prop = AccessTools.Property(__instance.GetType(), "Distance");
        if (prop != null) dist = (float)prop.GetValue(__instance, null);
        else dist = tr.Field<float>("distance").Value;
        var d2 = dist * dist;

        
        var sources = new List<Vector3>(8);
        var main = CharacterMainControl.Main;
        if (main) sources.Add(main.transform.position);

        foreach (var kv in playerStatuses)
        {
            var st = kv.Value;
            if (st != null && st.IsInGame) sources.Add(st.Position);
        }

        
        if (sources.Count == 0) return true;

        
        for (var i = 0; i < list.Count; i++)
        {
            var go = list[i];
            if (!go) continue;

            var within = false;
            var p = go.transform.position;
            for (var s = 0; s < sources.Count; s++)
                if ((p - sources[s]).sqrMagnitude <= d2)
                {
                    within = true;
                    break;
                }

            if (go.activeSelf != within) go.SetActive(within);
        }

        return false; 
    }
}

[HarmonyPatch(typeof(DamageReceiver), "Hurt")]
internal static class Patch_BlockClientAiVsAi_AtReceiver
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(DamageReceiver __instance, ref DamageInfo __0)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted || mod.IsServer) return true;

        var target = __instance ? __instance.GetComponentInParent<CharacterMainControl>() : null;
        var victimIsAI = target && (target.GetComponent<AICharacterController>() != null || target.GetComponent<NetAiTag>() != null);
        if (!victimIsAI) return true;

        var attacker = __0.fromCharacter;
        var attackerIsAI = attacker && (attacker.GetComponent<NetAiTag>() != null || attacker.GetComponent<NetAiTag>() != null);
        if (attackerIsAI) return false; 

        return true;
    }
}

[HarmonyPatch(typeof(SetActiveByPlayerDistance), "FixedUpdate")]
internal static class Patch_SABD_KeepRemoteAIActive_Client
{
    private static void Postfix(SetActiveByPlayerDistance __instance)
    {
        var m = ModBehaviourF.Instance;
        if (m == null || !m.networkStarted || m.IsServer) return;

        var forceAll = m.Client_ForceShowAllRemoteAI;
        if (forceAll) Traverse.Create(__instance).Field<float>("distance").Value = 9999f;
    }
}

[HarmonyPatch(typeof(DamageReceiver), "Hurt")]
internal static class Patch_ClientMelee_HurtRedirect_Destructible
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(DamageReceiver __instance, ref DamageInfo __0)
    {
        var m = ModBehaviourF.Instance;
        if (m == null || !m.networkStarted || m.IsServer) return true;

        
        if (!MeleeLocalGuard.LocalMeleeTryingToHurt) return true;

        
        var hs = __instance ? __instance.GetComponentInParent<HealthSimpleBase>() : null;
        if (!hs) return true;

        
        uint id = 0;
        var tag = hs.GetComponent<NetDestructibleTag>();
        if (tag) id = tag.id;
        if (id == 0)
            try
            {
                id = NetDestructibleTag.ComputeStableId(hs.gameObject);
            }
            catch
            {
            }

        if (id == 0) return true; 

        
        COOPManager.HurtM.Client_RequestDestructibleHurt(id, __0);
        return false; 
    }
}


[HarmonyPatch]
internal static class Patch_ClosureView_ShowAndReturnTask_SpectatorGate
{
    private static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("Duckov.UI.ClosureView");
        if (t == null) return null;
        return AccessTools.Method(t, "ShowAndReturnTask", new[] { typeof(DamageInfo), typeof(float) });
    }

    private static bool Prefix(ref UniTask __result, DamageInfo dmgInfo, float duration)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return true;

        if (Spectator.Instance._skipSpectatorForNextClosure)
        {
            Spectator.Instance._skipSpectatorForNextClosure = false;
            __result = UniTask.CompletedTask;
            return true;
        }

        
        if (Spectator.Instance.TryEnterSpectatorOnDeath(dmgInfo))
            
            
            return true; 

        return true;
    }
}

[HarmonyPatch(typeof(GameManager), "get_Paused")]
internal static class Patch_Paused_AlwaysFalse
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ref bool __result)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return true;

        __result = mod.Pausebool;

        return false;
    }
}

[HarmonyPatch(typeof(PauseMenu), "Show")]
internal static class Patch_PauseMenuShow_AlwaysFalse
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPostfix]
    private static void Postfix()
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return;

        mod.Pausebool = true;
    }
}

[HarmonyPatch(typeof(PauseMenu), "Hide")]
internal static class Patch_PauseMenuHide_AlwaysFalse
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPostfix]
    private static void Postfix()
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return;

        mod.Pausebool = false;
    }
}

internal static class NcMainRedirector
{
    [field: ThreadStatic] public static CharacterMainControl Current { get; private set; }

    public static void Set(CharacterMainControl cmc)
    {
        Current = cmc;
    }

    public static void Clear()
    {
        Current = null;
    }
}













































