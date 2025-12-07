namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class AIHealthMessage : IHybridMessage
{
    public string MessageType => "ai_health";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public int EntityId { get; set; }
    public float MaxHealth { get; set; }
    public float CurrentHealth { get; set; }
    public bool IsDead { get; set; }
    
    public static AIHealthMessage FromHealth(int entityId, Health health)
    {
        return new AIHealthMessage
        {
            EntityId = entityId,
            MaxHealth = health.MaxHealth,
            CurrentHealth = health.CurrentHealth,
            IsDead = health.IsDead
        };
    }
}
