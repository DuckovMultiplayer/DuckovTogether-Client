















using static EscapeFromDuckovCoopMod.LootNet;
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

public class HostHandle
{
    private NetService Service => NetService.Instance;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;
    private Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    private Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
    private Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;

    
    public void HandlePlayerJoinFromMessage(NetPeer peer, Net.HybridNet.PlayerJoinMessage msg)
    {
        
    }
    
    public void HandlePlayerLeaveFromMessage(NetPeer peer, Net.HybridNet.PlayerLeaveMessage msg)
    {
        
    }

    public void Server_HandlePlayerDeadTree(Vector3 pos, Quaternion rot, ItemSnapshot snap)
    {
        if (!DedicatedServerMode.ShouldRunHostLogic()) return;

        var tmpRoot = ItemTool.BuildItemFromSnapshot(snap);
        if (!tmpRoot)
        {
            Debug.LogWarning("[LOOT] HostDeath BuildItemFromSnapshot failed.");
            return;
        }

        var deadPfb = LootManager.Instance.ResolveDeadLootPrefabOnServer(); 
        var box = InteractableLootbox.CreateFromItem(tmpRoot, pos + Vector3.up * 0.10f, rot, true, deadPfb);
        if (box) DeadLootBox.Instance.Server_OnDeadLootboxSpawned(box, null); 

        if (tmpRoot && tmpRoot.gameObject) Object.Destroy(tmpRoot.gameObject);
    }

    
    public void Server_HandleHostDeathViaTree(CharacterMainControl who)
    {
        if (!networkStarted || !IsServer || !who) return;
        var item = who.CharacterItem;
        if (!item) return;

        var pos = who.transform.position;
        var rot = who.characterModel ? who.characterModel.transform.rotation : who.transform.rotation;

        var snap = ItemTool.MakeSnapshot(item); 
        Server_HandlePlayerDeadTree(pos, rot, snap);
    }
}