using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class EnvironmentHurtEventMessage : IHybridMessage
{
    public string MessageType => "environment_hurt_event";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int ObjectId { get; set; }
    public float NewHealth { get; set; }
    public Vector3 HitPoint { get; set; }
    public Vector3 HitNormal { get; set; }
}

public class EnvironmentDeadEventMessage : IHybridMessage
{
    public string MessageType => "environment_dead_event";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int ObjectId { get; set; }
    public Vector3 HitPoint { get; set; }
    public Vector3 HitNormal { get; set; }
}
