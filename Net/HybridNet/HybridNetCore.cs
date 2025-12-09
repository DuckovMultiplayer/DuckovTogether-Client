using System;
using System.Collections.Generic;
using DuckovNet;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public enum MessagePriority
{
    Critical = 0,
    High = 1,
    Normal = 2,
    Low = 3,
    Background = 4
}

public enum SerializationMode
{
    Auto,
    Json,
    Binary
}

public interface IHybridMessage
{
    string MessageType { get; }
    MessagePriority Priority { get; }
    SerializationMode PreferredMode { get; }
}

public static class HybridNetCore
{
    private static readonly Dictionary<string, Action<byte[], NetPeer>> _handlers = new();
    private static readonly InterestManager _interestManager = new();
    private static readonly BandwidthMonitor _bandwidthMonitor = new();
    
    public static InterestManager Interest => _interestManager;
    public static BandwidthMonitor Bandwidth => _bandwidthMonitor;
    
    public static void RegisterHandler<T>(Action<T, NetPeer> handler) where T : IHybridMessage, new()
    {
        var sample = new T();
        var messageType = sample.MessageType;
        
        _handlers[messageType] = (bytes, peer) =>
        {
            T message;
            if (sample.PreferredMode == SerializationMode.Binary)
                message = BinarySerializer.Deserialize<T>(bytes);
            else
                message = JsonSerializer.Deserialize<T>(bytes);
            
            handler(message, peer);
        };
    }
    
    public static void Send<T>(T message, NetPeer target = null, DeliveryMethod? deliveryOverride = null) where T : IHybridMessage
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null) return;
        
        var relevantPeers = new List<NetPeer>();
        
        if (target != null)
        {
            relevantPeers.Add(target);
        }
        else if (mod.IsServer)
        {
            foreach (var peer in mod.playerStatuses.Keys)
            {
                if (_interestManager.ShouldSend(message, peer))
                    relevantPeers.Add(peer);
            }
        }
        else
        {
            if (mod.connectedPeer != null)
                relevantPeers.Add(mod.connectedPeer);
        }
        
        if (relevantPeers.Count == 0) return;
        
        var mode = DecideSerializationMode(message);
        byte[] payload;
        
        if (mode == SerializationMode.Binary)
            payload = BinarySerializer.Serialize(message);
        else
            payload = JsonSerializer.Serialize(message);
        
        var delivery = deliveryOverride ?? GetDeliveryMethod(message.Priority);
        
        var writer = mod.writer;
        writer.Reset();
 
        writer.Put((byte)mode);
        writer.Put(message.MessageType);
        writer.Put(payload.Length);
        writer.Put(payload);
        
        foreach (var peer in relevantPeers)
        {
            if (!string.IsNullOrEmpty(mod._relayRoomId))
            {
                SendViaRelay(mod, writer.Data, writer.Length, delivery);
            }
            else
            {
                peer.Send(writer, delivery);
            }
            _bandwidthMonitor.RecordSent(payload.Length + 20, peer);
        }
    }
    
    public static void HandleIncoming(NetDataReader reader, NetPeer fromPeer)
    {
        try
        {
            var firstByte = reader.GetByte();
            
            
            if (firstByte == 9)
            {
                var jsonData = reader.GetString();
                JsonMessageRouter.HandleJsonMessageInternal(jsonData, fromPeer);
                return;
            }
            
            
            var mode = (SerializationMode)firstByte;
            var messageType = reader.GetString();
            var payloadLength = reader.GetInt();
            var payload = new byte[payloadLength];
            reader.GetBytes(payload, payloadLength);
            
            if (_handlers.TryGetValue(messageType, out var handler))
            {
                handler(payload, fromPeer);
                _bandwidthMonitor.RecordReceived(payloadLength + 20, fromPeer);
            }
            else
            {
                Debug.LogWarning($"[HybridNet] 未注册的消息类型: {messageType}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HybridNet] 处理消息失败: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    private static SerializationMode DecideSerializationMode<T>(T message) where T : IHybridMessage
    {
        if (message.PreferredMode != SerializationMode.Auto)
            return message.PreferredMode;
        
        var bandwidth = _bandwidthMonitor.GetCurrentBandwidth();
        
        if (bandwidth > 1024 * 1024)
            return SerializationMode.Binary;
        
        if (message.Priority <= MessagePriority.High)
            return SerializationMode.Binary;
        
        return SerializationMode.Json;
    }
    
    private static DeliveryMethod GetDeliveryMethod(MessagePriority priority)
    {
        return priority switch
        {
            MessagePriority.Critical => DeliveryMethod.ReliableOrdered,
            MessagePriority.High => DeliveryMethod.ReliableOrdered,
            MessagePriority.Normal => DeliveryMethod.ReliableUnordered,
            MessagePriority.Low => DeliveryMethod.Sequenced,
            MessagePriority.Background => DeliveryMethod.Unreliable,
            _ => DeliveryMethod.ReliableOrdered
        };
    }
    
    private static void SendViaRelay(ModBehaviourF mod, byte[] data, int length, DeliveryMethod delivery)
    {
        try
        {
            var roomIdBytes = System.Text.Encoding.UTF8.GetBytes(mod._relayRoomId.PadRight(36));
            var relayData = new byte[36 + length];
            System.Array.Copy(roomIdBytes, 0, relayData, 0, 36);
            System.Array.Copy(data, 0, relayData, 36, length);
            
            var relayManager = Relay.RelayServerManager.Instance;
            if (relayManager != null && relayManager.SelectedNode != null)
            {
                var udpClient = new System.Net.Sockets.UdpClient();
                var endpoint = new System.Net.IPEndPoint(
                    System.Net.IPAddress.Parse(relayManager.SelectedNode.Address),
                    relayManager.SelectedNode.Port
                );
                udpClient.Send(relayData, relayData.Length, endpoint);
                udpClient.Close();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HybridNet] 中继发送失败: {ex.Message}");
        }
    }
}
