using System;
using System.Collections.Generic;
using LiteNetLib;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net;

public static class AIAnimationMessage
{
    public class AnimSnapshotData
    {
        public string type = "ai_anim_snapshot";
        public List<AnimEntry> animations;
    }
    
    public class AnimEntry
    {
        public int aiId;
        public float speed;
        public float dirX;
        public float dirY;
        public int hand;
        public bool gunReady;
        public bool dashing;
    }
    
    public static void Server_BroadcastAnimations()
    {
        if (!DedicatedServerMode.ShouldBroadcastState() || AITool.aiById == null || AITool.aiById.Count == 0) 
            return;
        
        var list = new List<AnimEntry>();
        
        foreach (var kv in AITool.aiById)
        {
            var id = kv.Key;
            var cmc = kv.Value;
            if (!cmc) continue;
            
            if (!AITool.IsRealAI(cmc)) continue;
            if (!cmc.gameObject.activeInHierarchy || !cmc.enabled) continue;
            
            var magic = cmc.GetComponentInChildren<CharacterAnimationControl_MagicBlend>(true);
            var anim = magic ? magic.animator : cmc.GetComponentInChildren<Animator>(true);
            if (!anim || !anim.isActiveAndEnabled || !anim.gameObject.activeInHierarchy) continue;
            
            list.Add(new AnimEntry
            {
                aiId = id,
                speed = anim.GetFloat(Animator.StringToHash("MoveSpeed")),
                dirX = anim.GetFloat(Animator.StringToHash("MoveDirX")),
                dirY = anim.GetFloat(Animator.StringToHash("MoveDirY")),
                hand = anim.GetInteger(Animator.StringToHash("HandState")),
                gunReady = anim.GetBool(Animator.StringToHash("GunReady")),
                dashing = anim.GetBool(Animator.StringToHash("Dashing"))
            });
        }
        
        if (list.Count == 0) return;
        
        const int MAX_PER_PACKET = 50;
        for (var i = 0; i < list.Count; i += MAX_PER_PACKET)
        {
            var count = Math.Min(MAX_PER_PACKET, list.Count - i);
            var data = new AnimSnapshotData
            {
                animations = list.GetRange(i, count)
            };
            
            JsonMessage.BroadcastToAllClients(data, DeliveryMethod.Unreliable);
        }
    }
    
    public static void Client_HandleSnapshot(string json)
    {
        var data = JsonMessage.HandleReceivedJson<AnimSnapshotData>(json);
        if (data == null) return;
        
        var aiHandle = COOPManager.AIHandle;
        if (aiHandle == null) return;
        
        foreach (var entry in data.animations)
        {
            var st = new AiAnimState
            {
                speed = entry.speed,
                dirX = entry.dirX,
                dirY = entry.dirY,
                hand = entry.hand,
                gunReady = entry.gunReady,
                dashing = entry.dashing
            };
            
            if (!AITool.Client_ApplyAiAnim(entry.aiId, st))
            {
                aiHandle._pendingAiAnims[entry.aiId] = st;
            }
        }
    }
}
