















using LiteNetLib;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ItemStatsSystem;

namespace EscapeFromDuckovCoopMod;





public static class LootFullSyncMessage
{
    
    
    
    [System.Serializable]
    public class LootBoxData
    {
        public string type = "lootFullSync";
        public LootBoxInfo[] lootBoxes;
        public string timestamp;
    }

    
    
    
    [System.Serializable]
    public class LootBoxInfo
    {
        public int lootUid;              
        public int aiId;                 
        public Vector3Serializable position;        
        public Vector3Serializable rotation;        
        public int capacity;             
        public LootItemInfo[] items;     
    }

    
    
    
    [System.Serializable]
    public class LootItemInfo
    {
        public int position;             
        public int typeId;               
        public int stack;                
        public float durability;         
        public float durabilityLoss;     
        public bool inspected;           
        
    }

    
    
    
    [System.Serializable]
    public class Vector3Serializable
    {
        public float x, y, z;

        public Vector3Serializable(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    
    
    
    
    public static void Host_SendLootFullSync(NetPeer peer) { }

    
    
    
    private static System.Collections.IEnumerator SendLootBoxesInBatches(NetPeer peer, LootBoxInfo[] allLootBoxes) { yield break; }

    
    
    
    public static void Host_BroadcastLootFullSync() { }

    
    public static void Client_OnLootFullSync(string json)
{
    var service = ModBehaviourF.Instance;
    if (service == null)
    {
        Debug.LogWarning("[LootFullSync] ModBehaviourF未初始化");
        return;
    }

    if (service.IsServer)
    {
        Debug.LogWarning("[LootFullSync] 主机不应该接收此消息");
        return;
    }

    try
    {
        var data = JsonUtility.FromJson<LootBoxData>(json);
        if (data == null || data.lootBoxes == null)
        {
            Debug.LogError("[LootFullSync] 解析数据失败");
            return;
        }

        Debug.Log($"[LootFullSync] 收到战利品箱全量同步: {data.lootBoxes.Length} 个箱子, 时间={data.timestamp}");
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"[LootFullSync] 处理失败: {ex.Message}");
    }
}

    private static InteractableLootbox FindOrCreateLootBox(LootBoxInfo boxInfo)
    {
        var position = boxInfo.position.ToVector3();
        var rotation = Quaternion.Euler(boxInfo.rotation.ToVector3());

        
        var registry = Utils.LootContainerRegistry.Instance;
        InteractableLootbox lootBox = null;
        
        if (registry != null)
        {
            lootBox = registry.FindNearPosition(position, 0.5f) as InteractableLootbox;
            if (lootBox != null)
            {
                Debug.Log($"[LootFullSync] 找到现有战利品箱: lootUid={boxInfo.lootUid}, pos={position}");
                return lootBox;
            }
        }

        
        if (boxInfo.aiId > 0)
        {
            
            var deadLootBox = DeadLootBox.Instance;
            if (deadLootBox != null)
            {
                deadLootBox.SpawnDeadLootboxAt(boxInfo.aiId, boxInfo.lootUid, position, rotation);

                
                if (registry != null)
                {
                    lootBox = registry.FindNearPosition(position, 0.5f) as InteractableLootbox;
                    if (lootBox != null)
                    {
                        Debug.Log($"[LootFullSync] 创建AI掉落箱: lootUid={boxInfo.lootUid}, aiId={boxInfo.aiId}");
                        return lootBox;
                    }
                }
            }
        }

        Debug.LogWarning($"[LootFullSync] 无法创建战利品箱: lootUid={boxInfo.lootUid}");
        return null;
    }
}
