using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class ItemDropRequestMessage : IHybridMessage
{
    public string MessageType => "item_drop_request";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public uint Token { get; set; }
    public string PlayerId { get; set; }
    public byte[] ItemData { get; set; }
    public Vector3 Position { get; set; }
}

public class ItemSpawnMessage : IHybridMessage
{
    public string MessageType => "item_spawn";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public uint Token { get; set; }
    public uint DropId { get; set; }
    public byte[] ItemData { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
}

public class ItemPickupRequestMessage : IHybridMessage
{
    public string MessageType => "item_pickup_request";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public uint DropId { get; set; }
    public string PlayerId { get; set; }
}

public class ItemDespawnMessage : IHybridMessage
{
    public string MessageType => "item_despawn";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public uint DropId { get; set; }
}
