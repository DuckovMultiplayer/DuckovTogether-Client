using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class LootRequestOpenMessage : IHybridMessage
{
    public string MessageType => "loot_request_open";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int LootUid { get; set; }
    public int Scene { get; set; }
    public Vector3 PositionHint { get; set; }
}

public class LootStateMessage : IHybridMessage
{
    public string MessageType => "loot_state";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public int LootUid { get; set; }
    public string ContainerSnapshot { get; set; }
}

public class LootRequestPutMessage : IHybridMessage
{
    public string MessageType => "loot_request_put";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public int LootUid { get; set; }
    public int PreferPos { get; set; }
    public string ItemSnapshot { get; set; }
    public int Token { get; set; }
}

public class LootRequestTakeMessage : IHybridMessage
{
    public string MessageType => "loot_request_take";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int LootUid { get; set; }
    public int SlotIndex { get; set; }
    public int Position { get; set; }
    public int Token { get; set; }
}

public class LootPutOkMessage : IHybridMessage
{
    public string MessageType => "loot_put_ok";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int Token { get; set; }
    public int SlotIndex { get; set; }
}

public class LootTakeOkMessage : IHybridMessage
{
    public string MessageType => "loot_take_ok";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public int Token { get; set; }
    public string ItemSnapshot { get; set; }
}

public class LootDenyMessage : IHybridMessage
{
    public string MessageType => "loot_deny";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int Token { get; set; }
    public string Reason { get; set; }
}

public class LootRequestSplitMessage : IHybridMessage
{
    public string MessageType => "loot_request_split";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int LootUid { get; set; }
    public int SlotIndex { get; set; }
    public int SplitCount { get; set; }
}

public class LootSlotUnplugMessage : IHybridMessage
{
    public string MessageType => "loot_slot_unplug";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public int LootUid { get; set; }
    public string ItemPath { get; set; }
    public int SlotHash { get; set; }
}

public class LootSlotPlugMessage : IHybridMessage
{
    public string MessageType => "loot_slot_plug";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public int LootUid { get; set; }
    public string ItemPath { get; set; }
    public int SlotHash { get; set; }
    public string AttachmentSnapshot { get; set; }
}

public class DeadLootSpawnMessage : IHybridMessage
{
    public string MessageType => "dead_loot_spawn";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int Scene { get; set; }
    public int AiId { get; set; }
    public int LootUid { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
}

public class DeadLootDespawnMessage : IHybridMessage
{
    public string MessageType => "dead_loot_despawn";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int LootUid { get; set; }
}
