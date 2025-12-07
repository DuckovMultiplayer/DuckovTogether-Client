using Steamworks;

namespace EscapeFromDuckovCoopMod.Net;

public static class ClientStatusMessage
{
    public static void Client_SendStatusUpdate()
    {
        var service = NetService.Instance;
        if (service == null || service.connectedPeer == null || service.IsServer)
        {
            Debug.LogWarning("[ClientStatus] 无法发送状态：Service或Peer为空，或为服务器端");
            return;
        }

        var localStatus = service.localPlayerStatus;
        if (localStatus == null)
        {
            Debug.LogWarning("[ClientStatus] 无法发送状态：localPlayerStatus为空");
            return;
        }

        var writer = service.writer;
        var peer = service.connectedPeer;

        Debug.Log($"[ClientStatus] 准备发送CLIENT_STATUS_UPDATE到服务器，玩家：{localStatus.PlayerName}, IsInGame:{localStatus.IsInGame}");

        writer.Reset();
        writer.Put((byte)1);
        writer.Put(localStatus.EndPoint ?? "Client");
        writer.Put(localStatus.PlayerName ?? "Unknown");
        writer.Put(localStatus.IsInGame);
        writer.PutVector3(localStatus.Position);
        writer.PutQuaternion(localStatus.Rotation);
        writer.Put(localStatus.SceneId ?? string.Empty);
        
        writer.Put(localStatus.EquipmentList?.Count ?? 0);
        if (localStatus.EquipmentList != null)
        {
            foreach (var eq in localStatus.EquipmentList)
                eq.Serialize(writer);
        }
        
        writer.Put(localStatus.WeaponList?.Count ?? 0);
        if (localStatus.WeaponList != null)
        {
            foreach (var w in localStatus.WeaponList)
                w.Serialize(writer);
        }

        peer.Send(writer, DeliveryMethod.ReliableOrdered);
        Debug.Log($"[ClientStatus] 已发送CLIENT_STATUS_UPDATE消息到服务器");
    }
    
    public static void SendPlayerInfoUpdateToClients()
    {
    }
    
    public static void Host_HandleClientStatus(NetPeer fromPeer, string jsonData)
    {
    }
    
    public static string GetSteamNameFromSteamId(string steamId)
    {
        if (string.IsNullOrEmpty(steamId))
            return "Unknown";
            
        if (ulong.TryParse(steamId, out var id))
        {
            var csteamId = new CSteamID(id);
            return SteamFriends.GetFriendPersonaName(csteamId);
        }
        
        return "Unknown";
    }
}
