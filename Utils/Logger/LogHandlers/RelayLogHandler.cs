using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using EscapeFromDuckovCoopMod.Utils.Logger.Core;
using Newtonsoft.Json;

namespace EscapeFromDuckovCoopMod.Utils.Logger.LogHandlers
{
    public class RelayLogHandler : Core.ILogHandler
    {
        private UdpClient _udpClient;
        private IPEndPoint _relayEndpoint;
        private string _roomId;
        private bool _isEnabled;
        private readonly object _lock = new object();

        public RelayLogHandler()
        {
            _isEnabled = false;
        }

        public void Enable(string relayAddress, int relayPort, string roomId)
        {
            lock (_lock)
            {
                try
                {
                    _roomId = roomId;
                    _udpClient = new UdpClient();
                    _relayEndpoint = new IPEndPoint(IPAddress.Parse(relayAddress), relayPort);
                    _isEnabled = true;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[RelayLogHandler] 初始化失败: {ex.Message}");
                    _isEnabled = false;
                }
            }
        }

        public void Disable()
        {
            lock (_lock)
            {
                _isEnabled = false;
                if (_udpClient != null)
                {
                    try
                    {
                        _udpClient.Close();
                    }
                    catch { }
                    _udpClient = null;
                }
            }
        }

        public void Log<TLog>(TLog log) where TLog : struct, ILog
        {
            if (!_isEnabled || _udpClient == null || string.IsNullOrEmpty(_roomId))
                return;

            try
            {
                var logMessage = log.ParseToString();
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                var message = new
                {
                    type = "upload_log",
                    room_id = _roomId,
                    log_entry = logMessage,
                    timestamp = timestamp
                };

                var json = JsonConvert.SerializeObject(message);
                var data = Encoding.UTF8.GetBytes(json);

                _udpClient.Send(data, data.Length, _relayEndpoint);
            }
            catch
            {
            }
        }
    }
}
