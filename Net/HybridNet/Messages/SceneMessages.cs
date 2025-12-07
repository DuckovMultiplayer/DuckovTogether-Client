namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class SceneVoteStartMessage : IHybridMessage
{
    public string MessageType => "scene_vote_start";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string TargetSceneId { get; set; }
    public string CurtainGuid { get; set; }
    public float Duration { get; set; }
}

public class SceneVoteRequestMessage : IHybridMessage
{
    public string MessageType => "scene_vote_request";
    public MessagePriority Priority => MessagePriority.High;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
    public string TargetSceneId { get; set; }
}

public class SceneReadySetMessage : IHybridMessage
{
    public string MessageType => "scene_ready_set";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public bool IsReady { get; set; }
}

public class SceneBeginLoadMessage : IHybridMessage
{
    public string MessageType => "scene_begin_load";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string TargetSceneId { get; set; }
}

public class SceneCancelMessage : IHybridMessage
{
    public string MessageType => "scene_cancel";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string Reason { get; set; }
}

public class SceneReadyMessage : IHybridMessage
{
    public string MessageType => "scene_ready";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public int SceneId { get; set; }
}

public class SceneGateReadyMessage : IHybridMessage
{
    public string MessageType => "scene_gate_ready";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public string SceneId { get; set; }
}

public class SceneGateReleaseMessage : IHybridMessage
{
    public string MessageType => "scene_gate_release";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string SceneId { get; set; }
}
