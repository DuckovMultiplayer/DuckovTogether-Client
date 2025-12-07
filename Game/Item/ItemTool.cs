















using ItemStatsSystem;
using static EscapeFromDuckovCoopMod.LootNet;
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

public static class ItemTool
{
    public static uint nextDropId = 1;

    public static uint nextLocalDropToken = 1; 

    public static readonly Dictionary<uint, Item> serverDroppedItems = COOPManager.ItemHandle.serverDroppedItems; 
    public static readonly Dictionary<uint, Item> clientDroppedItems = COOPManager.ItemHandle.clientDroppedItems; 

    public static bool _serverApplyingLoot; 

    public static void AddNetDropTag(GameObject go, uint id)
    {
        if (!go) return;
        var tag = go.GetComponent<NetDropTag>() ?? go.AddComponent<NetDropTag>();
        tag.id = id;
    }

    public static void AddNetDropTag(Item item, uint id)
    {
        try
        {
            var ag = item?.ActiveAgent;
            if (ag && ag.gameObject) AddNetDropTag(ag.gameObject, id);
        }
        catch
        {
        }
    }

    
    public static List<Item> TryGetInventoryItems(Inventory inv)
    {
        if (inv == null) return null;

        var list = inv.Content;
        return list;
    }

    
    public static bool TryAddToInventory(Inventory inv, Item child)
    {
        if (inv == null || child == null) return false;
        try
        {
            
            return inv.AddAndMerge(child);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ITEM] Inventory.Add* 失败: {e.Message}");
            try
            {
                child.Detach();
                return inv.AddItem(child);
            }
            catch
            {
            }
        }

