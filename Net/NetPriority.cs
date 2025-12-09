















using DuckovNet;

namespace EscapeFromDuckovCoopMod.Net;




public enum PacketPriority : byte
{
    
    
    
    
    
    
    Critical = 0,

    
    
    
    
    
    
    Important = 1,

    
    
    
    
    
    
    Normal = 2,

    
    
    
    
    
    
    Frequent = 3,

    
    
    
    
    
    
    Voice = 4
}




public static class PacketPriorityExtensions
{
    
    
    
    public static DeliveryMethod GetDeliveryMethod(this PacketPriority priority)
    {
        return priority switch
        {
            PacketPriority.Critical => DeliveryMethod.ReliableOrdered,
            PacketPriority.Important => DeliveryMethod.ReliableSequenced,
            PacketPriority.Normal => DeliveryMethod.ReliableUnordered,
            PacketPriority.Frequent => DeliveryMethod.Unreliable,
            PacketPriority.Voice => DeliveryMethod.Sequenced,
            _ => DeliveryMethod.ReliableOrdered  
        };
    }

    
    
    
    public static byte GetChannelNumber(this PacketPriority priority)
    {
        return priority switch
        {
            PacketPriority.Critical => 0,   
            PacketPriority.Important => 1,  
            PacketPriority.Normal => 2,     
            PacketPriority.Frequent => 3,   
            PacketPriority.Voice => 3,      
            _ => 0
        };
    }

    
    
    
    public static string GetDescription(this PacketPriority priority)
    {
        return priority switch
        {
            PacketPriority.Critical => "关键（投票、伤害、拾取）",
            PacketPriority.Important => "重要（血量、装备）",
            PacketPriority.Normal => "普通（NPC、物品生成）",
            PacketPriority.Frequent => "高频（位置、动画）",
            PacketPriority.Voice => "语音（VOIP）",
            _ => "未知"
        };
    }

    
    
    
    public static bool IsReliable(this PacketPriority priority)
    {
        return priority switch
        {
            PacketPriority.Critical => true,
            PacketPriority.Important => true,
            PacketPriority.Normal => true,
            PacketPriority.Frequent => false,
            PacketPriority.Voice => false,
            _ => true
        };
    }
}

