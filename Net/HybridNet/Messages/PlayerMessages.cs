using UnityEngine;
using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class PlayerStatusUpdateMessage : IHybridMessage
{
    public string MessageType => "player_status_update";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public List<PlayerStatusData> Players { get; set; }
}

public class PlayerStatusData
{
    public string EndPoint { get; set; }
    public string PlayerName { get; set; }
    public int Latency { get; set; }
    public bool IsInGame { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public string SceneId { get; set; }
    public List<EquipmentSyncData> EquipmentList { get; set; }
    public List<WeaponSyncData> WeaponList { get; set; }
}

public class ClientStatusUpdateMessage : IHybridMessage
{
    public string MessageType => "client_status_update";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string EndPoint { get; set; }
    public bool IsInGame { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
}

public class PlayerPositionMessage : IHybridMessage, ISpatialMessage
{
    public string MessageType => "player_position";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float DirX { get; set; }
    public float DirY { get; set; }
    public float DirZ { get; set; }
    
    public int EntityId => PlayerId.GetHashCode();
    public Vector3 Position => new Vector3(PosX, PosY, PosZ);
    public Vector3 Direction => new Vector3(DirX, DirY, DirZ);
}

public class PlayerAnimationMessage : IHybridMessage
{
    public string MessageType => "player_animation";
    public MessagePriority Priority => MessagePriority.Background;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public float MoveSpeed { get; set; }
    public float MoveDirX { get; set; }
    public float MoveDirY { get; set; }
    public bool IsDashing { get; set; }
    public bool IsAttacking { get; set; }
    public int HandState { get; set; }
    public bool GunReady { get; set; }
    public int StateHash { get; set; }
    public float NormTime { get; set; }
}

public class PlayerEquipmentMessage : IHybridMessage
{
    public string MessageType => "player_equipment";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
    public int SlotHash { get; set; }
    public string ItemId { get; set; }
}

public class PlayerWeaponMessage : IHybridMessage
{
    public string MessageType => "player_weapon";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
    public int SlotHash { get; set; }
    public string ItemId { get; set; }
}

public class PlayerHealthReportMessage : IHybridMessage
{
    public string MessageType => "player_health_report";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public float MaxHealth { get; set; }
    public float CurrentHealth { get; set; }
}

public class PlayerHealthAuthSelfMessage : IHybridMessage
{
    public string MessageType => "player_health_auth_self";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public float MaxHealth { get; set; }
    public float CurrentHealth { get; set; }
}

public class PlayerHealthAuthRemoteMessage : IHybridMessage
{
    public string MessageType => "player_health_auth_remote";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public float MaxHealth { get; set; }
    public float CurrentHealth { get; set; }
}

public class PlayerAppearanceMessage : IHybridMessage
{
    public string MessageType => "player_appearance";
    public MessagePriority Priority => MessagePriority.Low;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
    public string FaceJson { get; set; }
}

public class PlayerHurtEventMessage : IHybridMessage
{
    public string MessageType => "player_hurt_event";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public float Damage { get; set; }
    public Vector3 HitPoint { get; set; }
    public Vector3 HitNormal { get; set; }
    public float CritRate { get; set; }
    public bool IsCrit { get; set; }
    public int WeaponItemId { get; set; }
    public float BleedChance { get; set; }
    public bool IsExplosion { get; set; }
}

public class PlayerDeadMessage : IHybridMessage
{
    public string MessageType => "player_dead";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public string ItemTreeJson { get; set; }
}

public class RemoteCharacterCreateMessage : IHybridMessage
{
    public string MessageType => "remote_create";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
    public int SceneId { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public string CharacterData { get; set; }
}

public class RemoteCharacterDespawnMessage : IHybridMessage
{
    public string MessageType => "remote_character_despawn";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
}

public class PlayerJoinMessage : IHybridMessage
{
    public string MessageType => "player_join";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
    public string PlayerName { get; set; }
}

public class PlayerLeaveMessage : IHybridMessage
{
    public string MessageType => "player_leave";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
}

public class PlayerInventoryMessage : IHybridMessage
{
    public string MessageType => "player_inventory";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
    public string InventoryData { get; set; }
}
