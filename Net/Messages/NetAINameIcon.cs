using DuckovNet;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net;

public static class AINameIconMessage
{
    public class NameIconData
    {
        public string type = "ai_name_icon";
        public int aiId;
        public int iconType;
        public bool showName;
        public string displayName;
    }
    
    public static void Server_Broadcast(int aiId, CharacterMainControl cmc)
    {
        if (!DedicatedServerMode.ShouldBroadcastState() || aiId == 0 || !cmc) 
            return;
        
        var iconType = 0;
        var showName = false;
        string displayName = null;
        
        try
        {
            var pr = cmc.characterPreset;
            if (pr)
            {
                try
                {
                    iconType = (int)AIName.FR_IconType(pr);
                }
                catch { }
                
                try
                {
                    if (iconType == 0 && pr.GetCharacterIcon() != null)
                        iconType = (int)AIName.FR_IconType(pr);
                }
                catch { }
                
                try
                {
                    showName = pr.showName;
                }
                catch { }
                
                var e = (CharacterIconTypes)iconType;
                if (!showName && (e == CharacterIconTypes.boss || e == CharacterIconTypes.elete))
                    showName = true;
                
                try
                {
                    displayName = pr.Name;
                }
                catch { }
            }
        }
        catch { }
        
        Debug.Log($"[Server AIIcon_Name] AI:{aiId} {cmc.characterPreset?.Name} Icon{AIName.FR_IconType(cmc.characterPreset)}");
        
        var data = new NameIconData
        {
            aiId = aiId,
            iconType = iconType,
            showName = showName,
            displayName = displayName
        };
        
        JsonMessage.BroadcastToAllClients(data, DeliveryMethod.ReliableOrdered);
    }
    
    public static void Client_Handle(string json)
    {
        var data = JsonMessage.HandleReceivedJson<NameIconData>(json);
        if (data == null) return;
        
        if (!AITool.aiById.TryGetValue(data.aiId, out var cmc) || !cmc)
            return;
        
        AIName.RefreshNameIconWithRetries(cmc, data.iconType, data.showName, data.displayName).Forget();
    }
}
