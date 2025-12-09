using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DuckovNet
{
    public class KcpClient
    {
        private UdpClient _udp;
        private Thread _receiveThread;
        private Thread _updateThread;
        private bool _running;
        private readonly object _lock = new();
        
        private IPEndPoint _serverEndpoint;
        private KcpChannel _channel;
        private bool _isConnected;
        private int _peerId;
        private long _lastActivity;
        private string _connectionKey = "DuckovNet";
        
        public bool IsConnected => _isConnected;
        public int Latency { get; private set; }
        
        private static readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();
        private static long TickCount64 => _sw.ElapsedMilliseconds;
        
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<byte[], DeliveryMode> OnDataReceived;
        
        private const byte MSG_CONNECT = 1;
        private const byte MSG_ACCEPT = 2;
        private const byte MSG_DISCONNECT = 3;
        private const byte MSG_DATA = 4;
        private const byte MSG_PING = 5;
        private const byte MSG_PONG = 6;
        private const byte MSG_KCP = 8;
        
        private const int KCP_INTERVAL = 10;
        private const int TIMEOUT_MS = 15000;
        private const int CONNECT_TIMEOUT_MS = 5000;
        
        public bool Connect(string host, int port, string key = "DuckovNet")
        {
            _connectionKey = key;
            
            try
            {
                _serverEndpoint = new IPEndPoint(IPAddress.Parse(host), port);
                _udp = new UdpClient(0);
                _udp.Client.ReceiveBufferSize = 1024 * 1024;
                _udp.Client.SendBufferSize = 1024 * 1024;
                _udp.Client.ReceiveTimeout = 1000;
                _running = true;
                
                _channel = new KcpChannel(1, (data) =>
                {
                    var packet = new byte[data.Length + 1];
                    packet[0] = MSG_KCP;
                    Buffer.BlockCopy(data, 0, packet, 1, data.Length);
                    SendRaw(packet);
                });
                
                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "KCP-Receive" };
                _receiveThread.Start();
                
                _updateThread = new Thread(UpdateLoop) { IsBackground = true, Name = "KCP-Update" };
                _updateThread.Start();
                
                SendConnect();
                
                var startTime = TickCount64;
                while (!_isConnected && TickCount64 - startTime < CONNECT_TIMEOUT_MS)
                {
                    Thread.Sleep(10);
                }
                
                return _isConnected;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KcpClient] Connect failed: {ex.Message}");
                return false;
            }
        }
        
        public void Disconnect(string reason = "")
        {
            if (!_running) return;
            
            SendRaw(MSG_DISCONNECT, System.Text.Encoding.UTF8.GetBytes(reason ?? ""));
            
            _running = false;
            _isConnected = false;
            
            _udp?.Close();
            
            OnDisconnected?.Invoke(reason);
        }
        
        public void Send(byte[] data, DeliveryMode mode)
        {
            if (!_isConnected) return;
            
            if (mode == DeliveryMode.Unreliable)
            {
                var packet = new byte[data.Length + 2];
                packet[0] = MSG_DATA;
                packet[1] = 0;
                Buffer.BlockCopy(data, 0, packet, 2, data.Length);
                SendRaw(packet);
            }
            else
            {
                lock (_lock)
                {
                    _channel?.Send(data, mode == DeliveryMode.ReliableOrdered);
                }
            }
        }
        
        public void SendPing()
        {
            var data = BitConverter.GetBytes(TickCount64);
            SendRaw(MSG_PING, data);
        }
        
        private void SendConnect()
        {
            var data = System.Text.Encoding.UTF8.GetBytes(_connectionKey);
            SendRaw(MSG_CONNECT, data);
        }
        
        private void SendRaw(byte msgType, byte[] data)
        {
            var packet = new byte[data.Length + 1];
            packet[0] = msgType;
            Buffer.BlockCopy(data, 0, packet, 1, data.Length);
            SendRaw(packet);
        }
        
        private void SendRaw(byte[] data)
        {
            try { _udp?.Send(data, data.Length, _serverEndpoint); } catch { }
        }
        
        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Any, 0);
                    var data = _udp.Receive(ref endpoint);
                    if (data.Length < 1) continue;
                    
                    if (!endpoint.Equals(_serverEndpoint)) continue;
                    
                    _lastActivity = TickCount64;
                    ProcessPacket(data);
                }
                catch (SocketException) { }
                catch { }
            }
        }
        
        private void ProcessPacket(byte[] data)
        {
            var msgType = data[0];
            var payload = new byte[data.Length - 1];
            if (payload.Length > 0)
                Buffer.BlockCopy(data, 1, payload, 0, payload.Length);
            
            switch (msgType)
            {
                case MSG_ACCEPT:
                    _isConnected = true;
                    _lastActivity = TickCount64;
                    OnConnected?.Invoke();
                    break;
                    
                case MSG_DISCONNECT:
                    var reason = System.Text.Encoding.UTF8.GetString(payload);
                    _running = false;
                    _isConnected = false;
                    OnDisconnected?.Invoke(reason);
                    break;
                    
                case MSG_DATA:
                    if (payload.Length > 1)
                    {
                        var content = new byte[payload.Length - 1];
                        Buffer.BlockCopy(payload, 1, content, 0, content.Length);
                        OnDataReceived?.Invoke(content, DeliveryMode.Unreliable);
                    }
                    break;
                    
                case MSG_PONG:
                    if (payload.Length >= 8)
                    {
                        var sendTime = BitConverter.ToInt64(payload, 0);
                        Latency = (int)(TickCount64 - sendTime);
                    }
                    break;
                    
                case MSG_KCP:
                    lock (_lock)
                    {
                        _channel?.Input(payload);
                    }
                    break;
            }
        }
        
        private void UpdateLoop()
        {
            var lastPing = TickCount64;
            
            while (_running)
            {
                var now = TickCount64;
                
                lock (_lock)
                {
                    _channel?.Update((uint)(now & 0xFFFFFFFF));
                    
                    while (_channel?.TryReceive(out var data) == true)
                    {
                        OnDataReceived?.Invoke(data, DeliveryMode.Reliable);
                    }
                }
                
                if (_isConnected)
                {
                    if (now - lastPing > 1000)
                    {
                        SendPing();
                        lastPing = now;
                    }
                    
                    if (now - _lastActivity > TIMEOUT_MS)
                    {
                        _isConnected = false;
                        _running = false;
                        OnDisconnected?.Invoke("timeout");
                    }
                }
                
                Thread.Sleep(KCP_INTERVAL);
            }
        }
    }
}
