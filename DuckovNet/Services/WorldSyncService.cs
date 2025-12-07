using EscapeFromDuckovCoopMod.DuckovNet.Core;

namespace EscapeFromDuckovCoopMod.DuckovNet.Services;

public class WorldSyncService : MonoBehaviour
{
    public static WorldSyncService Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    [DuckovRpc(RpcTarget.Server, Priority = RpcPriority.Normal, Reliable = true)]
    public void RequestOpenLootBox(string playerId, uint lootBoxId)
    {
        Debug.Log($"[DuckovNet-World] RequestOpenLootBox: {lootBoxId}");
        if (!ModBehaviourF.Instance.IsServer) return;

        
    }

    [DuckovRpc(RpcTarget.Client, Priority = RpcPriority.High, Reliable = true)]
    public void SyncLootBoxState(uint lootBoxId, LootBoxState state)
    {
        Debug.Log($"[DuckovNet-World] SyncLootBoxState: {lootBoxId}");
        if (ModBehaviourF.Instance.IsServer) return;

        
    }

    [DuckovRpc(RpcTarget.Server, Priority = RpcPriority.Normal, Reliable = true)]
    public void RequestDoorInteraction(uint doorId, bool isOpen)
    {
        Debug.Log($"[DuckovNet-World] RequestDoorInteraction: {doorId} open={isOpen}");
        if (!ModBehaviourF.Instance.IsServer) return;

        
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.Normal, Reliable = true)]
    public void SyncDoorState(uint doorId, bool isOpen, float progress)
    {
        Debug.Log($"[DuckovNet-World] SyncDoorState: {doorId} open={isOpen} progress={progress}");
        
    }

    [DuckovRpc(RpcTarget.Server, Priority = RpcPriority.Normal, Reliable = true)]
    public void RequestDestructibleDamage(uint destructibleId, float damage, Vector3 hitPoint)
    {
        Debug.Log($"[DuckovNet-World] RequestDestructibleDamage: {destructibleId} damage={damage}");
        if (!ModBehaviourF.Instance.IsServer) return;

        
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.Normal, Reliable = true)]
    public void SyncDestructibleHealth(uint destructibleId, float currentHealth)
    {
        Debug.Log($"[DuckovNet-World] SyncDestructibleHealth: {destructibleId} health={currentHealth}");
        
    }
}

public struct LootBoxState : IDuckovSerializable
{
    public int capacity;
    public LootItem[] items;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(capacity);
        writer.Put(items.Length);
        foreach (var item in items)
        {
            item.Serialize(writer);
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        capacity = reader.GetInt();
        var count = reader.GetInt();
        items = new LootItem[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = new LootItem();
            items[i].Deserialize(reader);
        }
    }
}

public struct LootItem : IDuckovSerializable
{
    public int itemId;
    public int slotIndex;
    public int stackCount;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(itemId);
        writer.Put(slotIndex);
        writer.Put(stackCount);
    }

    public void Deserialize(NetDataReader reader)
    {
        itemId = reader.GetInt();
        slotIndex = reader.GetInt();
        stackCount = reader.GetInt();
    }
}
