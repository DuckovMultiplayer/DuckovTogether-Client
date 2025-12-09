using System.Collections.Generic;
using DuckovNet;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net;

public static class AITransformMessage
{
    public class TransformSnapshotData
    {
        public string type = "ai_transform_snapshot";
        public List<TransformEntry> transforms;
    }
    
    public class TransformEntry
    {
        public int aiId;
        public Vector3 position;
        public Vector3 forward;
    }
    
    public static void Server_BroadcastTransforms()
    {
        if (!DedicatedServerMode.ShouldBroadcastState() || AITool.aiById.Count == 0) return;
        
        var data = new TransformSnapshotData
        {
            transforms = new List<TransformEntry>()
        };
        
        foreach (var kv in AITool.aiById)
        {
            var cmc = kv.Value;
            if (!cmc) continue;
            
            var t = cmc.transform;
            var fwd = cmc.characterModel.transform.rotation * Vector3.forward;
            
            data.transforms.Add(new TransformEntry
            {
                aiId = kv.Key,
                position = t.position,
                forward = fwd
            });
        }
        
        if (data.transforms.Count > 0)
            JsonMessage.BroadcastToAllClients(data, DeliveryMethod.ReliableOrdered);
    }
    
    public static void Client_HandleSnapshot(string json)
    {
        var data = JsonMessage.HandleReceivedJson<TransformSnapshotData>(json);
        if (data == null) return;
        
        foreach (var entry in data.transforms)
        {
            AITool.ApplyAiTransform(entry.aiId, entry.position, entry.forward);
        }
    }
}
