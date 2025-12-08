















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

    
    
    
    
    public static void Host_SendLootFullSync(NetPeer peer)
    {
        var service = ModBehaviourF.Instance;
        if (service == null || !service.IsServer) return;
        
        var registry = Utils.LootContainerRegistry.Instance;
        if (registry == null)
        {
            Debug.LogWarning("[LootFullSync] LootContainerRegistry not available");
            return;
        }
        
        var containers = registry.GetAllContainers();
        var lootBoxes = new List<LootBoxInfo>();
        
        foreach (var container in containers)
        {
            if (container == null) continue;
            
            var lootBox = container as InteractableLootbox;
            if (lootBox == null) continue;
            
            var boxInfo = new LootBoxInfo
            {
                lootUid = lootBox.GetInstanceID(),
                aiId = 0,
                position = new Vector3Serializable(lootBox.transform.position),
                rotation = new Vector3Serializable(lootBox.transform.eulerAngles),
                capacity = 10,
                items = ExtractItemsFromLootbox(lootBox)
            };
            
            lootBoxes.Add(boxInfo);
        }
        
        var data = new LootBoxData
        {
            type = "lootFullSync",
            lootBoxes = lootBoxes.ToArray(),
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        };
        
        var json = JsonUtility.ToJson(data);
        var writer = new LiteNetLib.Utils.NetDataWriter();
        writer.Put((byte)9);
        writer.Put(json);
        peer.Send(writer, LiteNetLib.DeliveryMethod.ReliableOrdered);
        
        Debug.Log($"[LootFullSync] Sent {lootBoxes.Count} loot boxes to peer {peer.EndPoint}");
    }
    
    private static LootItemInfo[] ExtractItemsFromLootbox(InteractableLootbox lootBox)
    {
        var items = new List<LootItemInfo>();
        
        try
        {
            var inventoryField = lootBox.GetType().GetField("inventory", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (inventoryField == null) return items.ToArray();
            
            var inventory = inventoryField.GetValue(lootBox);
            if (inventory == null) return items.ToArray();
            
            var itemsProperty = inventory.GetType().GetProperty("Items");
            if (itemsProperty == null) return items.ToArray();
            
            var itemList = itemsProperty.GetValue(inventory) as System.Collections.IList;
            if (itemList == null) return items.ToArray();
            
            for (int i = 0; i < itemList.Count; i++)
            {
                var item = itemList[i];
                if (item == null) continue;
                
                var typeIdProp = item.GetType().GetProperty("TypeId");
                var stackProp = item.GetType().GetProperty("Stack");
                
                items.Add(new LootItemInfo
                {
                    position = i,
                    typeId = typeIdProp != null ? (int)typeIdProp.GetValue(item) : 0,
                    stack = stackProp != null ? (int)stackProp.GetValue(item) : 1,
                    durability = 1f,
                    durabilityLoss = 0f,
                    inspected = false
                });
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LootFullSync] Failed to extract items: {ex.Message}");
        }
        
        return items.ToArray();
    }
    
    private static System.Collections.IEnumerator SendLootBoxesInBatches(NetPeer peer, LootBoxInfo[] allLootBoxes)
    {
        const int batchSize = 10;
        
        for (int i = 0; i < allLootBoxes.Length; i += batchSize)
        {
            var batch = allLootBoxes.Skip(i).Take(batchSize).ToArray();
            
            var data = new LootBoxData
            {
                type = "lootFullSync",
                lootBoxes = batch,
                timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
            
            var json = JsonUtility.ToJson(data);
            var writer = new LiteNetLib.Utils.NetDataWriter();
            writer.Put((byte)9);
            writer.Put(json);
            peer.Send(writer, LiteNetLib.DeliveryMethod.ReliableOrdered);
            
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    public static void Host_BroadcastLootFullSync()
    {
        var service = ModBehaviourF.Instance;
        if (service == null || !service.IsServer) return;
        
        var netClient = Net.CoopNetClient.Instance;
        if (netClient == null) return;
        
        Debug.Log("[LootFullSync] Broadcasting loot sync to all clients");
    }

    
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
