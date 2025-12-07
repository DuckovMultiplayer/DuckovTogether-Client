using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class FireRequestMessage : IHybridMessage
{
    public string MessageType => "fire_request";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public string ShooterId { get; set; }
    public int WeaponType { get; set; }
    public Vector3 Muzzle { get; set; }
    public Vector3 BaseDir { get; set; }
    public Vector3 FirstCheckStart { get; set; }
    public Vector2 ClientScatter { get; set; }
    public bool Ads01 { get; set; }
    public byte[] ProjectilePayload { get; set; }
    public Vector3 Origin { get; set; }
    public Vector3 Direction { get; set; }
    public string WeaponId { get; set; }
}

public class FireEventMessage : IHybridMessage
{
    public string MessageType => "fire_event";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string ShooterId { get; set; }
    public int WeaponTypeId { get; set; }
    public Vector3 MuzzlePosition { get; set; }
    public Vector3 Direction { get; set; }
    public byte[] PayloadData { get; set; }
    public bool Hit { get; set; }
    public Vector3 HitPoint { get; set; }
    public Vector3 HitNormal { get; set; }
}

public class GrenadeThrowRequestMessage : IHybridMessage
{
    public string MessageType => "grenade_throw_request";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public int TypeId { get; set; }
    public Vector3 StartPosition { get; set; }
    public Vector3 Velocity { get; set; }
    public bool CreateCrater { get; set; }
    public float CameraShake { get; set; }
    public float Damage { get; set; }
    public bool DelayOnHit { get; set; }
    public float FuseDelay { get; set; }
    public bool IsMine { get; set; }
    public float MineRange { get; set; }
    public string GrenadeType { get; set; }
}

public class GrenadeSpawnMessage : IHybridMessage
{
    public string MessageType => "grenade_spawn";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int GrenadeId { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public string GrenadeType { get; set; }
}

public class GrenadeExplodeMessage : IHybridMessage
{
    public string MessageType => "grenade_explode";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int GrenadeId { get; set; }
    public Vector3 Position { get; set; }
}

public class MeleeAttackRequestMessage : IHybridMessage
{
    public string MessageType => "melee_attack_request";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public float AnimDelay { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Forward { get; set; }
    public Vector3 SnapPos { get; set; }
    public Vector3 SnapDir { get; set; }
    public int DelayFrames { get; set; }
}

public class MeleeAttackSwingMessage : IHybridMessage
{
    public string MessageType => "melee_attack_swing";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public float AnimDelay { get; set; }
}

public class MeleeHitReportMessage : IHybridMessage
{
    public string MessageType => "melee_hit_report";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public float Damage { get; set; }
    public float ArmorPiercing { get; set; }
    public float CritDamageFactor { get; set; }
    public float CritRate { get; set; }
    public bool IsCrit { get; set; }
    public Vector3 HitPoint { get; set; }
    public Vector3 HitNormal { get; set; }
    public int WeaponItemId { get; set; }
    public float BleedChance { get; set; }
    public bool IsExplosion { get; set; }
}

public class BuffApplySelfMessage : IHybridMessage
{
    public string MessageType => "buff_apply_self";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string BuffId { get; set; }
    public float Duration { get; set; }
    public string Data { get; set; }
}

public class BuffApplyProxyMessage : IHybridMessage
{
    public string MessageType => "buff_apply_proxy";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
    public string BuffId { get; set; }
    public float Duration { get; set; }
    public string Data { get; set; }
}
