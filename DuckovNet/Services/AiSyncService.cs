using EscapeFromDuckovCoopMod.DuckovNet.Core;

namespace EscapeFromDuckovCoopMod.DuckovNet.Services;

public class AiSyncService : MonoBehaviour
{
    public static AiSyncService Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.Low, Reliable = false, Ordered = false)]
    public void SyncAiTransform(int aiId, Vector3 position, Vector3 forward)
    {
        Debug.Log($"[DuckovNet-AI] SyncAiTransform: AI#{aiId} at {position}");
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.Low, Reliable = false)]
    public void SyncAiAnimation(int aiId, float velocityX, float velocityY, bool isGrounded, int poseIndex)
    {
        Debug.Log($"[DuckovNet-AI] SyncAiAnimation: AI#{aiId} pose={poseIndex}");
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.Normal, Reliable = true)]
    public void SyncAiHealth(int aiId, float maxHealth, float currentHealth)
    {
        Debug.Log($"[DuckovNet-AI] SyncAiHealth: AI#{aiId} {currentHealth}/{maxHealth}");
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.High, Reliable = true)]
    public void SyncAiSpawn(int aiId, int seedId, Vector3 position, Quaternion rotation, AiLoadout loadout)
    {
        Debug.Log($"[DuckovNet-AI] SpawnAi: AI#{aiId} preset={loadout.presetName} at {position}");
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.High, Reliable = true)]
    public void SyncAiDeath(int aiId, Vector3 deathPosition)
    {
        Debug.Log($"[DuckovNet-AI] SyncAiDeath: AI#{aiId} at {deathPosition}");
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.Normal, Reliable = true)]
    public void SyncAiNameIcon(int aiId, int iconType, string aiName, bool showName)
    {
        Debug.Log($"[DuckovNet-AI] SyncAiNameIcon: AI#{aiId} name={aiName} icon={iconType}");
    }
}

public struct AiLoadout : IDuckovSerializable
{
    public string presetName;
    public int weaponId;
    public int helmetId;
    public int armorId;
    public int[] inventoryIds;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(presetName ?? string.Empty);
        writer.Put(weaponId);
        writer.Put(helmetId);
        writer.Put(armorId);
        writer.Put(inventoryIds?.Length ?? 0);
        if (inventoryIds != null)
        {
            foreach (var id in inventoryIds)
            {
                writer.Put(id);
            }
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        presetName = reader.GetString();
        weaponId = reader.GetInt();
        helmetId = reader.GetInt();
        armorId = reader.GetInt();
        var count = reader.GetInt();
        if (count > 0)
        {
            inventoryIds = new int[count];
            for (int i = 0; i < count; i++)
            {
                inventoryIds[i] = reader.GetInt();
            }
        }
    }
}
