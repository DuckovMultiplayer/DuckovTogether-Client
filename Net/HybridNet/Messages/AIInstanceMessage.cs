using DuckovNet;

namespace EscapeFromDuckovCoopMod.Net.HybridNet
{
    public class AIInstanceSpawnMessage : IHybridMessage, INetSerializable
    {
        public string MessageType => "AIInstanceSpawn";
        public MessagePriority Priority => MessagePriority.High;
        public SerializationMode PreferredMode => SerializationMode.Binary;
        
        public string InstanceId;
        public string SceneId;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float RotX;
        public float RotY;
        public float RotZ;
        public float RotW;
        public string PrefabPath;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(InstanceId);
            writer.Put(SceneId);
            writer.Put(PosX);
            writer.Put(PosY);
            writer.Put(PosZ);
            writer.Put(RotX);
            writer.Put(RotY);
            writer.Put(RotZ);
            writer.Put(RotW);
            writer.Put(PrefabPath);
        }

        public void Deserialize(NetDataReader reader)
        {
            InstanceId = reader.GetString();
            SceneId = reader.GetString();
            PosX = reader.GetFloat();
            PosY = reader.GetFloat();
            PosZ = reader.GetFloat();
            RotX = reader.GetFloat();
            RotY = reader.GetFloat();
            RotZ = reader.GetFloat();
            RotW = reader.GetFloat();
            PrefabPath = reader.GetString();
        }
    }

    public class AIInstanceDestroyMessage : IHybridMessage, INetSerializable
    {
        public string MessageType => "AIInstanceDestroy";
        public MessagePriority Priority => MessagePriority.High;
        public SerializationMode PreferredMode => SerializationMode.Binary;
        
        public string InstanceId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(InstanceId);
        }

        public void Deserialize(NetDataReader reader)
        {
            InstanceId = reader.GetString();
        }
    }

    public class AIInstanceStateMessage : IHybridMessage, INetSerializable
    {
        public string MessageType => "AIInstanceState";
        public MessagePriority Priority => MessagePriority.Low;
        public SerializationMode PreferredMode => SerializationMode.Binary;
        
        public string InstanceId;
        public float PosX;
        public float PosY;
        public float PosZ;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(InstanceId);
            writer.Put(PosX);
            writer.Put(PosY);
            writer.Put(PosZ);
        }

        public void Deserialize(NetDataReader reader)
        {
            InstanceId = reader.GetString();
            PosX = reader.GetFloat();
            PosY = reader.GetFloat();
            PosZ = reader.GetFloat();
        }
    }
}
