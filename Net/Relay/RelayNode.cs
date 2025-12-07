using System;

namespace EscapeFromDuckovCoopMod.Net.Relay;

public class RelayNode
{
    public string NodeId { get; set; }
    public string NodeName { get; set; }
    public string Address { get; set; }
    public int Port { get; set; }
    public string VirtualAddress { get; set; }
    public int VirtualPort { get; set; }
    public int Latency { get; set; } = -1;
    public bool IsAvailable { get; set; } = false;
    public DateTime LastPingTime { get; set; }
    
    public string DisplayName => $"{NodeName} ({(IsAvailable ? $"{Latency}ms" : "离线")})";
    public string FullAddress => $"{Address}:{Port}";
    public string DisplayAddress => $"{VirtualAddress}:{VirtualPort}";
    
    private static RelayNode[] _availableNodes;
    
    public static RelayNode[] GetAvailableNodes()
    {
        if (_availableNodes == null)
        {
            const string relayAddress = "60.182.113.127";
            const int relayPort = 53697;
            
            _availableNodes = new[]
            {
                new RelayNode
                {
                    NodeId = "beijing",
                    NodeName = "北京",
                    Address = relayAddress,
                    Port = relayPort,
                    VirtualAddress = "10.1.1.1",
                    VirtualPort = 8001
                },
                new RelayNode
                {
                    NodeId = "tianjin",
                    NodeName = "天津",
                    Address = relayAddress,
                    Port = relayPort,
                    VirtualAddress = "10.1.2.1",
                    VirtualPort = 8012
                },
                new RelayNode
                {
                    NodeId = "guangdong",
                    NodeName = "广东",
                    Address = relayAddress,
                    Port = relayPort,
                    VirtualAddress = "10.1.3.1",
                    VirtualPort = 8023
                },
                new RelayNode
                {
                    NodeId = "japan",
                    NodeName = "日本",
                    Address = relayAddress,
                    Port = relayPort,
                    VirtualAddress = "10.2.1.1",
                    VirtualPort = 9001
                },
                new RelayNode
                {
                    NodeId = "usa",
                    NodeName = "美国",
                    Address = relayAddress,
                    Port = relayPort,
                    VirtualAddress = "10.3.1.1",
                    VirtualPort = 9050
                }
            };
        }
        
        return _availableNodes;
    }
}
