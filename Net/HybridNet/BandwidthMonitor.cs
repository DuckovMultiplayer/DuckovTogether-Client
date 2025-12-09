using System.Collections.Generic;
using DuckovNet;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public class BandwidthMonitor
{
    private class BandwidthStats
    {
        public long BytesSent;
        public long BytesReceived;
        public float LastResetTime;
        public Queue<(float time, int bytes)> SendHistory = new();
        public Queue<(float time, int bytes)> ReceiveHistory = new();
    }
    
    private readonly Dictionary<NetPeer, BandwidthStats> _peerStats = new();
    private BandwidthStats _globalStats = new();
    private const float WINDOW_SIZE = 1.0f;
    
    public void RecordSent(int bytes, NetPeer peer)
    {
        if (!_peerStats.TryGetValue(peer, out var stats))
        {
            stats = new BandwidthStats { LastResetTime = Time.time };
            _peerStats[peer] = stats;
        }
        
        stats.BytesSent += bytes;
        stats.SendHistory.Enqueue((Time.time, bytes));
        
        _globalStats.BytesSent += bytes;
        _globalStats.SendHistory.Enqueue((Time.time, bytes));
        
        CleanupHistory(stats);
    }
    
    public void RecordReceived(int bytes, NetPeer peer)
    {
        if (!_peerStats.TryGetValue(peer, out var stats))
        {
            stats = new BandwidthStats { LastResetTime = Time.time };
            _peerStats[peer] = stats;
        }
        
        stats.BytesReceived += bytes;
        stats.ReceiveHistory.Enqueue((Time.time, bytes));
        
        _globalStats.BytesReceived += bytes;
        _globalStats.ReceiveHistory.Enqueue((Time.time, bytes));
        
        CleanupHistory(stats);
    }
    
    public long GetCurrentBandwidth()
    {
        CleanupHistory(_globalStats);
        
        long total = 0;
        foreach (var (_, bytes) in _globalStats.SendHistory)
            total += bytes;
        foreach (var (_, bytes) in _globalStats.ReceiveHistory)
            total += bytes;
        
        return total;
    }
    
    public long GetPeerBandwidth(NetPeer peer)
    {
        if (!_peerStats.TryGetValue(peer, out var stats))
            return 0;
        
        CleanupHistory(stats);
        
        long total = 0;
        foreach (var (_, bytes) in stats.SendHistory)
            total += bytes;
        foreach (var (_, bytes) in stats.ReceiveHistory)
            total += bytes;
        
        return total;
    }
    
    public void RemovePeer(NetPeer peer)
    {
        _peerStats.Remove(peer);
    }
    
    private void CleanupHistory(BandwidthStats stats)
    {
        var cutoff = Time.time - WINDOW_SIZE;
        
        while (stats.SendHistory.Count > 0 && stats.SendHistory.Peek().time < cutoff)
            stats.SendHistory.Dequeue();
        
        while (stats.ReceiveHistory.Count > 0 && stats.ReceiveHistory.Peek().time < cutoff)
            stats.ReceiveHistory.Dequeue();
    }
    
    public string GetDebugInfo()
    {
        var bandwidth = GetCurrentBandwidth();
        var kbps = bandwidth / 1024f;
        return $"总带宽: {kbps:F2} KB/s | 连接数: {_peerStats.Count}";
    }
}
