using LiteNetLib;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public static class HybridNetBridge
{
    public static void Initialize()
    {
        RegisterPlayerHandlers();
        RegisterCombatHandlers();
        RegisterItemHandlers();
        RegisterLootHandlers();
        RegisterSceneHandlers();
        RegisterAIHandlers();
        RegisterVoiceHandlers();
        Handlers.AIInstanceHandler.RegisterHandlers();
        
        UnityEngine.Debug.Log("[HybridNet] 所有消息处理器注册完成");
    }
    
    private static void RegisterPlayerHandlers()
    {
        HybridNetCore.RegisterHandler<PlayerStatusUpdateMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<ClientStatusUpdateMessage>((msg, peer) =>
        {
            COOPManager.ClientHandle?.HandleClientStatusUpdate(peer, null);
        });
        
        HybridNetCore.RegisterHandler<PlayerPositionMessage>((msg, peer) =>
        {
            COOPManager.ClientHandle?.HandlePlayerPositionFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<PlayerAnimationMessage>((msg, peer) =>
        {
            COOPManager.ClientHandle?.HandlePlayerAnimationFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<PlayerEquipmentMessage>((msg, peer) =>
        {
            COOPManager.ClientHandle?.HandlePlayerEquipmentFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<PlayerStatusUpdateMessage>((msg, peer) =>
        {
            COOPManager.ClientHandle?.HandlePlayerHealthFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<PlayerHealthReportMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<PlayerHealthAuthSelfMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<PlayerHealthAuthRemoteMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<PlayerAppearanceMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<PlayerHurtEventMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<PlayerJoinMessage>((msg, peer) =>
        {
        });
        
        HybridNetCore.RegisterHandler<PlayerDeadMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<RemoteCharacterCreateMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<RemoteCharacterDespawnMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<BuffApplySelfMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<BuffApplyProxyMessage>((msg, peer) =>
        {
            
        });
    }
    
    private static void RegisterCombatHandlers()
    {
        HybridNetCore.RegisterHandler<FireRequestMessage>((msg, peer) =>
        {
            COOPManager.WeaponHandle?.HandleFireRequestFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<FireEventMessage>((msg, peer) =>
        {
            COOPManager.WeaponHandle?.HandleFireEventFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<GrenadeThrowRequestMessage>((msg, peer) =>
        {
            COOPManager.GrenadeM?.HandleGrenadeThrowRequestFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<GrenadeSpawnMessage>((msg, peer) =>
        {
            COOPManager.GrenadeM?.HandleGrenadeSpawn(null);
        });
        
        HybridNetCore.RegisterHandler<GrenadeExplodeMessage>((msg, peer) =>
        {
            COOPManager.GrenadeM?.HandleGrenadeExplode(null);
        });
        
        HybridNetCore.RegisterHandler<MeleeAttackRequestMessage>((msg, peer) =>
        {
            COOPManager.WeaponHandle?.HandleMeleeAttackRequestFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<MeleeAttackSwingMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<MeleeHitReportMessage>((msg, peer) =>
        {
            COOPManager.WeaponHandle?.HandleMeleeHitReportFromMessage(peer, msg);
        });
    }
    
    private static void RegisterItemHandlers()
    {
        HybridNetCore.RegisterHandler<ItemDropRequestMessage>((msg, peer) =>
        {
            COOPManager.ItemHandle?.HandleItemDropRequestFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<ItemSpawnMessage>((msg, peer) =>
        {
            COOPManager.ItemHandle?.HandleItemSpawn(null);
        });
        
        HybridNetCore.RegisterHandler<ItemPickupRequestMessage>((msg, peer) =>
        {
            COOPManager.ItemHandle?.HandleItemPickupRequestFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<ItemDespawnMessage>((msg, peer) =>
        {
            COOPManager.ItemHandle?.HandleItemDespawn(null);
        });
    }
    
    private static void RegisterLootHandlers()
    {
        HybridNetCore.RegisterHandler<LootRequestOpenMessage>((msg, peer) =>
        {
            COOPManager.LootNet?.Server_HandleLootOpenRequestFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<LootStateMessage>((msg, peer) =>
        {
            COOPManager.LootNet?.Client_ApplyLootboxState(null);
        });
        
        HybridNetCore.RegisterHandler<LootRequestPutMessage>((msg, peer) =>
        {
            COOPManager.LootNet?.Server_HandleLootPutRequestFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<LootRequestTakeMessage>((msg, peer) =>
        {
            COOPManager.LootNet?.Server_HandleLootTakeRequestFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<LootPutOkMessage>((msg, peer) =>
        {
            COOPManager.LootNet?.Client_OnLootPutOk(null);
        });
        
        HybridNetCore.RegisterHandler<LootTakeOkMessage>((msg, peer) =>
        {
            COOPManager.LootNet?.Client_OnLootTakeOk(null);
        });
        
        HybridNetCore.RegisterHandler<LootDenyMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<LootRequestSplitMessage>((msg, peer) =>
        {
            COOPManager.LootNet?.Server_HandleLootSplitRequestFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<LootSlotUnplugMessage>((msg, peer) =>
        {
            COOPManager.LootNet?.Server_HandleLootSlotUnplugRequestFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<LootSlotPlugMessage>((msg, peer) =>
        {
            COOPManager.LootNet?.Server_HandleLootSlotPlugRequestFromMessage(peer, msg);
        });
        
        HybridNetCore.RegisterHandler<DeadLootSpawnMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<DeadLootDespawnMessage>((msg, peer) =>
        {
            
        });
    }
    
    private static void RegisterSceneHandlers()
    {
        HybridNetCore.RegisterHandler<SceneVoteStartMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<SceneVoteRequestMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<SceneReadySetMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<SceneBeginLoadMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<SceneCancelMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<SceneReadyMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<SceneGateReadyMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<SceneGateReleaseMessage>((msg, peer) =>
        {
            
        });
    }
    
    private static void RegisterEnvironmentHandlers()
    {
        HybridNetCore.RegisterHandler<EnvHurtRequestMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<EnvHurtEventMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<EnvDeadEventMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<EnvSyncRequestMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<EnvSyncStateMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<DoorRequestSetMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<DoorStateMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<AudioEventMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<DiscoverRequestMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<DiscoverResponseMessage>((msg, peer) =>
        {
            
        });
    }
    
    private static void RegisterAIHandlers()
    {
        HybridNetCore.RegisterHandler<AITransformMessage>((msg, peer) =>
        {
            AITool.ApplyAiTransform(msg.EntityId, msg.Position, msg.Forward);
        });
        
        HybridNetCore.RegisterHandler<AIAnimationMessage>((msg, peer) =>
        {
            var st = new AiAnimState
            {
                speed = msg.Speed,
                dirX = msg.DirX,
                dirY = msg.DirY,
                hand = msg.HandState,
                gunReady = msg.GunReady,
                dashing = msg.Dashing
            };
            AITool.Client_ApplyAiAnim(msg.EntityId, st);
        });
        
        HybridNetCore.RegisterHandler<AIHealthMessage>((msg, peer) =>
        {
            COOPManager.AIHealth.Client_ApplyAiHealth(msg.EntityId, msg.MaxHealth, msg.CurrentHealth);
        });
        
        HybridNetCore.RegisterHandler<AISeedSnapshotMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<AISeedPatchMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<AIFreezeToggleMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<AILoadoutSnapshotMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<AIAttackSwingMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<AIAttackTellMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<AIHealthReportMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<AINameIconMessage>((msg, peer) =>
        {
            
        });
    }
    
    private static void RegisterBroadcastHandlers()
    {
        HybridNetCore.RegisterHandler<SystemAnnouncementMessage>((msg, peer) =>
        {
            UnityEngine.Debug.Log($"[HybridNet] 系统公告 {msg.Title}: {msg.Content}");
        });
        
        HybridNetCore.RegisterHandler<ServerStatusMessage>((msg, peer) =>
        {
            
        });
        
        HybridNetCore.RegisterHandler<ChatMessage>((msg, peer) =>
        {
            UnityEngine.Debug.Log($"[HybridNet] 聊天 {msg.SenderName}: {msg.Content}");
        });
        
        HybridNetCore.RegisterHandler<PlayerKickMessage>((msg, peer) =>
        {
            UnityEngine.Debug.LogWarning($"[HybridNet] 踢出玩家 {msg.PlayerId} 原因: {msg.Reason}");
        });
        
        HybridNetCore.RegisterHandler<ServerShutdownMessage>((msg, peer) =>
        {
            UnityEngine.Debug.LogError($"[HybridNet] 服务器关闭倒计时 {msg.CountdownSeconds}秒 原因: {msg.Reason}");
        });
    }
    
    private static void RegisterVoiceHandlers()
    {
        HybridNetCore.RegisterHandler<VoiceDataMessage>((msg, peer) =>
        {
            var voiceManager = Main.Voice.VoiceManager.Instance;
            if (voiceManager != null)
            {
                voiceManager.ReceiveVoiceData(msg);
            }
        });
        
        HybridNetCore.RegisterHandler<VoiceStateMessage>((msg, peer) =>
        {
            var voiceManager = Main.Voice.VoiceManager.Instance;
            if (voiceManager != null)
            {
                voiceManager.ReceiveVoiceState(msg);
            }
        });
    }
}

