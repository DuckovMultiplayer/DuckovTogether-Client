using System;
using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod.Core;

public interface INetworkService
{
    bool IsConnected { get; }
    bool IsInGame { get; }
    int LocalPlayerId { get; }
    void Connect(string host, int port);
    void Disconnect();
    void SendMessage(byte[] data);
}

public interface IPlayerService
{
    void SpawnRemotePlayer(int playerId, string name);
    void RemoveRemotePlayer(int playerId);
    void UpdatePlayerPosition(int playerId, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation);
    bool IsRemotePlayer(int playerId);
}

public interface IAISyncService
{
    void SpawnRemoteAI(int aiId, string aiType);
    void RemoveRemoteAI(int aiId);
    void UpdateAIPosition(int aiId, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation);
    bool IsRemoteAI(int aiId);
}

public static class Services
{
    private static readonly Dictionary<Type, object> _services = new();
    private static readonly object _lock = new();
    
    public static void Register<T>(T service) where T : class
    {
        if (service == null) throw new ArgumentNullException(nameof(service));
        lock (_lock)
        {
            _services[typeof(T)] = service;
        }
    }
    
    public static T Get<T>() where T : class
    {
        lock (_lock)
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;
            throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
        }
    }
    
    public static T? TryGet<T>() where T : class
    {
        lock (_lock)
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;
            return null;
        }
    }
    
    public static bool IsRegistered<T>() where T : class
    {
        lock (_lock)
        {
            return _services.ContainsKey(typeof(T));
        }
    }
    
    public static void Clear()
    {
        lock (_lock)
        {
            _services.Clear();
        }
    }
}

public static class NetworkState
{
    private static INetworkService? _networkService;
    
    public static bool IsConnected => _networkService?.IsConnected ?? false;
    public static bool IsInGame => _networkService?.IsInGame ?? false;
    public static int LocalPlayerId => _networkService?.LocalPlayerId ?? -1;
    
    public static void Initialize(INetworkService service)
    {
        _networkService = service;
    }
    
    public static void Connect(string host, int port) => _networkService?.Connect(host, port);
    public static void Disconnect() => _networkService?.Disconnect();
    public static void SendMessage(byte[] data) => _networkService?.SendMessage(data);
}

public static class CoopEvents
{
    public static event Action? OnConnected;
    public static event Action<string>? OnDisconnected;
    public static event Action<int, string>? OnPlayerJoined;
    public static event Action<int>? OnPlayerLeft;
    public static event Action<string>? OnSceneChanged;
    public static event Action<int, float>? OnPlayerDamaged;
    public static event Action<int>? OnPlayerDied;
    
    public static void RaiseConnected() => OnConnected?.Invoke();
    public static void RaiseDisconnected(string reason) => OnDisconnected?.Invoke(reason);
    public static void RaisePlayerJoined(int playerId, string name) => OnPlayerJoined?.Invoke(playerId, name);
    public static void RaisePlayerLeft(int playerId) => OnPlayerLeft?.Invoke(playerId);
    public static void RaiseSceneChanged(string sceneId) => OnSceneChanged?.Invoke(sceneId);
    public static void RaisePlayerDamaged(int playerId, float damage) => OnPlayerDamaged?.Invoke(playerId, damage);
    public static void RaisePlayerDied(int playerId) => OnPlayerDied?.Invoke(playerId);
    
    public static void ClearAll()
    {
        OnConnected = null;
        OnDisconnected = null;
        OnPlayerJoined = null;
        OnPlayerLeft = null;
        OnSceneChanged = null;
        OnPlayerDamaged = null;
        OnPlayerDied = null;
    }
}
