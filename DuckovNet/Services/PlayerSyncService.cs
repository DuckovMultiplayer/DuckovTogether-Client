using EscapeFromDuckovCoopMod.DuckovNet.Core;

namespace EscapeFromDuckovCoopMod.DuckovNet.Services;

public class PlayerSyncService : MonoBehaviour
{
    public static PlayerSyncService Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.High, Reliable = false, Ordered = false)]
    public void SyncPlayerPosition(string playerId, Vector3 position, Quaternion rotation)
    {
        Debug.Log($"[DuckovNet-PlayerSync] SyncPlayerPosition: {playerId} at {position}");
        if (ModBehaviourF.Instance == null) return;

        var remoteChar = GetRemoteCharacter(playerId);
        if (remoteChar != null)
        {
            var interp = remoteChar.GetComponent<NetInterpolator>();
            if (interp != null)
            {
                interp.Push(position, rotation);
            }
        }
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.Normal)]
    public void SyncPlayerAnimation(string playerId, float velocityX, float velocityY, bool isGrounded, int poseIndex)
    {
        Debug.Log($"[DuckovNet-PlayerSync] SyncPlayerAnimation: {playerId}");
        var remoteChar = GetRemoteCharacter(playerId);
        if (remoteChar != null)
        {
            
        }
    }

    [DuckovRpc(RpcTarget.Server, Priority = RpcPriority.High, Reliable = true)]
    public void RequestPlayerHealth(string playerId, float currentHealth, float maxHealth)
    {
        Debug.Log($"[DuckovNet-PlayerSync] RequestPlayerHealth: {playerId} {currentHealth}/{maxHealth}");
        if (!ModBehaviourF.Instance.IsServer) return;

        
    }

    [DuckovRpc(RpcTarget.Client, Priority = RpcPriority.Critical, Reliable = true)]
    public void AuthorizePlayerHealth(string playerId, float health)
    {
        Debug.Log($"[DuckovNet-PlayerSync] AuthorizePlayerHealth: {playerId} {health}");
        if (ModBehaviourF.Instance.IsServer) return;

        var remoteChar = GetRemoteCharacter(playerId);
        if (remoteChar != null)
        {
            var healthComp = remoteChar.GetComponentInChildren<Health>();
            if (healthComp != null)
            {
                healthComp.SetHealth(health);
            }
        }
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.Normal)]
    public void SyncPlayerEquipment(string playerId, int weaponId, int helmetId, int armorId)
    {
        Debug.Log($"[DuckovNet-PlayerSync] SyncPlayerEquipment: {playerId} weapon={weaponId}");
        var remoteChar = GetRemoteCharacter(playerId);
        if (remoteChar == null) return;

        var cmc = remoteChar.GetComponent<CharacterMainControl>();
        if (cmc == null) return;

        StartCoroutine(ApplyEquipmentAsync(cmc, weaponId, helmetId, armorId));
    }

    private System.Collections.IEnumerator ApplyEquipmentAsync(CharacterMainControl cmc, int weaponId, int helmetId, int armorId)
    {
        if (weaponId > 0)
        {
            var weaponTask = COOPManager.GetItemAsync(weaponId);
            yield return new WaitUntil(() => weaponTask.IsCompleted);
            if (weaponTask.Result != null)
            {
                
            }
        }

        if (helmetId > 0)
        {
            var helmetTask = COOPManager.GetItemAsync(helmetId);
            yield return new WaitUntil(() => helmetTask.IsCompleted);
            if (helmetTask.Result != null)
            {
                COOPManager.ChangeHelmatModel(cmc.characterModel, helmetTask.Result);
            }
        }

        if (armorId > 0)
        {
            var armorTask = COOPManager.GetItemAsync(armorId);
            yield return new WaitUntil(() => armorTask.IsCompleted);
            if (armorTask.Result != null)
            {
                COOPManager.ChangeArmorModel(cmc.characterModel, armorTask.Result);
            }
        }
    }

    private GameObject GetRemoteCharacter(string playerId)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null) return null;

        if (!mod.IsServer)
        {
            mod.clientRemoteCharacters.TryGetValue(playerId, out var remoteChar);
            return remoteChar;
        }
        else
        {
            foreach (var kvp in mod.remoteCharacters)
            {
                if (kvp.Key.EndPoint.ToString() == playerId)
                {
                    return kvp.Value;
                }
            }
        }

        return null;
    }
}
