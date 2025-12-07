using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class AITransformMessage : IHybridMessage, ISpatialMessage, IAITransformData
{
    public string MessageType => "ai_transform";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int EntityId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float ForwardX { get; set; }
    public float ForwardY { get; set; }
    public float ForwardZ { get; set; }
    public float Timestamp { get; set; }
    
    public Vector3 Position => new Vector3(PosX, PosY, PosZ);
    public Vector3 Forward => new Vector3(ForwardX, ForwardY, ForwardZ);
    
    public static AITransformMessage FromTransform(int entityId, Transform transform, Transform modelTransform)
    {
        var forward = modelTransform.rotation * Vector3.forward;
        
        return new AITransformMessage
        {
            EntityId = entityId,
            PosX = transform.position.x,
            PosY = transform.position.y,
            PosZ = transform.position.z,
            ForwardX = forward.x,
            ForwardY = forward.y,
            ForwardZ = forward.z,
            Timestamp = Time.time
        };
    }
}
