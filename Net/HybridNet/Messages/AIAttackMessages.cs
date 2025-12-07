using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class AIAttackSwingMessage : IHybridMessage
{
    public string MessageType => "ai_attack_swing";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int AiId { get; set; }
    public int AttackIndex { get; set; }
}

public class AIAttackTellMessage : IHybridMessage
{
    public string MessageType => "ai_attack_tell";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int AiId { get; set; }
    public Vector3 Position { get; set; }
    public float Duration { get; set; }
}

public class AIHealthReportMessage : IHybridMessage
{
    public string MessageType => "ai_health_report";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int AiId { get; set; }
    public float MaxHealth { get; set; }
    public float CurrentHealth { get; set; }
}

public class AINameIconMessage : IHybridMessage
{
    public string MessageType => "ai_name_icon";
    public MessagePriority Priority => MessagePriority.Low;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public int AiId { get; set; }
    public int IconType { get; set; }
    public string DisplayName { get; set; }
    public bool ShowName { get; set; }
}
