using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class AILoadoutSnapshotMessage : IHybridMessage
{
    public string MessageType => "ai_loadout_snapshot";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public int AiId { get; set; }
    public List<AIEquipmentData> Equipment { get; set; }
    public List<AIWeaponData> Weapons { get; set; }
    public string FaceJson { get; set; }
    public string ModelName { get; set; }
    public int IconType { get; set; }
    public bool ShowName { get; set; }
    public string DisplayName { get; set; }
}

public class AIEquipmentData
{
    public int SlotHash { get; set; }
    public int TypeId { get; set; }
}

public class AIWeaponData
{
    public int SlotHash { get; set; }
    public string ItemId { get; set; }
}
