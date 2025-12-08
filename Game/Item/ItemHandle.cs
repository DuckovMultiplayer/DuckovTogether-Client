















using ItemStatsSystem;
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

public class ItemHandle
{
    public readonly HashSet<Item> _clientSpawnByServerItems = new(); 
    public readonly HashSet<Item> _serverSpawnedFromClientItems = new(); 
    public readonly Dictionary<uint, Item> clientDroppedItems = new(); 
    public readonly HashSet<uint> pendingLocalDropTokens = new();
    public readonly Dictionary<uint, Item> pendingTokenItems = new(); 
    public readonly Dictionary<uint, Item> serverDroppedItems = new(); 
    private ModBehaviourF Service => ModBehaviourF.Instance;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;

    public void HandleItemDropRequestFromMessage(NetPeer peer, Net.HybridNet.ItemDropRequestMessage msg)
    {
        if (!DedicatedServerMode.ShouldRunHostLogic()) return;
        
    }

    public void HandleItemPickupRequestFromMessage(NetPeer peer, Net.HybridNet.ItemPickupRequestMessage msg)
    {
        if (!DedicatedServerMode.ShouldRunHostLogic()) return;
        var id = msg.DropId;
        if (!serverDroppedItems.TryGetValue(id, out var item) || item == null)
            return;

        serverDroppedItems.Remove(id);
        try
        {
            var agent = item.ActiveAgent;
            if (agent != null && agent.gameObject != null)
                Object.Destroy(agent.gameObject);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ITEM] 服务器销毁 agent 异常: {e.Message}");
        }

        var despawnMsg = new Net.HybridNet.ItemDespawnMessage { DropId = id };
        Net.HybridNet.HybridNetCore.Send(despawnMsg);
    }

    public void HandleItemDropRequest(NetPeer peer, NetDataReader r)
    {
        if (!DedicatedServerMode.ShouldRunHostLogic()) return;
        var token = r.GetUInt();
        var pos = r.GetV3cm();
        var dir = r.GetDir();
        var angle = r.GetFloat();
        var create = r.GetBool();
        var snap = ItemTool.ReadItemSnapshot(r);

        
        var item = ItemTool.BuildItemFromSnapshot(snap);
        if (item == null) return;
        _serverSpawnedFromClientItems.Add(item);
        var agent = item.Drop(pos, create, dir, angle);

        
        var id = ItemTool.AllocateDropId();
        ItemTool.serverDroppedItems[id] = item;

        if (agent && agent.gameObject) ItemTool.AddNetDropTag(agent.gameObject, id);

        
        var w = writer;
        if (w == null) return;
        w.Reset();
        w.Put(token);
        w.Put(id);
        w.PutV3cm(pos);
        w.PutDir(dir);
        w.Put(angle);
        w.Put(create);
        ItemTool.WriteItemSnapshot(w, item); 
        var msg = new Net.HybridNet.ItemSpawnMessage { Token = token, DropId = id, ItemData = w.Data };
        Net.HybridNet.HybridNetCore.Send(msg);
    }

    public void HandleItemDropRequestInternal(NetPeer peer, uint token, Vector3 position)
    {
        if (!DedicatedServerMode.ShouldRunHostLogic()) return;
        var pos = position;
        var dir = Vector3.zero;
        var angle = 0f;
        var create = false;
        var snap = ItemTool.ReadItemSnapshot(null);

        
        var item = ItemTool.BuildItemFromSnapshot(snap);
        if (item == null) return;
        _serverSpawnedFromClientItems.Add(item);
        var agent = item.Drop(pos, create, dir, angle);

        
        var id = ItemTool.AllocateDropId();
        ItemTool.serverDroppedItems[id] = item;

        if (agent && agent.gameObject) ItemTool.AddNetDropTag(agent.gameObject, id);

        
        var w = writer;
        if (w == null) return;
        w.Reset();
        w.Put(token);
        w.Put(id);
        w.PutV3cm(pos);
        w.PutDir(dir);
        w.Put(angle);
        w.Put(create);
        ItemTool.WriteItemSnapshot(w, item); 
        var msg = new Net.HybridNet.ItemSpawnMessage { Token = token, DropId = id, ItemData = w.Data };
        Net.HybridNet.HybridNetCore.Send(msg);
    }

    public void HandleItemSpawn(NetDataReader r)
    {
        if (IsServer) return;
        var token = r.GetUInt();
        var id = r.GetUInt();
        var pos = r.GetV3cm();
        var dir = r.GetDir();
        var angle = r.GetFloat();
        var create = r.GetBool();
        var snap = ItemTool.ReadItemSnapshot(r);

        if (pendingLocalDropTokens.Remove(token))
        {
            if (pendingTokenItems.TryGetValue(token, out var localItem) && localItem != null)
            {
                clientDroppedItems[id] = localItem; 
                pendingTokenItems.Remove(token);

                ItemTool.AddNetDropTag(localItem, id);
            }
            else
            {
                
                var item2 = ItemTool.BuildItemFromSnapshot(snap);
                if (item2 != null)
                {
                    _clientSpawnByServerItems.Add(item2);
                    var agent2 = item2.Drop(pos, create, dir, angle);
                    clientDroppedItems[id] = item2;

                    if (agent2 && agent2.gameObject) ItemTool.AddNetDropTag(agent2.gameObject, id);
                }
            }

            return;
        }

        
        var item = ItemTool.BuildItemFromSnapshot(snap);
        if (item == null) return;

        _clientSpawnByServerItems.Add(item);
        var agent = item.Drop(pos, create, dir, angle);
        clientDroppedItems[id] = item;

        if (agent && agent.gameObject) ItemTool.AddNetDropTag(agent.gameObject, id);
    }

    public void HandleItemPickupRequest(NetPeer peer, NetDataReader r)
    {
        if (!DedicatedServerMode.ShouldRunHostLogic()) return;
        var id = r.GetUInt();
        if (!serverDroppedItems.TryGetValue(id, out var item) || item == null)
            return; 

        
        serverDroppedItems.Remove(id);
        try
        {
            var agent = item.ActiveAgent;
            if (agent != null && agent.gameObject != null)
                Object.Destroy(agent.gameObject);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ITEM] 服务器销毁 agent 异常: {e.Message}");
        }

        
        var msg = new Net.HybridNet.ItemDespawnMessage { DropId = id };
        Net.HybridNet.HybridNetCore.Send(msg);
    }

    public void HandleItemDespawn(NetDataReader r)
    {
        if (IsServer) return;
        var id = r.GetUInt();
        if (ItemTool.clientDroppedItems.TryGetValue(id, out var item))
        {
            ItemTool.clientDroppedItems.Remove(id);
            try
            {
                var agent = item?.ActiveAgent;
                if (agent != null && agent.gameObject != null)
                    Object.Destroy(agent.gameObject);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ITEM] 客户端销毁 agent 异常: {e.Message}");
            }
        }
    }
}