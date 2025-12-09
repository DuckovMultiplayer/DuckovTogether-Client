using DuckovNet;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public static class HybridNetBroadcast
{
    private static ModBehaviourF Service => ModBehaviourF.Instance;
    private static bool IsServer => Service != null && Service.IsServer;
    private static NetManager NetManager => Service?.netManager;
    
    public static void SendToAll<T>(T message) where T : IHybridMessage
    {
        if (!IsServer || NetManager == null) return;
        HybridNetCore.Send(message);
    }
    
    public static void SendToAllExcept<T>(T message, NetPeer excludePeer) where T : IHybridMessage
    {
        if (!IsServer || NetManager == null) return;
        
        foreach (var peer in NetManager.ConnectedPeerList)
        {
            if (peer != excludePeer)
            {
                HybridNetCore.Send(message, peer);
            }
        }
    }
    
    public static void SendReliable<T>(T message) where T : IHybridMessage
    {
        if (IsServer)
        {
            HybridNetCore.Send(message);
        }
        else
        {
            var peer = Service?.connectedPeer;
            if (peer != null)
            {
                HybridNetCore.Send(message, peer);
            }
        }
    }
    
    public static void SendToClient<T>(T message, NetPeer peer) where T : IHybridMessage
    {
        if (!IsServer || peer == null) return;
        HybridNetCore.Send(message, peer);
    }
    
    public static void SendToServer<T>(T message) where T : IHybridMessage
    {
        if (IsServer) return;
        var peer = Service?.connectedPeer;
        if (peer != null)
        {
            HybridNetCore.Send(message, peer);
        }
    }
    
    public static void BroadcastLegacy(NetDataWriter writer, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
    {
        if (!IsServer || NetManager == null) return;
        NetManager.SendToAll(writer, method);
    }
    
    public static void SendLegacy(NetDataWriter writer, NetPeer peer, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
    {
        peer?.Send(writer, method);
    }
}
