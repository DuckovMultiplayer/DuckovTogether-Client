using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class VoiceDataMessage : IHybridMessage
{
    public string MessageType => "voice_data";
    public MessagePriority Priority => MessagePriority.Low;
    public SerializationMode PreferredMode => SerializationMode.Binary;

    public string SenderEndPoint { get; set; }
    public int SequenceNumber { get; set; }
    public byte[] AudioData { get; set; }
    public Vector3 Position { get; set; }
}

public class VoiceStateMessage : IHybridMessage
{
    public string MessageType => "voice_state";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Json;

    public string SenderEndPoint { get; set; }
    public bool IsSpeaking { get; set; }
    public bool IsMuted { get; set; }
}
