using LiteNetLib;
using Newtonsoft.Json;

namespace EscapeFromDuckovCoopMod;

public static class JsonMessage
{
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        MaxDepth = 5
    };
    public static void Client_SendJsonToHost(string jsonData)
    {
        SendToHost(jsonData, DeliveryMethod.ReliableOrdered);
    }
    
    public static void SendToHost(object data, DeliveryMethod deliveryMethod)
    {
        var jsonData = data is string str ? str : JsonConvert.SerializeObject(data, Settings);
        var service = ModBehaviourF.Instance;
        if (service == null || service.connectedPeer == null || service.IsServer)
            return;
            
        var writer = service.writer;
        writer.Reset();
        writer.Put((byte)9);
        writer.Put(jsonData);
        service.connectedPeer.Send(writer, deliveryMethod);
    }
    
    public static void SendToPeer(NetPeer peer, object data, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
    {
        var jsonData = data is string str ? str : JsonConvert.SerializeObject(data, Settings);
        var service = ModBehaviourF.Instance;
        if (service == null || peer == null)
            return;
            
        var writer = service.writer;
        writer.Reset();
        writer.Put((byte)9);
        writer.Put(jsonData);
        peer.Send(writer, deliveryMethod);
    }
    
    public static void BroadcastToAllClients(object data, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
    {
        var jsonData = data is string str ? str : JsonConvert.SerializeObject(data, Settings);
        if (!DedicatedServerMode.ShouldBroadcastState())
            return;
        var service = ModBehaviourF.Instance;
            
        var writer = service.writer;
        writer.Reset();
        writer.Put((byte)9);
        writer.Put(jsonData);
        
        foreach (var peer in service.playerStatuses.Keys)
        {
            peer.Send(writer, deliveryMethod);
        }
    }
    
    public static void HandleReceivedJson(string jsonData)
    {
        JsonMessageRouter.Instance?.RouteMessage(jsonData);
    }
    
    public static T HandleReceivedJson<T>(string jsonData) where T : class
    {
        return JsonConvert.DeserializeObject<T>(jsonData);
    }
    
    public class TestJsonData
    {
        public string type { get; set; }
        public string message { get; set; }
        public string timestamp { get; set; }
        public int randomValue { get; set; }
    }
}
