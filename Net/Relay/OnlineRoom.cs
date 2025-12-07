using System;
using Newtonsoft.Json;

namespace EscapeFromDuckovCoopMod.Net.Relay;

public class OnlineRoom
{
    [JsonProperty("room_id")]
    public string RoomId { get; set; }
    
    [JsonProperty("room_name")]
    public string RoomName { get; set; }
    
    [JsonProperty("host_name")]
    public string HostName { get; set; }
    
    [JsonProperty("host_steam_id")]
    public string HostSteamId { get; set; }
    
    [JsonProperty("node_id")]
    public string NodeId { get; set; }
    
    [JsonProperty("current_players")]
    public int CurrentPlayers { get; set; }
    
    [JsonProperty("max_players")]
    public int MaxPlayers { get; set; }
    
    [JsonProperty("has_password")]
    public bool HasPassword { get; set; }
    
    [JsonProperty("map_name")]
    public string MapName { get; set; }
    
    [JsonProperty("host_address")]
    public string HostAddress { get; set; }
    
    [JsonProperty("host_port")]
    public int HostPort { get; set; }
    
    [JsonIgnore]
    public string RelayAddress => "60.182.113.127";
    
    [JsonIgnore]
    public int RelayPort => 53697;
    
    [JsonProperty("created_at")]
    public DateTime CreateTime { get; set; }
    
    [JsonProperty("last_heartbeat")]
    public DateTime LastHeartbeat { get; set; }
    
    [JsonIgnore]
    public int Latency { get; set; } = -1;
    
    [JsonIgnore]
    public bool IsP2P { get; set; } = false;
    
    [JsonIgnore]
    public ulong P2PLobbyId { get; set; } = 0;
    
    public bool IsFull => CurrentPlayers >= MaxPlayers;
    public string PlayersText => $"{CurrentPlayers}/{MaxPlayers}";
    public TimeSpan Uptime => DateTime.Now - CreateTime;
    
    public string DisplayName => HasPassword ? $"[密] {RoomName}" : RoomName;
    
    public string GetNodeDisplayName()
    {
        var nodes = RelayNode.GetAvailableNodes();
        foreach (var node in nodes)
        {
            if (node.NodeId == NodeId)
            {
                return node.NodeName;
            }
        }
        return NodeId;
    }
}
