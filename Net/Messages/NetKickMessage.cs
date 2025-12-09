















using Steamworks;
using UnityEngine;

namespace EscapeFromDuckovCoopMod;




[System.Serializable]
public class KickMessageData
{
    public string type = "kick";
    public ulong targetSteamId;  
    public string reason;  
}




public static class KickMessage
{
    
    
    
    
    
    public static void Server_KickPlayer(ulong targetSteamId, string reason = "被主机踢出")
    {
        var service = ModBehaviourF.Instance;
        if (service == null || !service.IsServer)
        {
            Debug.LogWarning("[KickMessage] 只有主机可以踢人");
            return;
        }

        var kickData = new KickMessageData
        {
            type = "kick",
            targetSteamId = targetSteamId,
            reason = reason
        };

        var json = JsonUtility.ToJson(kickData);
        Debug.Log($"[KickMessage] 主机踢出玩家: SteamID={targetSteamId}, 原因={reason}");

        
        var steamIdStr = targetSteamId.ToString();
        if (Utils.Database.PlayerInfoDatabase.Instance.RemovePlayer(steamIdStr))
        {
            Debug.Log($"[KickMessage] ✓ 已从数据库删除玩家: {steamIdStr}");
        }
        else
        {
            Debug.LogWarning($"[KickMessage] 从数据库删除玩家失败: {steamIdStr}");
        }

        
        JsonMessage.BroadcastToAllClients(json, LiteNetLib.DeliveryMethod.ReliableOrdered);
    }

    
    
    
    
    public static void Client_HandleKickMessage(string json)
    {
        try
        {
            var kickData = JsonUtility.FromJson<KickMessageData>(json);

            if (kickData == null || kickData.type != "kick")
            {
                return;  
            }

            
            if (SteamManager.Initialized)
            {
                var mySteamId = SteamUser.GetSteamID().m_SteamID;

                if (mySteamId == kickData.targetSteamId)
                {
                    Debug.LogWarning($"[KickMessage] 收到踢人消息: {kickData.reason}");

                    
                    var service = ModBehaviourF.Instance;
                    if (service != null)
                    {
                        
                        service.status = $"已被踢出: {kickData.reason}";

                        
                        if (service.connectedPeer != null)
                        {
                            service.connectedPeer.Disconnect();
                        }

                        
                        service.StopNetwork();

                        
                        if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsInLobby)
                        {
                            SteamLobbyManager.Instance.LeaveLobby();
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[KickMessage] 处理踢人消息失败: {ex.Message}");
        }
    }

    
    
    
    public static bool IsKickMessage(string json)
    {
        try
        {
            
            return json.Contains("\"type\":\"kick\"") || json.Contains("\"type\": \"kick\"");
        }
        catch
        {
            return false;
        }
    }
}
