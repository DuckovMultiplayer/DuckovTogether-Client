















using LiteNetLib;
using LiteNetLib.Utils;

namespace EscapeFromDuckovCoopMod.Net;




public static class NetworkExtensions
{
    
    
    
    public static void SendForced(this NetManager netManager, NetDataWriter writer, DeliveryMethod method, byte channel = 0)
    {
        netManager.SendToAll(writer.Data, 0, writer.Length, channel, method);
    }

    
    
    
    public static string GetNetworkStats(this NetManager netManager)
    {
        var stats = new System.Text.StringBuilder();
        stats.AppendLine("=== 网络统计 ===");
        stats.AppendLine($"连接数: {netManager.ConnectedPeersCount}");
        stats.AppendLine($"运行状态: {(netManager.IsRunning ? "运行中" : "已停止")}");

        if (netManager.FirstPeer != null)
        {
            var peer = netManager.FirstPeer;
            var statistics = peer.Statistics;
            stats.AppendLine($"\n=== 第一个Peer统计 ===");
            stats.AppendLine($"RTT (往返时间): {peer.Ping}ms");
            stats.AppendLine($"发送字节: {statistics.BytesSent}");
            stats.AppendLine($"接收字节: {statistics.BytesReceived}");
            stats.AppendLine($"丢包数: {statistics.PacketLoss}");
            stats.AppendLine($"发送包数: {statistics.PacketsSent}");
            stats.AppendLine($"接收包数: {statistics.PacketsReceived}");
        }

        return stats.ToString();
    }
}

