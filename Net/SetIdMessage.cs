















using LiteNetLib;
using UnityEngine;

namespace EscapeFromDuckovCoopMod;





public static class SetIdMessage
{
    
    
    
    [System.Serializable]
    public class SetIdData
    {
        public string type = "setId";  
        public string networkId;        
        public string timestamp;        
    }

    
    
    
    
    public static void SendSetIdToPeer(NetPeer peer)
    {
        if (peer == null)
        {
            Debug.LogWarning("[SetId] SendSetIdToPeer: peer为空");
            return;
        }

        var service = NetService.Instance;
        if (service == null || !service.IsServer)
        {
            Debug.LogWarning("[SetId] SendSetIdToPeer 只能在服务器端调用");
            return;
        }

        var networkId = peer.EndPoint.ToString();
        var data = new SetIdData
        {
            networkId = networkId,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        };

        JsonMessage.SendToPeer(peer, data, DeliveryMethod.ReliableOrdered);
        Debug.Log($"[SetId] 发送SetId给客户端: {networkId}");
    }

    
    
    
    
    public static void HandleSetIdMessage(NetPacketReader reader)
    {
        var service = NetService.Instance;
        if (service == null)
        {
            Debug.LogWarning("[SetId] NetService未初始化");
            return;
        }

        if (service.IsServer)
        {
            Debug.LogWarning("[SetId] 主机不应该接收SetId消息");
            return;
        }

        var jsonData = reader.GetString();
        var data = JsonMessage.HandleReceivedJson<SetIdData>(jsonData);
        if (data != null)
        {
            if (data.type != "setId")
            {
                Debug.LogWarning($"[SetId] 消息类型不匹配: {data.type}");
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

    
    
    
    public static bool IsSetIdMessage(string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            
            return json.Contains("\"type\"") && json.Contains("\"setId\"");
        }
        catch
        {
            return false;
        }
    }
}
