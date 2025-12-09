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
    
    public class NetPeer
    {
        public int Id { get; set; }
        public string EndPoint { get; set; }
        public ConnectionState ConnectionState { get; set; }
        public int Ping { get; set; }
        
        public Action<byte[], DeliveryMethod> SendAction { get; set; }
        
        public void Send(NetDataWriter writer, DeliveryMethod method)
        {
            SendAction?.Invoke(writer.CopyData(), method);
        }
        
        public void Send(byte[] data, DeliveryMethod method)
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
}