        return false;
    }

    public static uint AllocateDropId()
    {
        var id = nextDropId++;
        while (serverDroppedItems.ContainsKey(id))
            id = nextDropId++;
        return id;
    }

    public static async UniTaskVoid Server_DoSplitAsync(
        Inventory inv, int srcPos, int count, int prefer)
    {
        _serverApplyingLoot = true;
        try
        {
            var srcItem = inv.GetItemAt(srcPos);
            if (!srcItem) return;

            
            var newItem = await srcItem.Split(count);
            if (!newItem) return;

            
            var dst = prefer;
            if (dst < 0 || inv.GetItemAt(dst)) dst = inv.GetFirstEmptyPosition(srcPos + 1);
            if (dst < 0) dst = inv.GetFirstEmptyPosition();

            var ok = false;
            if (dst >= 0) ok = inv.AddAt(newItem, dst); 
            if (!ok) ok = inv.AddAndMerge(newItem, srcPos + 1); 

            if (!ok)
            {
                try
                {
                    Object.Destroy(newItem.gameObject);
                }
                catch
                {
                }

                if (srcItem) srcItem.StackCount = srcItem.StackCount + count;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LOOT][SPLIT] exception: {ex}");
        }
        finally
        {
            _serverApplyingLoot = false;
            COOPManager.LootNet.Server_SendLootboxState(null, inv);
        }
    }


    public static ItemSnapshot MakeSnapshot(Item item)
    {
        ItemSnapshot s;
        s.typeId = item.TypeID;
        s.stack = item.StackCount;
        s.durability = item.Durability;
        s.durabilityLoss = item.DurabilityLoss;
        s.inspected = item.Inspected;
        s.slots = new List<(string, ItemSnapshot)>();
        s.inventory = new List<ItemSnapshot>();

        var slots = item.Slots;
        if (slots != null && slots.list != null)
            foreach (var slot in slots.list)
                if (slot != null && slot.Content != null)
                    s.slots.Add((slot.Key ?? string.Empty, MakeSnapshot(slot.Content)));

        var invItems = TryGetInventoryItems(item.Inventory);
        if (invItems != null)
            foreach (var child in invItems)
                if (child != null)
                    s.inventory.Add(MakeSnapshot(child));

        return s;
    }

    
    public static void WriteItemSnapshot(NetDataWriter w, Item item)
    {
        w.Put(item.TypeID);
        w.Put(item.StackCount);
        w.Put(item.Durability);
        w.Put(item.DurabilityLoss);
        w.Put(item.Inspected);

        
        var slots = item.Slots;
        if (slots != null && slots.list != null)
        {
            var filled = 0;
            foreach (var s in slots.list)
                if (s != null && s.Content != null)
                    filled++;
            w.Put((ushort)filled);
            foreach (var s in slots.list)
            {
                if (s == null || s.Content == null) continue;
                w.Put(s.Key ?? string.Empty);
                WriteItemSnapshot(w, s.Content);
            }
        }
        else
        {
            w.Put((ushort)0);
        }

        
        var invItems = TryGetInventoryItems(item.Inventory);
        if (invItems != null)
        {
            var valid = new List<Item>(invItems.Count);
            foreach (var c in invItems)
                if (c != null)
                    valid.Add(c);

            w.Put((ushort)valid.Count);
            foreach (var child in valid)
                WriteItemSnapshot(w, child);
        }
        else
        {
            w.Put((ushort)0);
        }
    }

    
    
    
    
    public static ItemSnapshot ReadItemSnapshot(NetDataReader r)
    {
        ItemSnapshot s;
        s.typeId = r.GetInt();
        s.stack = r.GetInt();
        s.durability = r.GetFloat();
        s.durabilityLoss = r.GetFloat();
        s.inspected = r.GetBool();
        s.slots = new List<(string, ItemSnapshot)>();
        s.inventory = new List<ItemSnapshot>();

        int slotsCount = r.GetUShort();
        for (var i = 0; i < slotsCount; i++)
        {
            var key = r.GetString();
            var child = ReadItemSnapshot(r);
            s.slots.Add((key, child));
        }

        int invCount = r.GetUShort();
        for (var i = 0; i < invCount; i++)
        {
            var child = ReadItemSnapshot(r);
            s.inventory.Add(child);
        }

        return s;
    }

    public static ItemSnapshot ReadItemSnapshot(NetPacketReader r)
    {
        
        return ReadItemSnapshot((NetDataReader)r);
    }

    
    public static Item BuildItemFromSnapshot(ItemSnapshot s)
    {
        Item item = null;
        try
        {
            item = COOPManager.GetItemAsync(s.typeId).Result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ITEM] 实例化失败 typeId={s.typeId}, err={e}");
            return null;
        }

        if (item == null) return null;
        ApplySnapshotToItem(item, s);
        return item;
    }


    
    public static void ApplySnapshotToItem(Item item, ItemSnapshot s)
    {
        try
        {
            
            if (item.Stackable)
            {
                var target = s.stack;
                if (target < 1) target = 1;
                try
                {
                    target = Mathf.Clamp(target, 1, item.MaxStackCount);
                }
                catch
                {
                }

                item.StackCount = target;
            }

            item.Durability = s.durability;
            item.DurabilityLoss = s.durabilityLoss;
            item.Inspected = s.inspected;

            
            if (s.slots != null && s.slots.Count > 0 && item.Slots != null)
                foreach (var (key, childSnap) in s.slots)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    var slot = item.Slots.GetSlot(key);
                    if (slot == null)
                    {
                        Debug.LogWarning($"[ITEM] 找不到槽位 key={key} on {item.DisplayName}");
                        continue;
                    }

                    var child = BuildItemFromSnapshot(childSnap);
                    if (child == null) continue;
                    if (!slot.Plug(child, out _))
                        TryAddToInventory(item.Inventory, child);
                }

            
            if (s.inventory != null && s.inventory.Count > 0)
                foreach (var childSnap in s.inventory)
                {
                    var child = BuildItemFromSnapshot(childSnap);
                    if (child == null) continue;
                    TryAddToInventory(item.Inventory, child);
                }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ITEM] ApplySnapshot 出错: {e}");
        }
    }
}