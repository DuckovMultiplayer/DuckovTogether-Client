using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;





public static class HybridNetSender
{
    
    
    public static void SendPlayerPosition(string playerId, Vector3 position, Vector3 direction, NetPeer target = null)
    {
        var msg = new PlayerPositionMessage
        {
            PlayerId = playerId,
            PosX = position.x,
            PosY = position.y,
            PosZ = position.z,
            DirX = direction.x,
            DirY = direction.y,
            DirZ = direction.z
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendPlayerAnimation(string playerId, float moveSpeed, Vector2 moveDir, bool dashing, bool attacking, int handState, bool gunReady, int stateHash, float normTime, NetPeer target = null)
    {
        var msg = new PlayerAnimationMessage
        {
            PlayerId = playerId,
            MoveSpeed = moveSpeed,
            MoveDirX = moveDir.x,
            MoveDirY = moveDir.y,
            IsDashing = dashing,
            IsAttacking = attacking,
            HandState = handState,
            GunReady = gunReady,
            StateHash = stateHash,
            NormTime = normTime
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendPlayerHealthReport(string playerId, float maxHealth, float currentHealth, NetPeer target = null)
    {
        var msg = new PlayerHealthReportMessage
        {
            PlayerId = playerId,
            MaxHealth = maxHealth,
            CurrentHealth = currentHealth
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendPlayerHealthAuthSelf(float maxHealth, float currentHealth, NetPeer target = null)
    {
        var msg = new PlayerHealthAuthSelfMessage
        {
            MaxHealth = maxHealth,
            CurrentHealth = currentHealth
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendPlayerHealthAuthRemote(string playerId, float maxHealth, float currentHealth, NetPeer target = null)
    {
        var msg = new PlayerHealthAuthRemoteMessage
        {
            PlayerId = playerId,
            MaxHealth = maxHealth,
            CurrentHealth = currentHealth
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendPlayerAppearance(string playerId, string faceJson, NetPeer target = null)
    {
        var msg = new PlayerAppearanceMessage
        {
            PlayerId = playerId,
            FaceJson = faceJson
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendPlayerHurtEvent(float damage, Vector3 hitPoint, Vector3 hitNormal, NetPeer target = null)
    {
        var msg = new PlayerHurtEventMessage
        {
            Damage = damage,
            HitPoint = hitPoint,
            HitNormal = hitNormal
        };
        HybridNetCore.Send(msg, target);
    }
    
    
    
    public static void SendSceneVoteStart(string targetSceneId, string curtainGuid, float duration, NetPeer target = null)
    {
        var msg = new SceneVoteStartMessage
        {
            TargetSceneId = targetSceneId,
            CurtainGuid = curtainGuid,
            Duration = duration
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendSceneVoteRequest(string playerId, string targetSceneId, NetPeer target = null)
    {
        var msg = new SceneVoteRequestMessage
        {
            PlayerId = playerId,
            TargetSceneId = targetSceneId
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendSceneReadySet(string playerId, bool isReady, NetPeer target = null)
    {
        var msg = new SceneReadySetMessage
        {
            PlayerId = playerId,
            IsReady = isReady
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendSceneBeginLoad(string targetSceneId, NetPeer target = null)
    {
        var msg = new SceneBeginLoadMessage
        {
            TargetSceneId = targetSceneId
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendSceneCancel(string reason, NetPeer target = null)
    {
        var msg = new SceneCancelMessage
        {
            Reason = reason
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendSceneReady(string playerId, NetPeer target = null)
    {
        var msg = new SceneReadyMessage
        {
            PlayerId = playerId
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendSceneGateReady(string playerId, string sceneId, NetPeer target = null)
    {
        var msg = new SceneGateReadyMessage
        {
            PlayerId = playerId,
            SceneId = sceneId
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendSceneGateRelease(string sceneId, NetPeer target = null)
    {
        var msg = new SceneGateReleaseMessage
        {
            SceneId = sceneId
        };
        HybridNetCore.Send(msg, target);
    }
    
    
    
    public static void SendLootRequestOpen(int lootUid, int scene, Vector3 positionHint, NetPeer target = null)
    {
        var msg = new LootRequestOpenMessage
        {
            LootUid = lootUid,
            Scene = scene,
            PositionHint = positionHint
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendLootState(int lootUid, string containerSnapshot, NetPeer target = null)
    {
        var msg = new LootStateMessage
        {
            LootUid = lootUid,
            ContainerSnapshot = containerSnapshot
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendLootPutOk(int token, int slotIndex, NetPeer target = null)
    {
        var msg = new LootPutOkMessage
        {
            Token = token,
            SlotIndex = slotIndex
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendLootTakeOk(int token, string itemSnapshot, NetPeer target = null)
    {
        var msg = new LootTakeOkMessage
        {
            Token = token,
            ItemSnapshot = itemSnapshot
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendLootDeny(int token, string reason, NetPeer target = null)
    {
        var msg = new LootDenyMessage
        {
            Token = token,
            Reason = reason
        };
        HybridNetCore.Send(msg, target);
    }
    
    
    
    public static void SendDoorState(int doorKey, bool isClosed, NetPeer target = null)
    {
        var msg = new DoorStateMessage
        {
            DoorKey = doorKey,
            IsClosed = isClosed
        };
        HybridNetCore.Send(msg, target);
    }
    
    public static void SendAudioEvent(string eventType, Vector3 position, string data, NetPeer target = null)
    {
        var msg = new AudioEventMessage
        {
            EventType = eventType,
            Position = position,
            Data = data
        };
        HybridNetCore.Send(msg, target);
    }
    
    
    
    
    
    
    public static void BroadcastToAllClients<T>(T message) where T : IHybridMessage
    {
        if (!ModBehaviourF.Instance.IsServer)
        {
            Debug.LogWarning("[HybridNetSender] BroadcastToAllClients只能在服务端调用");
            return;
        }
        
        HybridNetCore.Send(message);
    }
    
    
    
    
    public static void SendToServer<T>(T message) where T : IHybridMessage
    {
        if (ModBehaviourF.Instance.IsServer)
        {
            Debug.LogWarning("[HybridNetSender] SendToServer只能在客户端调用");
            return;
        }
        
        var service = ModBehaviourF.Instance;
        if (service != null && service.netManager.ConnectedPeersCount > 0)
        {
            var serverPeer = service.netManager.ConnectedPeerList[0];
            HybridNetCore.Send(message, serverPeer);
        }
    }
}
