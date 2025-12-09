using System.Collections.Generic;
using DuckovNet;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class InterestManager
{
    private readonly Dictionary<NetPeer, Vector3> _playerPositions = new();
    private readonly Dictionary<int, Vector3> _entityPositions = new();
    private readonly HashSet<int> _globalEntities = new();
    
    public float InterestRadius { get; set; } = 100f;
    public float CriticalRadius { get; set; } = 50f;
    
    public void UpdatePlayerPosition(NetPeer peer, Vector3 position)
    {
        _playerPositions[peer] = position;
    }
    
    public void UpdateEntityPosition(int entityId, Vector3 position)
    {
        _entityPositions[entityId] = position;
    }
    
    public void MarkGlobalEntity(int entityId, bool isGlobal = true)
    {
        if (isGlobal)
            _globalEntities.Add(entityId);
        else
            _globalEntities.Remove(entityId);
    }
    
    public bool ShouldSend<T>(T message, NetPeer peer) where T : IHybridMessage
    {
        if (message.Priority == MessagePriority.Critical)
            return true;
        
        if (message is ISpatialMessage spatialMsg)
        {
            if (spatialMsg.EntityId > 0 && _globalEntities.Contains(spatialMsg.EntityId))
                return true;
            
            if (!_playerPositions.TryGetValue(peer, out var playerPos))
                return true;
            
            var entityPos = spatialMsg.Position;
            var distance = Vector3.Distance(playerPos, entityPos);
            
            if (message.Priority == MessagePriority.High)
                return distance <= InterestRadius * 1.5f;
            
            if (message.Priority == MessagePriority.Normal)
                return distance <= InterestRadius;
            
            return distance <= InterestRadius * 0.5f;
        }
        
        return true;
    }
    
    public List<NetPeer> GetInterestedPeers(Vector3 position, float radius = -1)
    {
        if (radius < 0) radius = InterestRadius;
        
        var result = new List<NetPeer>();
        foreach (var kv in _playerPositions)
        {
            if (Vector3.Distance(kv.Value, position) <= radius)
                result.Add(kv.Key);
        }
        return result;
    }
    
    public void RemovePeer(NetPeer peer)
    {
        _playerPositions.Remove(peer);
    }
    
    public void RemoveEntity(int entityId)
    {
        _entityPositions.Remove(entityId);
        _globalEntities.Remove(entityId);
    }
}

public interface ISpatialMessage
{
    int EntityId { get; }
    Vector3 Position { get; }
}
