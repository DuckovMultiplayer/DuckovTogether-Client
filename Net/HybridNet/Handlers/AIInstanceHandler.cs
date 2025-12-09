using DuckovNet;
using EscapeFromDuckovCoopMod.Main.AI;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet.Handlers
{
    public static class AIInstanceHandler
    {
        public static void RegisterHandlers()
        {
            HybridNetCore.RegisterHandler<AIInstanceSpawnMessage>(OnAIInstanceSpawn);
            HybridNetCore.RegisterHandler<AIInstanceDestroyMessage>(OnAIInstanceDestroy);
            HybridNetCore.RegisterHandler<AIInstanceStateMessage>(OnAIInstanceState);
        }

        private static void OnAIInstanceSpawn(AIInstanceSpawnMessage msg, NetPeer peer)
        {
            if (AIInstanceSync.Instance == null)
                return;

            var data = new AIInstanceData
            {
                InstanceId = msg.InstanceId,
                SceneId = msg.SceneId,
                Position = new Vector3(msg.PosX, msg.PosY, msg.PosZ),
                Rotation = new Quaternion(msg.RotX, msg.RotY, msg.RotZ, msg.RotW),
                PrefabPath = msg.PrefabPath,
                SpawnTime = Time.time
            };

            AIInstanceSync.Instance.Client_ReceiveAISpawn(data);
        }

        private static void OnAIInstanceDestroy(AIInstanceDestroyMessage msg, NetPeer peer)
        {
            if (AIInstanceSync.Instance == null)
                return;

            AIInstanceSync.Instance.Client_ReceiveAIDestroy(msg.InstanceId);
        }

        private static void OnAIInstanceState(AIInstanceStateMessage msg, NetPeer peer)
        {
            if (ModBehaviourF.Instance.IsServer)
                return;

            var instance = GameObject.Find($"AI_{msg.InstanceId}");
            if (instance != null)
            {
                instance.transform.position = new Vector3(msg.PosX, msg.PosY, msg.PosZ);
            }
        }
    }
}
