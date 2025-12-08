















using EscapeFromDuckovCoopMod.Net;

namespace EscapeFromDuckovCoopMod;




public class JsonMessageRouter : ModBehaviourF
{
    public static JsonMessageRouter Instance { get; private set; }
    
    private void Awake()
    {
        Instance = this;
    }
    
    
    
    
    [System.Serializable]
    private class BaseJsonMessage
    {
        public string type;
    }
    
    public void RouteMessage(string jsonData)
    {
        HandleJsonMessageInternal(jsonData, null);
    }

    
    
    
    
    
    
    public static void HandleJsonMessage(NetDataReader reader, NetPeer fromPeer = null)
    {
        if (reader == null)
        {
            Debug.LogWarning("[JsonRouter] reader为空");
            return;
        }

        var json = reader.GetString();
        HandleJsonMessageInternal(json, fromPeer);
    }
    
    public static void HandleJsonMessageInternal(string json, NetPeer fromPeer = null)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[JsonRouter] 收到空JSON消息");
            return;
        }

        try
        {
            
            var baseMsg = JsonUtility.FromJson<BaseJsonMessage>(json);
            if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type))
            {
                Debug.LogWarning($"[JsonRouter] JSON消息缺少type字段: {json}");
                return;
            }

            Debug.Log($"[JsonRouter] 收到JSON消息，type={baseMsg.type}");

            
            switch (baseMsg.type)
            {
                case "setId":
                    HandleSetIdMessage(json);
                    break;

                case "lootFullSync":
                    
                    LootFullSyncMessage.Client_OnLootFullSync(json);
                    break;

                case "sceneVote":
                    
                    SceneVoteMessage.Client_HandleVoteState(json);
                    break;

                case "sceneVoteRequest":
                    
                    SceneVoteMessage.Host_HandleVoteRequest(json);
                    break;

                case "sceneVoteReady":
                    
                    SceneVoteMessage.Host_HandleReadyToggle(json);
                    break;

                case "forceSceneLoad":
                    
                    SceneVoteMessage.Client_HandleForceSceneLoad(json);
                    break;

                case "updateClientStatus":
                    
                    HandleClientStatusMessage(json, fromPeer);
                    break;

                case "kick":
                    
                    KickMessage.Client_HandleKickMessage(json);
                    break;

                case "test":
                    
                    HandleTestMessage(json);
                    break;
                
                case "ai_seed_snapshot":
                    AISeedMessage.Client_HandleSeedSnapshot(json);
                    break;
                
                case "ai_seed_patch":
                    AISeedMessage.Client_HandleSeedPatch(json);
                    break;
                
                case "ai_loadout":
                    AILoadoutMessage.Client_HandleLoadout(json);
                    break;
                
                case "ai_transform_snapshot":
                    AITransformMessage.Client_HandleSnapshot(json);
                    break;
                
                case "ai_anim_snapshot":
                    AIAnimationMessage.Client_HandleSnapshot(json);
                    break;
                
                case "ai_health_sync":
                    AIHealthMessage.Client_HandleHealthSync(json);
                    break;
                
                case "ai_health_report":
                    AIHealthMessage.Host_HandleHealthReport(fromPeer, json);
                    break;
                
                case "ai_name_icon":
                    AINameIconMessage.Client_Handle(json);
                    break;
                
                case "playerJoin":
                    HandlePlayerJoin(json);
                    break;
                
                case "playerDisconnect":
                    HandlePlayerDisconnect(json);
                    break;
                
                case "playerDeath":
                    HandlePlayerDeath(json);
                    break;
                
                case "playerRespawn":
                    HandlePlayerRespawn(json);
                    break;
                
                case "playerHealth":
                    HandlePlayerHealth(json);
                    break;
                
                case "playerList":
                    HandlePlayerList(json);
                    break;
                
                case "player_transform_snapshot":
                    HandlePlayerTransformSnapshot(json);
                    break;
                
                case "player_anim_snapshot":
                    HandlePlayerAnimSnapshot(json);
                    break;
                
                case "player_equipment_snapshot":
                    HandlePlayerEquipmentSnapshot(json);
                    break;
                
                case "deadLootSpawn":
                    HandleDeadLootSpawn(json);
                    break;
                
                case "weaponFire":
                    HandleWeaponFire(json);
                    break;
                
                case "playerDamage":
                case "aiDamage":
                    HandleDamage(json);
                    break;
                
                case "grenadeThrow":
                    HandleGrenadeThrow(json);
                    break;
                
                case "grenadeExplode":
                    HandleGrenadeExplode(json);
                    break;

                default:
                    Debug.LogWarning($"[JsonRouter] 未知的消息类型: {baseMsg.type}");
                    break;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[JsonRouter] 处理JSON消息失败: {ex.Message}\nJSON: {json}");
        }
    }

    
    
    
    private static void HandleSetIdMessage(string json)
    {
        var service = ModBehaviourF.Instance;
        if (service == null)
        {
            Debug.LogWarning("[JsonRouter] NetService未初始化");
            return;
        }

        if (service.IsServer)
        {
            Debug.LogWarning("[JsonRouter] 主机不应该接收SetId消息");
            return;
        }

        try
        {
            var data = JsonUtility.FromJson<SetIdMessage.SetIdData>(json);
            if (data == null)
            {
                Debug.LogError("[JsonRouter] SetId消息解析失败");
                return;
            }

            var oldId = service.localPlayerStatus?.EndPoint;
            var newId = data.networkId;

            Debug.Log($"[SetId] 收到主机告知的网络ID: {newId}");
            Debug.Log($"[SetId] 旧ID: {oldId}");

            
            if (service.localPlayerStatus != null)
            {
                service.localPlayerStatus.EndPoint = newId;
                Debug.Log($"[SetId] ✓ 已更新 localPlayerStatus.EndPoint: {oldId} → {newId}");
            }
            else
            {
                Debug.LogWarning("[SetId] localPlayerStatus为空，无法更新");
            }

            
            CleanupSelfDuplicate(oldId, newId);

            
            
            ClientStatusMessage.Client_SendStatusUpdate();
            Debug.Log("[SetId] ✓ 已发送客户端状态更新（包含 SteamID）");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[JsonRouter] 处理SetId消息失败: {ex.Message}");
        }
    }

    
    
    
    private static void CleanupSelfDuplicate(string oldId, string newId)
    {
        var service = ModBehaviourF.Instance;
        if (service == null || service.clientRemoteCharacters == null)
            return;

        var toRemove = new System.Collections.Generic.List<string>();

        foreach (var kv in service.clientRemoteCharacters)
        {
            var playerId = kv.Key;
            var go = kv.Value;

            
            if (playerId == oldId || playerId == newId)
            {
                Debug.LogWarning($"[SetId] 发现自己的远程副本，准备删除: {playerId}");
                toRemove.Add(playerId);
                if (go != null)
                {
                    UnityEngine.Object.Destroy(go);
                    Debug.Log($"[SetId] ✓ 已删除远程副本GameObject: {playerId}");
                }
            }
        }

        foreach (var id in toRemove)
        {
            service.clientRemoteCharacters.Remove(id);
            Debug.Log($"[SetId] ✓ 已从clientRemoteCharacters移除: {id}");
        }

        if (toRemove.Count > 0)
        {
            Debug.Log($"[SetId] ✓ 清理完成，共删除 {toRemove.Count} 个自己的远程副本");
        }
    }

    
    
    
    private static void HandleClientStatusMessage(string json, NetPeer fromPeer)
    {
        var service = ModBehaviourF.Instance;
        if (service == null || !service.IsServer)
        {
            Debug.LogWarning("[JsonRouter] 只有主机可以接收客户端状态消息");
            return;
        }

        if (fromPeer == null)
        {
            Debug.LogWarning("[JsonRouter] fromPeer为空，无法处理客户端状态消息");
            return;
        }

        try
        {
            ClientStatusMessage.Host_HandleClientStatus(fromPeer, json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[JsonRouter] 处理客户端状态消息失败: {ex.Message}");
        }
    }

    
    
    
    private static void HandleTestMessage(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<JsonMessage.TestJsonData>(json);
            Debug.Log($"[JsonRouter] 测试消息: {data.message} (时间: {data.timestamp}, 随机值: {data.randomValue})");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[JsonRouter] 处理测试消息失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class PlayerJoinData
    {
        public string type;
        public int peerId;
        public string playerName;
        public Vec3 position;
        public Vec3 rotation;
        public string customFaceJson;
        public string timestamp;
    }
    
    [System.Serializable]
    private class Vec3 { public float x, y, z; }
    
    private static void HandlePlayerJoin(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<PlayerJoinData>(json);
            if (data == null) return;
            
            Debug.Log($"[PlayerSync] 玩家加入: {data.playerName} (ID: {data.peerId})");
            
            var pos = new Vector3(data.position.x, data.position.y, data.position.z);
            var rot = Quaternion.Euler(data.rotation.x, data.rotation.y, data.rotation.z);
            
            CreateRemoteCharacter.CreateRemoteCharacterForClient(
                data.peerId.ToString(), pos, rot, data.customFaceJson ?? ""
            ).Forget();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerSync] 处理玩家加入失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class PlayerDisconnectData
    {
        public string type;
        public int peerId;
        public string timestamp;
    }
    
    private static void HandlePlayerDisconnect(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<PlayerDisconnectData>(json);
            if (data == null) return;
            
            Debug.Log($"[PlayerSync] 玩家断开: ID {data.peerId}");
            
            var service = ModBehaviourF.Instance;
            if (service?.clientRemoteCharacters != null)
            {
                var playerId = data.peerId.ToString();
                if (service.clientRemoteCharacters.TryGetValue(playerId, out var go) && go != null)
                {
                    UnityEngine.Object.Destroy(go);
                }
                service.clientRemoteCharacters.Remove(playerId);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerSync] 处理玩家断开失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class PlayerDeathData
    {
        public string type;
        public int peerId;
        public int killerId;
        public string cause;
        public string timestamp;
    }
    
    private static void HandlePlayerDeath(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<PlayerDeathData>(json);
            if (data == null) return;
            
            Debug.Log($"[PlayerSync] 玩家死亡: ID {data.peerId}, 击杀者: {data.killerId}");
            
            var service = ModBehaviourF.Instance;
            var playerId = data.peerId.ToString();
            if (service?.clientRemoteCharacters?.TryGetValue(playerId, out var go) == true && go != null)
            {
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerSync] 处理玩家死亡失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class PlayerRespawnData
    {
        public string type;
        public int peerId;
        public Vec3 position;
        public string timestamp;
    }
    
    private static void HandlePlayerRespawn(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<PlayerRespawnData>(json);
            if (data == null) return;
            
            Debug.Log($"[PlayerSync] 玩家重生: ID {data.peerId}");
            
            var service = ModBehaviourF.Instance;
            var playerId = data.peerId.ToString();
            if (service?.clientRemoteCharacters?.TryGetValue(playerId, out var go) == true && go != null)
            {
                go.transform.position = new Vector3(data.position.x, data.position.y, data.position.z);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerSync] 处理玩家重生失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class PlayerHealthData
    {
        public string type;
        public int peerId;
        public float currentHealth;
        public float maxHealth;
    }
    
    private static void HandlePlayerHealth(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<PlayerHealthData>(json);
            if (data == null) return;
            
            var service = ModBehaviourF.Instance;
            var playerId = data.peerId.ToString();
            if (service?.clientRemoteCharacters?.TryGetValue(playerId, out var go) == true && go != null)
            {
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerSync] 处理玩家生命值失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class PlayerListData
    {
        public string type;
        public PlayerListEntry[] players;
    }
    
    [System.Serializable]
    private class PlayerListEntry
    {
        public int peerId;
        public string endPoint;
        public string playerName;
        public bool isInGame;
        public string sceneId;
        public int latency;
    }
    
    private static void HandlePlayerList(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<PlayerListData>(json);
            if (data?.players == null) return;
            
            var service = ModBehaviourF.Instance;
            if (service == null) return;
            
            foreach (var p in data.players)
            {
                if (service.IsSelfId(p.peerId.ToString())) continue;
                
                if (!service.clientPlayerStatuses.TryGetValue(p.peerId.ToString(), out var status))
                {
                    status = new PlayerStatus();
                    service.clientPlayerStatuses[p.peerId.ToString()] = status;
                }
                
                status.EndPoint = p.endPoint;
                status.PlayerName = p.playerName;
                status.IsInGame = p.isInGame;
                status.SceneId = p.sceneId;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerSync] 处理玩家列表失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class PlayerTransformSnapshotData
    {
        public string type;
        public PlayerTransformEntry[] transforms;
    }
    
    [System.Serializable]
    private class PlayerTransformEntry
    {
        public int peerId;
        public Vec3 position;
        public Vec3 rotation;
        public Vec3 velocity;
    }
    
    private static void HandlePlayerTransformSnapshot(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<PlayerTransformSnapshotData>(json);
            if (data?.transforms == null) return;
            
            var service = ModBehaviourF.Instance;
            if (service == null) return;
            
            foreach (var t in data.transforms)
            {
                if (service.IsSelfId(t.peerId.ToString())) continue;
                
                var playerId = t.peerId.ToString();
                if (service.clientRemoteCharacters?.TryGetValue(playerId, out var go) == true && go != null)
                {
                    var interp = NetInterpUtil.Attach(go);
                    if (interp != null)
                    {
                        var pos = new Vector3(t.position.x, t.position.y, t.position.z);
                        var rot = Quaternion.Euler(t.rotation.x, t.rotation.y, t.rotation.z);
                        interp.Push(pos, rot);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerSync] 处理玩家位置快照失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class PlayerAnimSnapshotData
    {
        public string type;
        public PlayerAnimEntry[] anims;
    }
    
    [System.Serializable]
    private class PlayerAnimEntry
    {
        public int peerId;
        public float speed;
        public float dirX;
        public float dirY;
        public int hand;
        public bool gunReady;
        public bool dashing;
        public bool reloading;
    }
    
    private static void HandlePlayerAnimSnapshot(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<PlayerAnimSnapshotData>(json);
            if (data?.anims == null) return;
            
            var service = ModBehaviourF.Instance;
            if (service == null) return;
            
            foreach (var a in data.anims)
            {
                if (service.IsSelfId(a.peerId.ToString())) continue;
                
                var playerId = a.peerId.ToString();
                if (service.clientRemoteCharacters?.TryGetValue(playerId, out var go) == true && go != null)
                {
                    var animInterp = AnimInterpUtil.Attach(go);
                    if (animInterp != null)
                    {
                        animInterp.Push(new AnimSample { speed = a.speed, dirX = a.dirX, dirY = a.dirY, hand = a.hand, gunReady = a.gunReady, dashing = a.dashing });
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerSync] 处理玩家动画快照失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class PlayerEquipmentSnapshotData
    {
        public string type;
        public PlayerEquipmentEntry[] equipment;
    }
    
    [System.Serializable]
    private class PlayerEquipmentEntry
    {
        public int peerId;
        public int weaponId;
        public int armorId;
        public int helmetId;
        public int[] hotbar;
    }
    
    private static void HandlePlayerEquipmentSnapshot(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<PlayerEquipmentSnapshotData>(json);
            if (data?.equipment == null) return;
            
            var service = ModBehaviourF.Instance;
            if (service == null) return;
            
            foreach (var e in data.equipment)
            {
                if (service.IsSelfId(e.peerId.ToString())) continue;
                
                var playerId = e.peerId.ToString();
                if (service.clientRemoteCharacters?.TryGetValue(playerId, out var go) == true && go != null)
                {
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerSync] 处理玩家装备快照失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class DeadLootSpawnData
    {
        public string type;
        public int aiId;
        public int lootUid;
        public Vec3 position;
        public Vec3 rotation;
        public string aiType;
        public string playerName;
        public LootItemDataJson[] items;
        public string timestamp;
    }
    
    [System.Serializable]
    private class LootItemDataJson
    {
        public int slot;
        public string itemId;
        public int count;
    }
    
    private static void HandleDeadLootSpawn(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<DeadLootSpawnData>(json);
            if (data == null) return;
            
            Debug.Log($"[DeathSync] 收到死亡掉落箱: aiId={data.aiId}, lootUid={data.lootUid}, pos={data.position.x},{data.position.y},{data.position.z}");
            
            var pos = new Vector3(data.position.x, data.position.y, data.position.z);
            var rot = Quaternion.Euler(data.rotation.x, data.rotation.y, data.rotation.z);
            
            if (DeadLootBox.Instance != null)
            {
                DeadLootBox.Instance.SpawnDeadLootboxAt(data.aiId, data.lootUid, pos, rot);
            }
            else
            {
                Debug.LogWarning("[DeathSync] DeadLootBox.Instance 为空，无法生成掉落箱");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DeathSync] 处理死亡掉落箱失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class WeaponFireData
    {
        public string type;
        public int shooterId;
        public int weaponId;
        public Vec3 origin;
        public Vec3 direction;
        public int ammoType;
        public long timestamp;
    }
    
    private static void HandleWeaponFire(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<WeaponFireData>(json);
            if (data == null) return;
            
            var service = ModBehaviourF.Instance;
            if (service == null || service.IsSelfId(data.shooterId.ToString())) return;
            
            var playerId = data.shooterId.ToString();
            if (service.clientRemoteCharacters?.TryGetValue(playerId, out var go) == true && go != null)
            {
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CombatSync] 处理武器开火失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class DamageData
    {
        public string type;
        public int targetId;
        public int attackerId;
        public float damage;
        public string damageType;
        public Vec3 hitPoint;
        public long timestamp;
    }
    
    private static void HandleDamage(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<DamageData>(json);
            if (data == null) return;
            
            var hitPoint = new Vector3(data.hitPoint.x, data.hitPoint.y, data.hitPoint.z);
            
            if (data.type == "playerDamage")
            {
                var service = ModBehaviourF.Instance;
                var playerId = data.targetId.ToString();
                
                if (service?.clientRemoteCharacters?.TryGetValue(playerId, out var go) == true && go != null)
                {
                }
            }
            else if (data.type == "aiDamage")
            {
                if (AITool.aiById.TryGetValue(data.targetId, out var ai) && ai != null)
                {
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CombatSync] 处理伤害失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class GrenadeThrowData
    {
        public string type;
        public int throwerId;
        public int grenadeType;
        public Vec3 origin;
        public Vec3 velocity;
        public long timestamp;
    }
    
    private static void HandleGrenadeThrow(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<GrenadeThrowData>(json);
            if (data == null) return;
            
            var service = ModBehaviourF.Instance;
            if (service == null || service.IsSelfId(data.throwerId.ToString())) return;
            
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CombatSync] 处理手雷投掷失败: {ex.Message}");
        }
    }
    
    [System.Serializable]
    private class GrenadeExplodeData
    {
        public string type;
        public int grenadeId;
        public Vec3 position;
        public float radius;
        public float damage;
        public long timestamp;
    }
    
    private static void HandleGrenadeExplode(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<GrenadeExplodeData>(json);
            if (data == null) return;
            
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CombatSync] 处理手雷爆炸失败: {ex.Message}");
        }
    }
}
