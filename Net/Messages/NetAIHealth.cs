using DuckovNet;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net;

public static class AIHealthMessage
{
    public class HealthSyncData
    {
        public string type = "ai_health_sync";
        public int aiId;
        public float maxHealth;
        public float currentHealth;
    }
    
    public class HealthReportData
    {
        public string type = "ai_health_report";
        public int aiId;
        public float maxHealth;
        public float currentHealth;
    }
    
    public static void Server_BroadcastHealth(int aiId, float maxHealth, float currentHealth)
    {
        if (!DedicatedServerMode.ShouldBroadcastState()) return;
        
        var data = new HealthSyncData
        {
            aiId = aiId,
            maxHealth = maxHealth,
            currentHealth = currentHealth
        };
        
        JsonMessage.BroadcastToAllClients(data, DeliveryMethod.ReliableOrdered);
    }
    
    public static void Client_ReportHealth(int aiId, float max, float cur)
    {
        var service = ModBehaviourF.Instance;
        if (service == null || !service.networkStarted || service.IsServer || service.connectedPeer == null || aiId == 0) 
            return;
        
        var aiHealth = COOPManager.AIHealth;
        if (aiHealth == null) return;
        
        var now = Time.time;
        if (aiHealth._cliNextReportAt.TryGetValue(aiId, out var next) && now < next)
        {
            if (aiHealth._cliLastReportedHp.TryGetValue(aiId, out var last) && Mathf.Abs(last - cur) < 0.01f)
                return;
        }
        
        var data = new HealthReportData
        {
            aiId = aiId,
            maxHealth = max,
            currentHealth = cur
        };
        
        JsonMessage.SendToHost(data, DeliveryMethod.ReliableOrdered);
        
        aiHealth._cliNextReportAt[aiId] = now + 0.05f;
        aiHealth._cliLastReportedHp[aiId] = cur;
        
        if (ModBehaviourF.LogAiHpDebug)
            Debug.Log($"[AI-HP][CLIENT] report aiId={aiId} max={max} cur={cur}");
    }
    
    public static void Client_HandleHealthSync(string json)
    {
        var data = JsonMessage.HandleReceivedJson<HealthSyncData>(json);
        if (data == null) return;
        
        var aiHealth = COOPManager.AIHealth;
        if (aiHealth == null) return;
        
        aiHealth.Client_ApplyAiHealth(data.aiId, data.maxHealth, data.currentHealth);
    }
    
    public static void Host_HandleHealthReport(NetPeer fromPeer, string json)
    {
        var service = ModBehaviourF.Instance;
        if (service == null || !service.networkStarted || !service.IsServer) return;
        
        var data = JsonMessage.HandleReceivedJson<HealthReportData>(json);
        if (data == null) return;
        
        var aiHealth = COOPManager.AIHealth;
        if (aiHealth == null) return;
        
        if (!AITool.aiById.TryGetValue(data.aiId, out var cmc) || !cmc)
        {
            if (ModBehaviourF.LogAiHpDebug)
                Debug.LogWarning($"[AI-HP][SERVER] report missing AI aiId={data.aiId} from={fromPeer?.EndPoint}");
            return;
        }
        
        var h = cmc.Health;
        if (!h)
        {
            if (ModBehaviourF.LogAiHpDebug)
                Debug.LogWarning($"[AI-HP][SERVER] report aiId={data.aiId} has no Health");
            return;
        }
        
        var applyMax = data.maxHealth > 0f ? data.maxHealth : h.MaxHealth;
        var maxForClamp = applyMax > 0f ? applyMax : h.MaxHealth;
        var clampedCur = maxForClamp > 0f ? Mathf.Clamp(data.currentHealth, 0f, maxForClamp) : Mathf.Max(0f, data.currentHealth);
        
        var wasDead = false;
        try
        {
            wasDead = h.IsDead;
        }
        catch { }
        
        HealthM.Instance.ForceSetHealth(h, applyMax, clampedCur, false);
        
        if (ModBehaviourF.LogAiHpDebug)
            Debug.Log($"[AI-HP][SERVER] apply report aiId={data.aiId} max={applyMax} cur={clampedCur} from={fromPeer?.EndPoint}");
        
        Server_BroadcastHealth(data.aiId, applyMax, clampedCur);
        
        DamageInfo deathInfo = new DamageInfo();
        if (clampedCur <= 0f && !wasDead)
        {
            if (ModBehaviourF.LogAiHpDebug)
                Debug.Log($"[AI-HP][SERVER] AI死亡触发 aiId={data.aiId}, 准备生成战利品盒子");
            
            deathInfo = new DamageInfo();
            
            try { deathInfo.damageValue = Mathf.Max(1f, applyMax > 0f ? applyMax : 1f); } catch { }
            try { deathInfo.finalDamage = deathInfo.damageValue; } catch { }
            try { deathInfo.damagePoint = cmc.transform.position; } catch { }
            try { deathInfo.damageNormal = Vector3.up; } catch { }
            try { deathInfo.toDamageReceiver = cmc.mainDamageReceiver; } catch { }
            
            try
            {
                if (service.playerStatuses != null && fromPeer != null && service.playerStatuses.TryGetValue(fromPeer, out var st) && st != null)
                    deathInfo.fromCharacter = CharacterMainControl.Main;
            }
            catch { }
        }
        
        if (clampedCur <= 0f)
        {
            aiHealth.Server_HandleAuthoritativeAiDeath(cmc, h, data.aiId, deathInfo, !wasDead);
        }
    }
}
