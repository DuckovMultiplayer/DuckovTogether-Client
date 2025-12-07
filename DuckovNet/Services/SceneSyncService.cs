using EscapeFromDuckovCoopMod.DuckovNet.Core;

namespace EscapeFromDuckovCoopMod.DuckovNet.Services;

public class SceneSyncService : MonoBehaviour
{
    public static SceneSyncService Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    [DuckovRpc(RpcTarget.Server, Priority = RpcPriority.Normal, Reliable = true)]
    public void RequestSceneVote(string playerId, string sceneId)
    {
        Debug.Log($"[DuckovNet-Scene] RequestSceneVote: playerId={playerId} sceneId={sceneId}");
        if (!ModBehaviourF.Instance.IsServer) return;

        
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.High, Reliable = true)]
    public void BroadcastVoteState(SceneVoteState voteState)
    {
        Debug.Log($"[DuckovNet-Scene] BroadcastVoteState: active={voteState.isActive} scene={voteState.sceneId}");
        
    }

    [DuckovRpc(RpcTarget.Server, Priority = RpcPriority.Normal, Reliable = true)]
    public void SetPlayerReady(string playerId, bool isReady)
    {
        Debug.Log($"[DuckovNet-Scene] SetPlayerReady: playerId={playerId} isReady={isReady}");
        if (!ModBehaviourF.Instance.IsServer) return;

        
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.Critical, Reliable = true)]
    public void ForceSceneLoad(string sceneId)
    {
        Debug.Log($"[DuckovNet-Scene] ForceSceneLoad: sceneId={sceneId}");
        
    }

    [DuckovRpc(RpcTarget.Server, Priority = RpcPriority.High, Reliable = true)]
    public void NotifySceneReady(string playerId)
    {
        Debug.Log($"[DuckovNet-Scene] NotifySceneReady: playerId={playerId}");
        if (!ModBehaviourF.Instance.IsServer) return;

        
    }

    [DuckovRpc(RpcTarget.All, Priority = RpcPriority.High, Reliable = true)]
    public void ReleaseSceneGate()
    {
        Debug.Log($"[DuckovNet-Scene] ReleaseSceneGate");
        
    }
}

public struct SceneVoteState : IDuckovSerializable
{
    public string sceneId;
    public bool isActive;
    public string initiatorId;
    public Dictionary<string, bool> playerReadyStates;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(sceneId ?? string.Empty);
        writer.Put(isActive);
        writer.Put(initiatorId ?? string.Empty);
        writer.Put(playerReadyStates?.Count ?? 0);
        if (playerReadyStates != null)
        {
            foreach (var kvp in playerReadyStates)
            {
                writer.Put(kvp.Key);
                writer.Put(kvp.Value);
            }
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        sceneId = reader.GetString();
        isActive = reader.GetBool();
        initiatorId = reader.GetString();
        var count = reader.GetInt();
        playerReadyStates = new Dictionary<string, bool>(count);
        for (int i = 0; i < count; i++)
        {
            var key = reader.GetString();
            var value = reader.GetBool();
            playerReadyStates[key] = value;
        }
    }
}
