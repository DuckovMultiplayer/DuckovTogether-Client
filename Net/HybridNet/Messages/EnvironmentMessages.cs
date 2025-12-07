using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class EnvHurtRequestMessage : IHybridMessage
{
    public string MessageType => "env_hurt_request";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int TargetId { get; set; }
    public float Damage { get; set; }
    public Vector3 HitPoint { get; set; }
    public Vector3 HitNormal { get; set; }
}

public class EnvHurtEventMessage : IHybridMessage
{
    public string MessageType => "env_hurt_event";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int TargetId { get; set; }
    public float CurrentHealth { get; set; }
    public Vector3 HitPoint { get; set; }
    public Vector3 HitNormal { get; set; }
}

public class EnvDeadEventMessage : IHybridMessage
{
    public string MessageType => "env_dead_event";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int TargetId { get; set; }
}

public class EnvSyncRequestMessage : IHybridMessage
{
    public string MessageType => "env_sync_request";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
}

public class EnvironmentSyncRequestMessage : IHybridMessage
{
    public string MessageType => "environment_sync_request";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
}

public class EnvironmentSyncStateMessage : IHybridMessage
{
    public string MessageType => "environment_sync_state";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public byte[] StateData { get; set; }
}

public class EnvSyncStateMessage : IHybridMessage
{
    public string MessageType => "env_sync_state";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string EnvironmentData { get; set; }
}

public class DoorRequestSetMessage : IHybridMessage
{
    public string MessageType => "door_request_set";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int DoorKey { get; set; }
    public bool IsClosed { get; set; }
}

public class DoorStateMessage : IHybridMessage
{
    public string MessageType => "door_state";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int DoorKey { get; set; }
    public bool IsClosed { get; set; }
}

public class AudioEventMessage : IHybridMessage
{
    public string MessageType => "audio_event";
    public MessagePriority Priority => MessagePriority.Low;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string EventType { get; set; }
    public Vector3 Position { get; set; }
    public string Data { get; set; }
}

public class DiscoverRequestMessage : IHybridMessage
{
    public string MessageType => "discover_request";
    public MessagePriority Priority => MessagePriority.Low;
    public SerializationMode PreferredMode => SerializationMode.Binary;
}

public class DiscoverResponseMessage : IHybridMessage
{
    public string MessageType => "discover_response";
    public MessagePriority Priority => MessagePriority.Low;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string ServerInfo { get; set; }
}
