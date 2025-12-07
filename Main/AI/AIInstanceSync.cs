using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Main.AI
{
    public class AIInstanceSync : MonoBehaviour
    {
        public static AIInstanceSync Instance { get; private set; }

        private Dictionary<string, AIInstanceData> _serverInstances = new Dictionary<string, AIInstanceData>();
        private Dictionary<string, GameObject> _clientInstances = new Dictionary<string, GameObject>();
        private Queue<AIInstanceData> _pendingSpawns = new Queue<AIInstanceData>();
        private int _maxSpawnsPerFrame = 3;
        private float _lastSpawnTime = 0f;
        private const float SPAWN_INTERVAL = 0.05f;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (NetService.Instance == null || NetService.Instance.IsServer)
                return;

            if (_pendingSpawns.Count > 0 && Time.time - _lastSpawnTime > SPAWN_INTERVAL)
            {
                int spawned = 0;
                while (_pendingSpawns.Count > 0 && spawned < _maxSpawnsPerFrame)
                {
                    var data = _pendingSpawns.Dequeue();
                    SpawnClientAI(data);
                    spawned++;
                }
                _lastSpawnTime = Time.time;
            }
        }

        public void Server_RegisterAIInstance(GameObject aiObject, string sceneId)
        {
            if (!NetService.Instance.IsServer)
                return;

            string instanceId = GenerateInstanceId();
            
            var data = new AIInstanceData
            {
                InstanceId = instanceId,
                SceneId = sceneId,
                Position = aiObject.transform.position,
                Rotation = aiObject.transform.rotation,
                PrefabPath = GetPrefabPath(aiObject),
                SpawnTime = Time.time
            };

            _serverInstances[instanceId] = data;
            aiObject.name = $"AI_{instanceId}";

            BroadcastAISpawn(data);
        }

        public void Client_ReceiveAISpawn(AIInstanceData data)
        {
            if (NetService.Instance.IsServer)
                return;

            _pendingSpawns.Enqueue(data);
        }

        private void SpawnClientAI(AIInstanceData data)
        {
            if (_clientInstances.ContainsKey(data.InstanceId))
                return;

            var prefab = LoadAIPrefab(data.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Failed to load AI prefab: {data.PrefabPath}");
                return;
            }

            var instance = Instantiate(prefab, data.Position, data.Rotation);
            instance.name = $"AI_{data.InstanceId}";
            _clientInstances[data.InstanceId] = instance;

            var syncComponent = instance.AddComponent<AIInstanceSyncComponent>();
            syncComponent.InstanceId = data.InstanceId;
            syncComponent.SceneId = data.SceneId;
        }

        public void Server_UnregisterAIInstance(string instanceId)
        {
            if (!NetService.Instance.IsServer)
                return;

            if (_serverInstances.Remove(instanceId))
            {
                BroadcastAIDestroy(instanceId);
            }
        }

        public void Client_ReceiveAIDestroy(string instanceId)
        {
            if (NetService.Instance.IsServer)
                return;

            if (_clientInstances.TryGetValue(instanceId, out var instance))
            {
                Destroy(instance);
                _clientInstances.Remove(instanceId);
            }
        }

        private void BroadcastAISpawn(AIInstanceData data)
        {
            var msg = new Net.HybridNet.AIInstanceSpawnMessage
            {
                InstanceId = data.InstanceId,
                SceneId = data.SceneId,
                PosX = data.Position.x,
                PosY = data.Position.y,
                PosZ = data.Position.z,
                RotX = data.Rotation.x,
                RotY = data.Rotation.y,
                RotZ = data.Rotation.z,
                RotW = data.Rotation.w,
                PrefabPath = data.PrefabPath
            };

            var service = NetService.Instance;
            if (service?.playerStatuses != null)
            {
                foreach (var kv in service.playerStatuses)
                {
                    Net.HybridNet.HybridNetCore.Send(msg, kv.Key);
                }
            }
        }

        private void BroadcastAIDestroy(string instanceId)
        {
            var msg = new Net.HybridNet.AIInstanceDestroyMessage
            {
                InstanceId = instanceId
            };

            var service = NetService.Instance;
            if (service?.playerStatuses != null)
            {
                foreach (var kv in service.playerStatuses)
                {
                    Net.HybridNet.HybridNetCore.Send(msg, kv.Key);
                }
            }
        }

        private string GenerateInstanceId()
        {
            return $"{NetService.Instance.GetPlayerId(null)}_{Time.frameCount}_{UnityEngine.Random.Range(1000, 9999)}";
        }

        private string GetPrefabPath(GameObject obj)
        {
            return obj.name;
        }

        private GameObject LoadAIPrefab(string prefabPath)
        {
            return null;
        }

        public void ClearAll()
        {
            _serverInstances.Clear();
            _clientInstances.Clear();
            _pendingSpawns.Clear();
        }
    }

    public class AIInstanceData
    {
        public string InstanceId;
        public string SceneId;
        public Vector3 Position;
        public Quaternion Rotation;
        public string PrefabPath;
        public float SpawnTime;
    }

    public class AIInstanceSyncComponent : MonoBehaviour
    {
        public string InstanceId;
        public string SceneId;
        private float _lastSyncTime;
        private const float SYNC_INTERVAL = 0.1f;

        private void Update()
        {
            if (NetService.Instance.IsServer && Time.time - _lastSyncTime > SYNC_INTERVAL)
            {
                SyncState();
                _lastSyncTime = Time.time;
            }
        }

        private void SyncState()
        {
            var msg = new Net.HybridNet.AIInstanceStateMessage
            {
                InstanceId = InstanceId,
                PosX = transform.position.x,
                PosY = transform.position.y,
                PosZ = transform.position.z
            };

            var service = NetService.Instance;
            if (service?.playerStatuses != null)
            {
                foreach (var kv in service.playerStatuses)
                {
                    Net.HybridNet.HybridNetCore.Send(msg, kv.Key);
                }
            }
        }
    }
}
