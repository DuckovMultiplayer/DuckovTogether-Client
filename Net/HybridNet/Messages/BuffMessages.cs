namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class PlayerBuffSelfApplyMessage : IHybridMessage
{
    public string MessageType => "player_buff_self_apply";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public int WeaponTypeId { get; set; }
    public int BuffId { get; set; }
}

public class HostBuffProxyApplyMessage : IHybridMessage
{
    public string MessageType => "host_buff_proxy_apply";
    public MessagePriority Priority => MessagePriority.Normal;
    public SerializationMode PreferredMode => SerializationMode.Binary;
    
    public string PlayerId { get; set; }
    public int WeaponTypeId { get; set; }
    public int BuffId { get; set; }
}
