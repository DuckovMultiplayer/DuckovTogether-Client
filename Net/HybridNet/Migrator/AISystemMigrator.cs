using UnityEngine;
using System.Collections.Generic;
using DuckovNet;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public static class AISystemMigrator
{
    private static float _lastTransformBroadcast = 0f;
    private static float _lastAnimBroadcast = 0f;
    private static float _lastHealthCheck = 0f;
    
    private const float TRANSFORM_INTERVAL = 0.05f;
    private const float ANIM_INTERVAL = 0.1f;
    private const float HEALTH_CHECK_INTERVAL = 1.0f;
    
    public static void Initialize()
    {
        Debug.Log("[HybridNet-AI] AI系统迁移器初始化");
    }
    
    public static void Update()
    {
        var service = ModBehaviourF.Instance;
        if (service == null || !service.networkStarted) return;
        
        if (service.IsServer)
        {
            UpdateServerAI();
        }
    }
    
    private static void UpdateServerAI()
    {
        var time = Time.time;
        
        if (time - _lastTransformBroadcast >= TRANSFORM_INTERVAL)
        {
            BroadcastAITransforms();
            _lastTransformBroadcast = time;
        }
        
        if (time - _lastAnimBroadcast >= ANIM_INTERVAL)
        {
            BroadcastAIAnimations();
            _lastAnimBroadcast = time;
        }
        
        if (time - _lastHealthCheck >= HEALTH_CHECK_INTERVAL)
        {
            CheckAIHealth();
            _lastHealthCheck = time;
        }
    }
    
    private static void BroadcastAITransforms()
    {
        var batchSize = 50;
        var currentBatch = 0;
        
        foreach (var kv in AITool.aiById)
        {
            var aiId = kv.Key;
            var cmc = kv.Value;
            
            if (cmc == null || !cmc.gameObject.activeInHierarchy) continue;
            
            var model = cmc.characterModel;
            if (model == null) continue;
            
            var msg = AITransformMessage.FromTransform(aiId, cmc.transform, model.transform);
            HybridNetCore.Interest.UpdateEntityPosition(aiId, msg.Position);
            HybridNetCore.Send(msg);
            
            currentBatch++;
            if (currentBatch >= batchSize) break;
        }
    }
    
    private static void BroadcastAIAnimations()
    {
        var batchSize = 100;
        var currentBatch = 0;
        
        foreach (var kv in AITool.aiById)
        {
            var aiId = kv.Key;
            var cmc = kv.Value;
            
            if (cmc == null || !cmc.gameObject.activeInHierarchy) continue;
            
            var anim = cmc.GetComponent<Animator>();
            if (anim == null) continue;
            
            var msg = AIAnimationMessage.FromAnimator(aiId, anim);
            msg.EntityId = aiId;
            HybridNetCore.Send(msg);
            
            currentBatch++;
            if (currentBatch >= batchSize) break;
        }
    }
    
    private static void CheckAIHealth()
    {
        foreach (var kv in AITool.aiById)
        {
            var aiId = kv.Key;
            var cmc = kv.Value;
            
            if (cmc == null) continue;
            
            var health = cmc.Health;
            if (health == null) continue;
            
            if (health.IsDead)
            {
                var msg = AIHealthMessage.FromHealth(aiId, health);
                HybridNetCore.Send(msg);
            }
        }
    }
    
    public static void Server_BroadcastAIHealth(int aiId, Health health)
    {
        if (health == null) return;
        
        var msg = AIHealthMessage.FromHealth(aiId, health);
        HybridNetCore.Send(msg);
    }
    
    public static void Server_MarkBossAI(int aiId, bool isBoss = true)
    {
        HybridNetCore.Interest.MarkGlobalEntity(aiId, isBoss);
        Debug.Log($"[HybridNet-AI] 标记Boss AI: {aiId}");
    }
}
