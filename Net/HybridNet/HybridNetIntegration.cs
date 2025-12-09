using DuckovNet;
using UnityEngine;
using EscapeFromDuckovCoopMod.Utils.NetHelper;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public static class HybridNetIntegration
{
    private static bool _initialized = false;
    
    public static void Initialize()
    {
        if (_initialized) return;
        
        Debug.Log("[HybridNet] 初始化混合网络框架...");
        
        HybridNetBridge.Initialize();
        
        RegisterWithOpHandler();
        
        _initialized = true;
        Debug.Log("[HybridNet] 混合网络框架初始化完成");
    }
    
    private static void RegisterWithOpHandler()
    {
        Debug.Log("[HybridNet] HybridNet框架已初始化，无需Op码注册");
    }
    
    private static void HandleHybridNetMessage(NetPeer peer, NetDataReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        HybridNetCore.HandleIncoming(reader, peer);
    }
    
    public static void OnPeerConnected(NetPeer peer)
    {
        Debug.Log($"[HybridNet] 对等端连接: {peer.EndPoint}");
    }
    
    public static void OnPeerDisconnected(NetPeer peer)
    {
        Debug.Log($"[HybridNet] 对等端断开: {peer.EndPoint}");
        HybridNetCore.Interest.RemovePeer(peer);
        HybridNetCore.Bandwidth.RemovePeer(peer);
    }
    
    public static void Update()
    {
        if (!_initialized) return;
        
        var service = ModBehaviourF.Instance;
        if (service == null || !service.networkStarted) return;
        
        if (service.IsServer)
        {
            UpdatePlayerPositions();
        }
    }
    
    private static void UpdatePlayerPositions()
    {
        var service = ModBehaviourF.Instance;
        if (service == null) return;
        
        var mainPlayer = CharacterMainControl.Main;
        if (mainPlayer != null && service.connectedPeer == null)
        {
            HybridNetCore.Interest.UpdatePlayerPosition(null, mainPlayer.transform.position);
        }
        
        foreach (var kv in service.remoteCharacters)
        {
            if (kv.Value != null)
            {
                HybridNetCore.Interest.UpdatePlayerPosition(kv.Key, kv.Value.transform.position);
            }
        }
    }
    
    public static string GetDebugInfo()
    {
        if (!_initialized) return "HybridNet: 未初始化";
        
        var bandwidth = HybridNetCore.Bandwidth.GetDebugInfo();
        var interest = $"兴趣半径: {HybridNetCore.Interest.InterestRadius}m";
        
        return $"[HybridNet] {bandwidth} | {interest}";
    }
}
