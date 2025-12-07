using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class SystemAnnouncementMessage : IHybridMessage
{
    public string MessageType => "system_announcement";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string Title { get; set; }
    public string Content { get; set; }
    public string Severity { get; set; }
}

public class ServerStatusMessage : IHybridMessage
{
    public string MessageType => "server_status";
    public MessagePriority Priority => MessagePriority.Low;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public int PlayerCount { get; set; }
    public float ServerTime { get; set; }
    public string Version { get; set; }
}

public class ChatMessage : IHybridMessage
{
    public string MessageType => "chat";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string SenderId { get; set; }
    public string SenderName { get; set; }
    public string Content { get; set; }
    public long Timestamp { get; set; }
}

public class PlayerKickMessage : IHybridMessage
{
    public string MessageType => "player_kick";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string PlayerId { get; set; }
    public string Reason { get; set; }
}

public class ServerShutdownMessage : IHybridMessage
{
    public string MessageType => "server_shutdown";
    public MessagePriority Priority => MessagePriority.Critical;
    public SerializationMode PreferredMode => SerializationMode.Json;
    
    public string Reason { get; set; }
    public int CountdownSeconds { get; set; }
}
