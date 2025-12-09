















using DuckovNet;
using EscapeFromDuckovCoopMod.Net;

namespace EscapeFromDuckovCoopMod;

public static class AudioEventMessage
{
    public static void ClientSend(in CoopAudioEventPayload payload)
    {
        var service = ModBehaviourF.Instance;
        if (service == null || service.IsServer || !service.networkStarted)
            return;

        var peer = service.connectedPeer;
        if (peer == null)
            return;

        
        
        
        
        
        
        
        
    }

    public static void ServerBroadcast(in CoopAudioEventPayload payload)
    {
        
    }

    public static void ServerBroadcastExcept(in CoopAudioEventPayload payload, NetPeer excludedPeer)
    {
        
    }
}
