using System.Collections.Generic;
using DuckovNet;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net;

public static class AISeedMessage
{
    public class SeedSnapshotData
    {
        public string type = "ai_seed_snapshot";
        public int sceneSeed;
        public List<SeedPair> seeds;
    }
    
    public class SeedPair
    {
        public int rootId;
        public int seed;
    }
    
    public class SeedPatchData
    {
        public string type = "ai_seed_patch";
        public List<SeedPair> seeds;
    }
    
    public static void Server_SendSeedSnapshot(List<(int id, int seed)> pairs, int sceneSeed, NetPeer target = null)
    {
        var data = new SeedSnapshotData
        {
            sceneSeed = sceneSeed,
            seeds = new List<SeedPair>(pairs.Count)
        };
        
        foreach (var (id, seed) in pairs)
        {
            data.seeds.Add(new SeedPair { rootId = id, seed = seed });
        }
        
        if (target == null)
            JsonMessage.BroadcastToAllClients(data, DeliveryMethod.ReliableOrdered);
        else
            JsonMessage.SendToPeer(target, data, DeliveryMethod.ReliableOrdered);
            
        Debug.Log($"[AI-SEED] 已发送 {pairs.Count} 条 Root 映射 目标={(target == null ? "ALL" : target.EndPoint.ToString())}");
    }
    
    public static void Server_SendSeedPatch(List<(int id, int seed)> pairs, NetPeer target = null)
    {
        var data = new SeedPatchData
        {
            seeds = new List<SeedPair>(pairs.Count)
        };
        
        foreach (var (id, seed) in pairs)
        {
            data.seeds.Add(new SeedPair { rootId = id, seed = seed });
        }
        
        if (target == null)
            JsonMessage.BroadcastToAllClients(data, DeliveryMethod.ReliableOrdered);
        else
            JsonMessage.SendToPeer(target, data, DeliveryMethod.ReliableOrdered);
    }
    
    public static void Client_HandleSeedSnapshot(string json)
    {
        var data = JsonMessage.HandleReceivedJson<SeedSnapshotData>(json);
        if (data == null) return;
        
        var aiHandle = COOPManager.AIHandle;
        if (aiHandle == null) return;
        
        aiHandle.sceneSeed = data.sceneSeed;
        aiHandle.aiRootSeeds.Clear();
        
        foreach (var pair in data.seeds)
        {
            aiHandle.aiRootSeeds[pair.rootId] = pair.seed;
        }
        
        Debug.Log($"[AI-SEED] 收到 {data.seeds.Count} 个 Root 的种子");
    }
    
    public static void Client_HandleSeedPatch(string json)
    {
        var data = JsonMessage.HandleReceivedJson<SeedPatchData>(json);
        if (data == null) return;
        
        var aiHandle = COOPManager.AIHandle;
        if (aiHandle == null) return;
        
        foreach (var pair in data.seeds)
        {
            aiHandle.aiRootSeeds[pair.rootId] = pair.seed;
        }
        
        Debug.Log($"[AI-SEED] 应用增量 Root 种子数: {data.seeds.Count}");
    }
}
