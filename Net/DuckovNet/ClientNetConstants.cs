using System;
using System.Collections.Generic;

namespace DuckovNet
{
    public enum DeliveryMode : byte
    {
        Unreliable = 0,
        Reliable = 1,
        ReliableOrdered = 2
    }
    
    public enum DeliveryMethod : byte
    {
        Unreliable = 0,
        Sequenced = 1,
        ReliableUnordered = 2,
        ReliableSequenced = 3,
        ReliableOrdered = 4
    }
    
    public enum DisconnectReason
    {
        ConnectionFailed,
        Timeout,
        HostUnreachable,
        NetworkUnreachable,
        RemoteConnectionClose,
        DisconnectPeerCalled,
        ConnectionRejected,
        InvalidProtocol,
        UnknownHost,
        Reconnect,
        PeerToPeerConnection
    }
    
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting
    }
    
    public class NetStatistics
    {
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public long PacketsSent { get; set; }
        public long PacketsReceived { get; set; }
        public float PacketLoss { get; set; }
    }
    
    public class NetPeer
    {
        public int Id { get; set; }
        public string EndPoint { get; set; }
        public ConnectionState ConnectionState { get; set; }
        public int Ping { get; set; }
        public NetStatistics Statistics { get; } = new NetStatistics();
        
        public Action<byte[], DeliveryMethod> SendAction { get; set; }
        
        public void Send(NetDataWriter writer, DeliveryMethod method)
        {
            SendAction?.Invoke(writer.CopyData(), method);
        }
        
        public void Send(byte[] data, DeliveryMethod method)
        {
            SendAction?.Invoke(data, method);
        }
        
        public void Send(byte[] data, int start, int length, DeliveryMethod method)
        {
            var segment = new byte[length];
            Buffer.BlockCopy(data, start, segment, 0, length);
            SendAction?.Invoke(segment, method);
        }
        
        public void Send(NetDataWriter writer, byte channel, DeliveryMethod method)
        {
            SendAction?.Invoke(writer.CopyData(), method);
        }
        
        public void Send(byte[] data, byte channel, DeliveryMethod method)
        {
            SendAction?.Invoke(data, method);
        }
        
        public void Disconnect()
        {
            ConnectionState = ConnectionState.Disconnected;
        }
    }
    
    public class DisconnectInfo
    {
        public DisconnectReason Reason { get; set; }
    }
    
    public class ConnectionRequest
    {
        public string RemoteEndPoint { get; set; }
        public byte[] Data { get; set; }
        
        public NetPeer AcceptIfKey(string key)
        {
            return new NetPeer();
        }
        
        public void Reject()
        {
        }
    }
    
    public interface INetEventListener
    {
        void OnPeerConnected(NetPeer peer);
        void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason);
        void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod);
        void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError);
        void OnConnectionRequest(ConnectionRequest request);
        void OnNetworkLatencyUpdate(NetPeer peer, int latency);
    }
    
    public class NetPacketReader : NetDataReader
    {
        public NetPacketReader() : base() { }
        public NetPacketReader(byte[] data) : base(data) { }
        public NetPacketReader(byte[] data, int offset, int length) : base(data, offset, length) { }
        
        public void Recycle() { }
    }
    
    public interface INetSerializable
    {
        void Serialize(NetDataWriter writer);
        void Deserialize(NetDataReader reader);
    }
    
    public class NetManager
    {
        public int UpdateTime { get; set; } = 15;
        public bool IsRunning { get; private set; }
        public List<NetPeer> ConnectedPeerList { get; } = new List<NetPeer>();
        public int ConnectedPeersCount => ConnectedPeerList.Count;
        public NetPeer FirstPeer => ConnectedPeerList.Count > 0 ? ConnectedPeerList[0] : null;
        
        public event Action<NetPeer> OnPeerConnected;
        public event Action<NetPeer, DisconnectInfo> OnPeerDisconnected;
        public event Action<NetPeer, NetPacketReader, DeliveryMethod> OnNetworkReceive;
        
        public void Start() { IsRunning = true; }
        public void Stop() { IsRunning = false; }
        public void PollEvents() { }
        
        public void SendToAll(NetDataWriter writer, DeliveryMethod method) { }
        public void SendToAll(byte[] data, DeliveryMethod method) { }
        public void SendToAll(byte[] data, int start, int length, byte channel, DeliveryMethod method) { }
        public void SendUnconnectedMessage(NetDataWriter writer, System.Net.IPEndPoint endpoint) { }
        public void SendUnconnectedMessage(NetDataWriter writer, string address, int port) { }
        public void SendUnconnectedMessage(byte[] data, int start, int length, System.Net.IPEndPoint endpoint) { }
        public void DisconnectAll() { }
    }
    
    public class QuicTransport
    {
        public static bool IsSupported => false;
        public const byte PROTOCOL_VERSION = 1;
        
        public QuicPeer ServerPeer { get; set; }
        
        public event Action<QuicPeer> OnPeerConnected;
        public event Action<QuicPeer, string> OnPeerDisconnected;
        public event Action<QuicPeer, byte[], DeliveryMode> OnDataReceived;
        
        public void Stop() { }
        public void Disconnect(QuicPeer peer) { }
        public void Disconnect(QuicPeer peer, string reason) { }
        public void Disconnect(int peerId) { }
        public void Send(QuicPeer peer, byte[] data, DeliveryMode mode) { }
        public void Send(int peerId, byte[] data, DeliveryMode mode) { }
        public System.Threading.Tasks.Task<QuicPeer> ConnectAsync(string host, int port) => System.Threading.Tasks.Task.FromResult<QuicPeer>(null);
    }
    
    public class QuicPeer
    {
        public int Id { get; set; }
        public string EndPoint { get; set; }
        public bool IsConnected { get; set; }
        public int Latency { get; set; }
        
        public void Send(byte[] data, DeliveryMode mode) { }
        public void Send(NetDataWriter writer, DeliveryMode mode) { }
        public void Disconnect() { }
    }
}
