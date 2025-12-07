using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class AIAnimationMessage : IHybridMessage, IAIAnimationData
{
    public string MessageType => "ai_animation";
    public MessagePriority Priority => MessagePriority.Background;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int EntityId { get; set; }
    public float Speed { get; set; }
    public float DirX { get; set; }
    public float DirY { get; set; }
    public int HandState { get; set; }
    public bool GunReady { get; set; }
    public bool Dashing { get; set; }
    public bool Attacking { get; set; }
    
    public static AIAnimationMessage FromAnimator(int entityId, Animator animator)
    {
        return new AIAnimationMessage
        {
            EntityId = entityId,
            Speed = animator.GetFloat(Animator.StringToHash("MoveSpeed")),
            DirX = animator.GetFloat(Animator.StringToHash("MoveDirX")),
            DirY = animator.GetFloat(Animator.StringToHash("MoveDirY")),
            HandState = animator.GetInteger(Animator.StringToHash("HandState")),
            GunReady = animator.GetBool(Animator.StringToHash("GunReady")),
            Dashing = animator.GetBool(Animator.StringToHash("Dashing")),
            Attacking = false
        };
    }
}
