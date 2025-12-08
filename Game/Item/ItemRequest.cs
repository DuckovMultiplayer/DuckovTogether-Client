















using ItemStatsSystem;

namespace EscapeFromDuckovCoopMod;

public class ItemRequest
{
    private ModBehaviourF Service => ModBehaviourF.Instance;


    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;

    public void SendItemDropRequest(uint token, Item item, Vector3 pos, bool createRb, Vector3 dir, float angle)
    {
        if (netManager == null || IsServer) return;
        var w = writer;
        if (w == null) return;
        w.Reset();
        w.Put(token);
        w.PutV3cm(pos);
        w.PutDir(dir);
        w.Put(angle);
        w.Put(createRb);
        ItemTool.WriteItemSnapshot(w, item);
        var msg = new Net.HybridNet.ItemDropRequestMessage { Token = token, ItemData = w.Data };
        Net.HybridNet.HybridNetCore.Send(msg);
    }

    public void SendItemPickupRequest(uint dropId)
    {
        if (IsServer || !networkStarted) return;
        var w = writer;
        if (w == null) return;
        var msg = new Net.HybridNet.ItemPickupRequestMessage { DropId = dropId };
        Net.HybridNet.HybridNetCore.Send(msg);
    }
}