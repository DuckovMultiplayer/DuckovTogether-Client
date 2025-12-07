using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class AISeedSnapshotMessage : IHybridMessage
{
    public string MessageType => "ai_seed_snapshot";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int SceneSeed { get; set; }
    public List<AISeedPair> Seeds { get; set; }
}

public class AISeedPatchMessage : IHybridMessage
{
    public string MessageType => "ai_seed_patch";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int Count { get; set; }
    public int AiIdA { get; set; }
    public int SeedA { get; set; }
    public int AiIdB { get; set; }
    public int SeedB { get; set; }
}

public class AISeedPair
{
    public int RootId { get; set; }
    public int Seed { get; set; }
}

public class AIFreezeToggleMessage : IHybridMessage
{
    public string MessageType => "ai_freeze_toggle";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public bool Freeze { get; set; }
}

public class SceneAISeedRequestMessage : IHybridMessage
{
    public string MessageType => "scene_ai_seed_request";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public string SceneId { get; set; }
}

public class SceneAISeedResponseMessage : IHybridMessage
{
    public string MessageType => "scene_ai_seed_response";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string SceneId { get; set; }
    public int SceneSeed { get; set; }
    public List<AISeedPair> Seeds { get; set; }
}
