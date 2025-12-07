















using EscapeFromDuckovCoopMod.Net;

namespace EscapeFromDuckovCoopMod;




public class JsonMessageRouter : MonoBehaviour
{
    public static JsonMessageRouter Instance { get; private set; }
    
    private void Awake()
    {
        Instance = this;
    }
    
    
    
    
    [System.Serializable]
    private class BaseJsonMessage
    {
        public string type;
    }
    
    public void RouteMessage(string jsonData)
    {
        HandleJsonMessageInternal(jsonData, null);
    }

    
    
    
    
    
    
    public static void HandleJsonMessage(NetDataReader reader, NetPeer fromPeer = null)
    {
        if (reader == null)
        {
            Debug.LogWarning("[JsonRouter] reader为空");
            return;
        }

        var json = reader.GetString();
        HandleJsonMessageInternal(json, fromPeer);
    }
    
    public static void HandleJsonMessageInternal(string json, NetPeer fromPeer = null)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[JsonRouter] 收到空JSON消息");
            return;
        }

        try
        {
            
            var baseMsg = JsonUtility.FromJson<BaseJsonMessage>(json);
            if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type))
            {
                Debug.LogWarning($"[JsonRouter] JSON消息缺少type字段: {json}");
                return;
            }

            Debug.Log($"[JsonRouter] 收到JSON消息，type={baseMsg.type}");

            
            switch (baseMsg.type)
            {
                case "setId":
                    HandleSetIdMessage(json);
                    break;

                case "lootFullSync":
                    
                    LootFullSyncMessage.Client_OnLootFullSync(json);
                    break;

                case "sceneVote":
                    
                    SceneVoteMessage.Client_HandleVoteState(json);
                    break;

                case "sceneVoteRequest":
                    
                    SceneVoteMessage.Host_HandleVoteRequest(json);
                    break;

                case "sceneVoteReady":
                    
                    SceneVoteMessage.Host_HandleReadyToggle(json);
                    break;

                case "forceSceneLoad":
                    
                    SceneVoteMessage.Client_HandleForceSceneLoad(json);
                    break;

                case "updateClientStatus":
                    
                    HandleClientStatusMessage(json, fromPeer);
                    break;

                case "kick":
                    
                    KickMessage.Client_HandleKickMessage(json);
                    break;

                case "test":
                    
                    HandleTestMessage(json);
                    break;
                
                case "ai_seed_snapshot":
                    AISeedMessage.Client_HandleSeedSnapshot(json);
                    break;
                
                case "ai_seed_patch":
                    AISeedMessage.Client_HandleSeedPatch(json);
                    break;
                
                case "ai_loadout":
                    AILoadoutMessage.Client_HandleLoadout(json);
                    break;
                
                case "ai_transform_snapshot":
                    AITransformMessage.Client_HandleSnapshot(json);
                    break;
                
                case "ai_anim_snapshot":
                    AIAnimationMessage.Client_HandleSnapshot(json);
                    break;
                
                case "ai_health_sync":
                    AIHealthMessage.Client_HandleHealthSync(json);
                    break;
                
                case "ai_health_report":
                    AIHealthMessage.Host_HandleHealthReport(fromPeer, json);
                    break;
                
                case "ai_name_icon":
                    AINameIconMessage.Client_Handle(json);
                    break;

                default:
                    Debug.LogWarning($"[JsonRouter] 未知的消息类型: {baseMsg.type}");
                    break;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[JsonRouter] 处理JSON消息失败: {ex.Message}\nJSON: {json}");
        }
    }

    
    
    
    private static void HandleSetIdMessage(string json)
    {
        var service = NetService.Instance;
        if (service == null)
        {
            Debug.LogWarning("[JsonRouter] NetService未初始化");
            return;
        }

        if (service.IsServer)
        {
            Debug.LogWarning("[JsonRouter] 主机不应该接收SetId消息");
            return;
        }

        try
        {
            var data = JsonUtility.FromJson<SetIdMessage.SetIdData>(json);
            if (data == null)
            {
                Debug.LogError("[JsonRouter] SetId消息解析失败");
                return;
            }

            var oldId = service.localPlayerStatus?.EndPoint;
            var newId = data.networkId;

            Debug.Log($"[SetId] 收到主机告知的网络ID: {newId}");
            Debug.Log($"[SetId] 旧ID: {oldId}");

            
            if (service.localPlayerStatus != null)
            {
                service.localPlayerStatus.EndPoint = newId;
                Debug.Log($"[SetId] ✓ 已更新 localPlayerStatus.EndPoint: {oldId} → {newId}");
            }
            else
            {
                Debug.LogWarning("[SetId] localPlayerStatus为空，无法更新");
            }

            
            CleanupSelfDuplicate(oldId, newId);

            
            
            ClientStatusMessage.Client_SendStatusUpdate();
            Debug.Log("[SetId] ✓ 已发送客户端状态更新（包含 SteamID）");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[JsonRouter] 处理SetId消息失败: {ex.Message}");
        }
    }

    
    
    
    private static void CleanupSelfDuplicate(string oldId, string newId)
    {
        var service = NetService.Instance;
        if (service == null || service.clientRemoteCharacters == null)
            return;

        var toRemove = new System.Collections.Generic.List<string>();

        foreach (var kv in service.clientRemoteCharacters)
        {
            var playerId = kv.Key;
            var go = kv.Value;

            
            if (playerId == oldId || playerId == newId)
            {
                Debug.LogWarning($"[SetId] 发现自己的远程副本，准备删除: {playerId}");
                toRemove.Add(playerId);
                if (go != null)
                {
                    UnityEngine.Object.Destroy(go);
                    Debug.Log($"[SetId] ✓ 已删除远程副本GameObject: {playerId}");
                }
            }
        }

        foreach (var id in toRemove)
        {
            service.clientRemoteCharacters.Remove(id);
            Debug.Log($"[SetId] ✓ 已从clientRemoteCharacters移除: {id}");
        }

        if (toRemove.Count > 0)
        {
            Debug.Log($"[SetId] ✓ 清理完成，共删除 {toRemove.Count} 个自己的远程副本");
        }
    }

    
    
    
    private static void HandleClientStatusMessage(string json, NetPeer fromPeer)
    {
        var service = NetService.Instance;
        if (service == null || !service.IsServer)
        {
            Debug.LogWarning("[JsonRouter] 只有主机可以接收客户端状态消息");
            return;
        }

        if (fromPeer == null)
        {
            Debug.LogWarning("[JsonRouter] fromPeer为空，无法处理客户端状态消息");
            return;
        }

        try
        {
            ClientStatusMessage.Host_HandleClientStatus(fromPeer, json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[JsonRouter] 处理客户端状态消息失败: {ex.Message}");
        }
    }

    
    
    
    private static void HandleTestMessage(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<JsonMessage.TestJsonData>(json);
            Debug.Log($"[JsonRouter] 测试消息: {data.message} (时间: {data.timestamp}, 随机值: {data.randomValue})");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[JsonRouter] 处理测试消息失败: {ex.Message}");
        }
    }
}
