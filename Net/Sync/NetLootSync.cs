















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
        if (!DedicatedServerMode.ShouldBroadcastState())
        {
            Debug.LogWarning("[LootFullSync] 只能在主机端调用");
            return;
        }

        try
        {
            
            var lootBoxes = CollectAllLootBoxes();

            if (lootBoxes.Length == 0)
            {
                Debug.Log($"[LootFullSync] 没有战利品箱需要同步 → {peer.EndPoint}");
                return;
            }

            
            if (lootBoxes.Length < 50)
            {
                var data = new LootBoxData
                {
                    lootBoxes = lootBoxes,
                    timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };

                JsonMessage.SendToPeer(peer, data, DeliveryMethod.ReliableOrdered);
                Debug.Log($"[LootFullSync] 发送战利品箱全量同步: {lootBoxes.Length} 个箱子 → {peer.EndPoint}");
            }
            else
            {
                
                Debug.Log($"[LootFullSync] 启动分批发送: {lootBoxes.Length} 个箱子 → {peer.EndPoint}");
                ModBehaviourF.Instance.StartCoroutine(SendLootBoxesInBatches(peer, lootBoxes));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LootFullSync] 发送失败: {ex.Message}\n{ex.StackTrace}");
        }
    }

    
    
    
    private static System.Collections.IEnumerator SendLootBoxesInBatches(NetPeer peer, LootBoxInfo[] allLootBoxes)
    {
        const int BATCH_SIZE = 50; 
        int totalBatches = (allLootBoxes.Length + BATCH_SIZE - 1) / BATCH_SIZE;

        Debug.Log($"[LootFullSync] 开始分批发送: 总计 {allLootBoxes.Length} 个箱子，分 {totalBatches} 批");

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            int startIndex = batchIndex * BATCH_SIZE;
            int count = System.Math.Min(BATCH_SIZE, allLootBoxes.Length - startIndex);

            
            var batch = new LootBoxInfo[count];
            System.Array.Copy(allLootBoxes, startIndex, batch, 0, count);

            try
            {
                var data = new LootBoxData
                {
                    lootBoxes = batch,
                    timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };

                
                JsonMessage.SendToPeer(peer, data, DeliveryMethod.ReliableOrdered);

                Debug.Log($"[LootFullSync] 发送批次 {batchIndex + 1}/{totalBatches}: {count} 个箱子 → {peer.EndPoint}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LootFullSync] 发送批次 {batchIndex + 1} 失败: {ex.Message}");
            }

            
            yield return null;
        }

        Debug.Log($"[LootFullSync] 分批发送完成: 总计 {allLootBoxes.Length} 个箱子 → {peer.EndPoint}");
    }

    
    
    
    public static void Host_BroadcastLootFullSync()
    {
        if (!DedicatedServerMode.ShouldBroadcastState())
        {
            Debug.LogWarning("[LootFullSync] 只能在主机端调用");
            return;
        }

        var service = NetService.Instance;
        var netManager = service?.netManager;
        if (netManager == null || netManager.ConnectedPeerList.Count == 0)
        {
            Debug.Log("[LootFullSync] 没有连接的客户端，跳过广播");
            return;
        }

        try
        {
            
            var lootBoxes = CollectAllLootBoxes();

            var data = new LootBoxData
            {
                lootBoxes = lootBoxes,
                timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };

            
            JsonMessage.BroadcastToAllClients(data, DeliveryMethod.ReliableOrdered);

            Debug.Log($"[LootFullSync] 广播战利品箱全量同步: {lootBoxes.Length} 个箱子 → {netManager.ConnectedPeerList.Count} 个客户端");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LootFullSync] 广播失败: {ex.Message}\n{ex.StackTrace}");
        }
    }

    
    
    
    private static LootBoxInfo[] CollectAllLootBoxes()
    {
        var lootBoxList = new List<LootBoxInfo>();
        var lootManager = LootManager.Instance;

        if (lootManager == null || lootManager._srvLootByUid == null)
        {
            Debug.LogWarning("[LootFullSync] LootManager 或 _srvLootByUid 为空");
            return lootBoxList.ToArray();
        }

        Debug.Log($"[LootFullSync] 开始收集战利品箱，共 {lootManager._srvLootByUid.Count} 个已注册的箱子");

        
        
        int successCount = 0;
        int failCount = 0;
        int skippedUninitialized = 0;

        foreach (var kv in lootManager._srvLootByUid)
        {
            int lootUid = kv.Key;
            var inventory = kv.Value;

            if (inventory == null)
            {
                failCount++;
                continue;
            }

            try
            {
                
                var lootBox = lootManager.FindLootboxByInventory(inventory);

                if (lootBox == null)
                {
                    
                    skippedUninitialized++;
                    continue;
                }

                
                
                if (inventory.Loading)
                {
                    skippedUninitialized++;
                    continue;
                }

                
                int aiId = 0;

                
                var items = CollectLootBoxItems(inventory);

                var boxInfo = new LootBoxInfo
                {
                    lootUid = lootUid,
                    aiId = aiId,
                    position = new Vector3Serializable(lootBox.transform.position),
                    rotation = new Vector3Serializable(lootBox.transform.rotation.eulerAngles),
                    capacity = inventory.Capacity,
                    items = items
                };

                lootBoxList.Add(boxInfo);
                successCount++;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LootFullSync] 收集战利品箱失败 (lootUid={lootUid}): {ex.Message}");
                failCount++;
            }
        }

        Debug.Log($"[LootFullSync] 收集完成: 成功={successCount}, 跳过未初始化={skippedUninitialized}, 失败={failCount}");

        return lootBoxList.ToArray();
    }

    
    
    
    private static LootItemInfo[] CollectLootBoxItems(Inventory inventory)
    {
        var itemList = new List<LootItemInfo>();

        if (inventory == null)
            return itemList.ToArray();

        
        var items = ItemTool.TryGetInventoryItems(inventory);
        if (items == null || items.Count == 0)
            return itemList.ToArray();

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null)
                continue;

            try
            {
                var itemInfo = new LootItemInfo
                {
                    position = i,
                    typeId = item.TypeID,
                    stack = item.StackCount,
                    durability = item.Durability,
                    durabilityLoss = item.DurabilityLoss,
                    inspected = item.Inspected
                };

                itemList.Add(itemInfo);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LootFullSync] 收集物品失败 (位置{i}): {ex.Message}");
            }
        }

        return itemList.ToArray();
    }

    
    
    
    public static void Client_OnLootFullSync(string json)
    {
        var service = NetService.Instance;
        if (service == null)
        {
            Debug.LogWarning("[LootFullSync] NetService未初始化");
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

            
            ApplyLootBoxes(data.lootBoxes);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LootFullSync] 处理失败: {ex.Message}\n{ex.StackTrace}");
        }
    }

    
    
    
    private static void ApplyLootBoxes(LootBoxInfo[] lootBoxes)
    {
        int successCount = 0;
        int failCount = 0;

        foreach (var boxInfo in lootBoxes)
        {
            try
            {
                ApplySingleLootBox(boxInfo);
                successCount++;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LootFullSync] 应用战利品箱失败 (lootUid={boxInfo.lootUid}): {ex.Message}");
                failCount++;
            }
        }

        Debug.Log($"[LootFullSync] 应用完成: 成功={successCount}, 失败={failCount}");
    }

    
    
    
    private static void ApplySingleLootBox(LootBoxInfo boxInfo)
    {
        
        var lootBox = FindOrCreateLootBox(boxInfo);
        if (lootBox == null)
        {
            Debug.LogWarning($"[LootFullSync] 无法创建战利品箱: lootUid={boxInfo.lootUid}");
            return;
        }

        var inventory = lootBox.Inventory;
        if (inventory == null)
        {
            Debug.LogWarning($"[LootFullSync] 战利品箱没有Inventory: lootUid={boxInfo.lootUid}");
            return;
        }

        
        try
        {
            inventory.SetCapacity(boxInfo.capacity);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LootFullSync] 设置容量失败: {ex.Message}");
        }

        
        var existingItems = ItemTool.TryGetInventoryItems(inventory);
        if (existingItems != null)
        {
            foreach (var item in existingItems.ToList())
            {
                if (item != null)
                {
                    try
                    {
                        UnityEngine.Object.Destroy(item.gameObject);
                    }
                    catch { }
                }
            }
            existingItems.Clear();
        }

        
        foreach (var itemInfo in boxInfo.items)
        {
            try
            {
                
                var snapshot = new LootNet.ItemSnapshot
                {
                    typeId = itemInfo.typeId,
                    stack = itemInfo.stack,
                    durability = itemInfo.durability,
                    durabilityLoss = itemInfo.durabilityLoss,
                    inspected = itemInfo.inspected,
                    slots = null,
                    inventory = null
                };

                
                var item = ItemTool.BuildItemFromSnapshot(snapshot);
                if (item == null)
                {
                    Debug.LogWarning($"[LootFullSync] 无法创建物品: typeId={itemInfo.typeId}");
                    continue;
                }

                try
                {
                    item.Inspected = true;
                }
                catch
                {
                }

                
                var added = false;
                var preferPos = itemInfo.position;

                if (preferPos >= 0)
                {
                    try
                    {
                        if (preferPos >= inventory.Capacity)
                            preferPos = inventory.Capacity - 1;
                    }
                    catch
                    {
                    }

                    if (preferPos >= 0)
                    {
                        try
                        {
                            if (inventory.GetItemAt(preferPos) == null)
                                added = InventoryPlacementUtil.TryPlaceItemExact(inventory, item, preferPos);
                        }
                        catch
                        {
                        }
                    }
                }

                if (!added)
                {
                    try
                    {
                        var empty = inventory.GetFirstEmptyPosition();
                        if (empty >= 0 && empty < inventory.Capacity)
                            added = InventoryPlacementUtil.TryPlaceItemExact(inventory, item, empty);
                    }
                    catch
                    {
                    }
                }

                if (!added)
                    added = ItemTool.TryAddToInventory(inventory, item);

                if (!added)
                {
                    Debug.LogWarning($"[LootFullSync] 无法添加物品到容器: typeId={itemInfo.typeId}, pos={itemInfo.position}");
                    UnityEngine.Object.Destroy(item.gameObject);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LootFullSync] 添加物品失败 (位置{itemInfo.position}): {ex.Message}");
            }
        }

        
        var lootManager = LootManager.Instance;
        if (lootManager != null && boxInfo.lootUid >= 0)
        {
            lootManager._cliLootByUid[boxInfo.lootUid] = inventory;
        }

        Debug.Log($"[LootFullSync] 应用战利品箱: lootUid={boxInfo.lootUid}, items={boxInfo.items.Length}");
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
