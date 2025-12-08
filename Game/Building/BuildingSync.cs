using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Game.Building
{
    public class BuildingSync
    {
        private static BuildingSync _instance;
        public static BuildingSync Instance => _instance ??= new BuildingSync();
        
        private readonly Dictionary<string, BuildingState> _buildings = new();
        private readonly Dictionary<string, GameObject> _buildingObjects = new();
        
        private ModBehaviourF Service => ModBehaviourF.Instance;
        private bool IsConnected => Service?.networkStarted ?? false;
        
        public void Initialize()
        {
            _buildings.Clear();
            _buildingObjects.Clear();
            Debug.Log("[BuildingSync] Initialized");
        }
        
        public void Clear()
        {
            _buildings.Clear();
            _buildingObjects.Clear();
        }
        
        public string GenerateBuildingId()
        {
            return $"bld_{DateTime.UtcNow.Ticks}_{UnityEngine.Random.Range(1000, 9999)}";
        }
        
        public void OnLocalBuildingPlaced(string buildingType, Vector3 position, Quaternion rotation, GameObject buildingObject)
        {
            if (!IsConnected) return;
            
            var buildingId = GenerateBuildingId();
            var state = new BuildingState
            {
                BuildingId = buildingId,
                BuildingType = buildingType,
                OwnerId = GetLocalPlayerId(),
                Position = position,
                Rotation = rotation.eulerAngles,
                PlacedAt = DateTime.UtcNow.Ticks,
                IsDestroyed = false
            };
            
            _buildings[buildingId] = state;
            _buildingObjects[buildingId] = buildingObject;
            
            SendBuildingPlaced(state);
            Debug.Log($"[BuildingSync] Local building placed: {buildingType} at {position}");
        }
        
        public void OnLocalBuildingDestroyed(GameObject buildingObject)
        {
            if (!IsConnected) return;
            
            foreach (var kvp in _buildingObjects)
            {
                if (kvp.Value == buildingObject)
                {
                    var buildingId = kvp.Key;
                    if (_buildings.TryGetValue(buildingId, out var state))
                    {
                        state.IsDestroyed = true;
                        SendBuildingDestroyed(buildingId);
                        Debug.Log($"[BuildingSync] Local building destroyed: {buildingId}");
                    }
                    break;
                }
            }
        }
        
        public void OnLocalBuildingUpgraded(GameObject buildingObject, int newLevel)
        {
            if (!IsConnected) return;
            
            foreach (var kvp in _buildingObjects)
            {
                if (kvp.Value == buildingObject)
                {
                    var buildingId = kvp.Key;
                    if (_buildings.TryGetValue(buildingId, out var state))
                    {
                        state.Level = newLevel;
                        SendBuildingUpgraded(buildingId, newLevel);
                        Debug.Log($"[BuildingSync] Local building upgraded: {buildingId} to level {newLevel}");
                    }
                    break;
                }
            }
        }
        
        private void SendBuildingPlaced(BuildingState state)
        {
            var msg = new BuildingPlacedMessage
            {
                type = "buildingPlaced",
                buildingId = state.BuildingId,
                buildingType = state.BuildingType,
                ownerId = state.OwnerId,
                posX = state.Position.x,
                posY = state.Position.y,
                posZ = state.Position.z,
                rotX = state.Rotation.x,
                rotY = state.Rotation.y,
                rotZ = state.Rotation.z,
                timestamp = state.PlacedAt
            };
            
            SendJsonMessage(msg);
        }
        
        private void SendBuildingDestroyed(string buildingId)
        {
            var msg = new BuildingDestroyedMessage
            {
                type = "buildingDestroyed",
                buildingId = buildingId,
                timestamp = DateTime.UtcNow.Ticks
            };
            
            SendJsonMessage(msg);
        }
        
        private void SendBuildingUpgraded(string buildingId, int newLevel)
        {
            var msg = new BuildingUpgradedMessage
            {
                type = "buildingUpgraded",
                buildingId = buildingId,
                newLevel = newLevel,
                timestamp = DateTime.UtcNow.Ticks
            };
            
            SendJsonMessage(msg);
        }
        
        public void Client_OnBuildingPlaced(string json)
        {
            try
            {
                var msg = JsonUtility.FromJson<BuildingPlacedMessage>(json);
                if (msg == null) return;
                
                if (msg.ownerId == GetLocalPlayerId()) return;
                
                var state = new BuildingState
                {
                    BuildingId = msg.buildingId,
                    BuildingType = msg.buildingType,
                    OwnerId = msg.ownerId,
                    Position = new Vector3(msg.posX, msg.posY, msg.posZ),
                    Rotation = new Vector3(msg.rotX, msg.rotY, msg.rotZ),
                    PlacedAt = msg.timestamp,
                    IsDestroyed = false
                };
                
                _buildings[msg.buildingId] = state;
                SpawnRemoteBuilding(state);
                
                Debug.Log($"[BuildingSync] Remote building placed: {msg.buildingType} by {msg.ownerId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildingSync] Failed to process building placed: {ex.Message}");
            }
        }
        
        public void Client_OnBuildingDestroyed(string json)
        {
            try
            {
                var msg = JsonUtility.FromJson<BuildingDestroyedMessage>(json);
                if (msg == null) return;
                
                if (_buildingObjects.TryGetValue(msg.buildingId, out var obj) && obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                    _buildingObjects.Remove(msg.buildingId);
                }
                
                if (_buildings.TryGetValue(msg.buildingId, out var state))
                {
                    state.IsDestroyed = true;
                }
                
                Debug.Log($"[BuildingSync] Remote building destroyed: {msg.buildingId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildingSync] Failed to process building destroyed: {ex.Message}");
            }
        }
        
        public void Client_OnBuildingUpgraded(string json)
        {
            try
            {
                var msg = JsonUtility.FromJson<BuildingUpgradedMessage>(json);
                if (msg == null) return;
                
                if (_buildings.TryGetValue(msg.buildingId, out var state))
                {
                    state.Level = msg.newLevel;
                }
                
                Debug.Log($"[BuildingSync] Remote building upgraded: {msg.buildingId} to level {msg.newLevel}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildingSync] Failed to process building upgraded: {ex.Message}");
            }
        }
        
        public void Client_OnBuildingFullSync(string json)
        {
            try
            {
                var msg = JsonUtility.FromJson<BuildingFullSyncMessage>(json);
                if (msg == null || msg.buildings == null) return;
                
                Debug.Log($"[BuildingSync] Received full sync: {msg.buildings.Length} buildings");
                
                foreach (var bld in msg.buildings)
                {
                    var state = new BuildingState
                    {
                        BuildingId = bld.buildingId,
                        BuildingType = bld.buildingType,
                        OwnerId = bld.ownerId,
                        Position = new Vector3(bld.posX, bld.posY, bld.posZ),
                        Rotation = new Vector3(bld.rotX, bld.rotY, bld.rotZ),
                        Level = bld.level,
                        PlacedAt = bld.timestamp,
                        IsDestroyed = bld.isDestroyed
                    };
                    
                    _buildings[bld.buildingId] = state;
                    
                    if (!state.IsDestroyed && !_buildingObjects.ContainsKey(bld.buildingId))
                    {
                        SpawnRemoteBuilding(state);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildingSync] Failed to process full sync: {ex.Message}");
            }
        }
        
        private void SpawnRemoteBuilding(BuildingState state)
        {
        }
        
        private string GetLocalPlayerId()
        {
            return Net.CoopNetClient.Instance?.NetworkId ?? "";
        }
        
        private void SendJsonMessage<T>(T msg)
        {
            var json = JsonUtility.ToJson(msg);
            var writer = new LiteNetLib.Utils.NetDataWriter();
            writer.Put((byte)9);
            writer.Put(json);
            ModBehaviourF.Instance?.SendRaw(writer);
        }
        
        public void RequestFullSync()
        {
            if (!IsConnected) return;
            
            var msg = new BuildingSyncRequestMessage
            {
                type = "buildingSyncRequest",
                timestamp = DateTime.UtcNow.Ticks
            };
            
            SendJsonMessage(msg);
            
            Debug.Log("[BuildingSync] Requested full sync");
        }
        
        public IReadOnlyDictionary<string, BuildingState> GetAllBuildings() => _buildings;
    }
    
    public class BuildingState
    {
        public string BuildingId { get; set; } = "";
        public string BuildingType { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public int Level { get; set; } = 1;
        public long PlacedAt { get; set; }
        public bool IsDestroyed { get; set; }
    }
    
    [Serializable]
    public class BuildingPlacedMessage
    {
        public string type;
        public string buildingId;
        public string buildingType;
        public string ownerId;
        public float posX, posY, posZ;
        public float rotX, rotY, rotZ;
        public long timestamp;
    }
    
    [Serializable]
    public class BuildingDestroyedMessage
    {
        public string type;
        public string buildingId;
        public long timestamp;
    }
    
    [Serializable]
    public class BuildingUpgradedMessage
    {
        public string type;
        public string buildingId;
        public int newLevel;
        public long timestamp;
    }
    
    [Serializable]
    public class BuildingFullSyncMessage
    {
        public string type;
        public BuildingSyncData[] buildings;
        public long timestamp;
    }
    
    [Serializable]
    public class BuildingSyncData
    {
        public string buildingId;
        public string buildingType;
        public string ownerId;
        public float posX, posY, posZ;
        public float rotX, rotY, rotZ;
        public int level;
        public long timestamp;
        public bool isDestroyed;
    }
    
    [Serializable]
    public class BuildingSyncRequestMessage
    {
        public string type;
        public long timestamp;
    }
}