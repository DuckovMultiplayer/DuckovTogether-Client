using System.Collections.Generic;
using DuckovNet;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net;

public static class AILoadoutMessage
{
    public class LoadoutData
    {
        public string type = "ai_loadout";
        public byte version = 5;
        public int aiId;
        public List<EquipmentSlot> equipment;
        public List<WeaponSlot> weapons;
        public string faceJson;
        public string modelName;
        public int iconType;
        public bool showName;
        public string displayName;
    }
    
    public class EquipmentSlot
    {
        public int slotHash;
        public int typeId;
    }
    
    public class WeaponSlot
    {
        public int slotHash;
        public int typeId;
    }
    
    public static void Server_BroadcastLoadout(int aiId, CharacterMainControl cmc)
    {
        if (cmc == null) return;
        
        var data = new LoadoutData
        {
            aiId = aiId,
            equipment = new List<EquipmentSlot>(),
            weapons = new List<WeaponSlot>()
        };
        
        var eqList = AITool.GetLocalAIEquipment(cmc);
        foreach (var eq in eqList)
        {
            var tid = 0;
            if (!string.IsNullOrEmpty(eq.ItemId))
                int.TryParse(eq.ItemId, out tid);
                
            data.equipment.Add(new EquipmentSlot 
            { 
                slotHash = eq.SlotHash, 
                typeId = tid 
            });
        }
        
        var gun = cmc.GetGun();
        var melee = cmc.GetMeleeWeapon();
        if (gun != null)
        {
            data.weapons.Add(new WeaponSlot
            {
                slotHash = (int)gun.handheldSocket,
                typeId = gun.Item ? gun.Item.TypeID : 0
            });
        }
        if (melee != null)
        {
            data.weapons.Add(new WeaponSlot
            {
                slotHash = (int)melee.handheldSocket,
                typeId = melee.Item ? melee.Item.TypeID : 0
            });
        }
        
        data.modelName = AIName.NormalizePrefabName(cmc.characterModel ? cmc.characterModel.name : null);
        
        var iconType = 0;
        var showName = false;
        try
        {
            var pr = cmc.characterPreset;
            if (pr)
            {
                iconType = (int)AIName.FR_IconType(pr);
                var e = (CharacterIconTypes)iconType;
                if (e == CharacterIconTypes.none && pr.GetCharacterIcon() != null)
                    iconType = (int)AIName.FR_IconType(pr);
                    
                if (!showName && (e == CharacterIconTypes.boss || e == CharacterIconTypes.elete))
                    showName = true;
            }
        }
        catch { }
        
        data.iconType = iconType;
        data.showName = showName;
        
        try
        {
            var preset = cmc.characterPreset;
            if (preset) data.displayName = preset.Name;
        }
        catch { }
        
        Debug.Log($"[AI-LOADOUT] ver={data.version} aiId={aiId} model='{data.modelName}' icon={iconType} showName={showName}");
        
        JsonMessage.BroadcastToAllClients(data, DeliveryMethod.ReliableOrdered);
        
        if (iconType == (int)CharacterIconTypes.none)
            AIRequest.Instance?.Server_TryRebroadcastIconLater(aiId, cmc);
    }
    
    public static void Client_HandleLoadout(string json)
    {
        var data = JsonMessage.HandleReceivedJson<LoadoutData>(json);
        if (data == null) return;
        
        var aiHandle = COOPManager.AIHandle;
        if (aiHandle == null) return;
        
        var equips = new List<(int slot, int tid)>();
        foreach (var eq in data.equipment)
        {
            equips.Add((eq.slotHash, eq.typeId));
        }
        
        var weapons = new List<(int slot, int tid)>();
        foreach (var w in data.weapons)
        {
            weapons.Add((w.slotHash, w.typeId));
        }
        
        if (!AITool.aiById.TryGetValue(data.aiId, out var cmc) || !cmc)
        {
            aiHandle.pendingAiLoadouts[data.aiId] = (
                equips,
                weapons,
                data.faceJson,
                data.modelName,
                data.iconType,
                data.showName,
                data.displayName
            );
            return;
        }
        
        aiHandle.Client_ApplyAiLoadout(
            data.aiId,
            equips,
            weapons,
            data.faceJson,
            data.modelName,
            data.iconType,
            data.showName,
            data.displayName
        ).Forget();
    }
}
