using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class PlayerEquipmentUpdateMessage : IHybridMessage
{
    public string MessageType => "player_equipment_update";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public int SlotHash { get; set; }
    public string ItemId { get; set; }
}

public class PlayerWeaponUpdateMessage : IHybridMessage
{
    public string MessageType => "player_weapon_update";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public int SlotHash { get; set; }
    public string ItemId { get; set; }
}

public class PlayerDeadTreeMessage : IHybridMessage
{
    public string MessageType => "player_dead_tree";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public string ItemTreeJson { get; set; }
}

public class AIHealthSyncMessage : IHybridMessage
{
    public string MessageType => "ai_health_sync";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int AiId { get; set; }
    public float MaxHealth { get; set; }
    public float CurrentHealth { get; set; }
}
