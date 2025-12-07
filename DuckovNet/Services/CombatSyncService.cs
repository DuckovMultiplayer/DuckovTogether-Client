using EscapeFromDuckovCoopMod.DuckovNet.Core;

namespace EscapeFromDuckovCoopMod.DuckovNet.Services;

public class CombatSyncService : MonoBehaviour
{
    public static CombatSyncService Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    [DuckovRpc(RpcTarget.Server, Priority = RpcPriority.High, Reliable = true)]
    public void RequestFireWeapon(string playerId, Vector3 muzzlePos, Vector3 direction, int weaponId, float spread)
    {
        Debug.Log($"[DuckovNet-Combat] RequestFireWeapon: {playerId} {muzzlePos} -> {direction} (weaponId={weaponId}, spread={spread})");
        if (!ModBehaviourF.Instance.IsServer) return;

        
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.High, Reliable = false, Ordered = false)]
    public void BroadcastFireEvent(string playerId, Vector3 muzzlePos, Vector3 direction, int weaponId)
    {
        Debug.Log($"[DuckovNet-Combat] BroadcastFireEvent: {playerId} {muzzlePos} -> {direction} (weaponId={weaponId})");
        if (playerId == GetLocalPlayerId()) return;

        
    }

    [DuckovRpc(RpcTarget.Server, Priority = RpcPriority.Critical, Reliable = true)]
    public void RequestThrowGrenade(string playerId, int grenadeId, Vector3 position, Vector3 velocity, Quaternion rotation)
    {
        Debug.Log($"[DuckovNet-Combat] RequestThrowGrenade: {playerId} {grenadeId} at {position}");
        if (!ModBehaviourF.Instance.IsServer) return;

        
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.High, Reliable = true)]
    public void SpawnGrenade(int instanceId, int grenadeId, Vector3 position, Vector3 velocity, Quaternion rotation)
    {
        Debug.Log($"[DuckovNet-Combat] SpawnGrenade: instance={instanceId} type={grenadeId} at {position}");
        
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.High, Reliable = true)]
    public void ExplodeGrenade(int instanceId, Vector3 position)
    {
        Debug.Log($"[DuckovNet-Combat] ExplodeGrenade: instance={instanceId} at {position}");
        
    }

    [DuckovRpc(RpcTarget.Server, Priority = RpcPriority.High, Reliable = true)]
    public void RequestMeleeAttack(string playerId, Vector3 attackPoint, Vector3 direction, float damage, float range)
    {
        Debug.Log($"[DuckovNet-Combat] RequestMeleeAttack: {playerId} {attackPoint} -> {direction} (damage={damage}, range={range})");
        if (!ModBehaviourF.Instance.IsServer) return;

    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.Normal, Reliable = false)]
    public void BroadcastMeleeSwing(string playerId, int animationIndex)
    {
        if (playerId == GetLocalPlayerId()) return;

    }

    [DuckovRpc(RpcTarget.Client, Priority = RpcPriority.High, Reliable = true)]
    public void ReportDamage(string targetId, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (ModBehaviourF.Instance.IsServer) return;

    }

    private string GetLocalPlayerId()
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null) return string.Empty;

        if (!mod.IsServer)
        {
            return mod.connectedPeer?.EndPoint.ToString() ?? string.Empty;
        }
        return "local";
    }
}
