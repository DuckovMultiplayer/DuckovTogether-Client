using System;
using System.Collections.Generic;

namespace DuckovNet
{
    public class KcpChannel
    {
        private readonly Kcp _kcp;
        private readonly Action<byte[]> _output;
        private readonly Queue<byte[]> _receiveQueue = new();
        private uint _nextSendSn;
        private readonly object _lock = new();

        public KcpChannel(uint conv, Action<byte[]> output)
        {
            _output = output;
            _kcp = new Kcp(conv, OnKcpOutput);
            
            _kcp.SetNoDelay(1, 10, 2, 1);
            _kcp.SetWindowSize(128, 128);
            _kcp.SetMtu(1200);
        }

        private void OnKcpOutput(byte[] data, int size)
        {
            var packet = new byte[size];
            Buffer.BlockCopy(data, 0, packet, 0, size);
            _output?.Invoke(packet);
        }

        public void Send(byte[] data, bool ordered)
        {
            lock (_lock)
            {
                _kcp.Send(data, 0, data.Length);
            }
        }

        public void Input(byte[] data)
        {
            lock (_lock)
            {
                _kcp.Input(data, 0, data.Length);
            }
        }

        public void Update(uint current)
        {
            lock (_lock)
            {
                _kcp.Update(current);
                
                while (true)
                {
                    var size = _kcp.PeekSize();
                    if (size <= 0) break;
                    
                    var buffer = new byte[size];
                    if (_kcp.Recv(buffer, 0, size) > 0)
                    {
                        _receiveQueue.Enqueue(buffer);
                    }
                }
            }
        }

        public bool TryReceive(out byte[] data)
        {
            lock (_lock)
            {
                if (_receiveQueue.Count > 0)
                {
                    data = _receiveQueue.Dequeue();
                    return true;
                }
                data = null;
                return false;
            }
        }
    }

    public class Kcp
    {
        private const int IKCP_RTO_NDL = 30;
        private const int IKCP_RTO_MIN = 100;
        private const int IKCP_RTO_DEF = 200;
        private const int IKCP_RTO_MAX = 60000;
        private const int IKCP_CMD_PUSH = 81;
        private const int IKCP_CMD_ACK = 82;
        private const int IKCP_CMD_WASK = 83;
        private const int IKCP_CMD_WINS = 84;
        private const int IKCP_ASK_SEND = 1;
        private const int IKCP_ASK_TELL = 2;
        private const int IKCP_WND_SND = 32;
        private const int IKCP_WND_RCV = 128;
        private const int IKCP_MTU_DEF = 1400;
        private const int IKCP_INTERVAL = 100;
        private const int IKCP_OVERHEAD = 24;
        private const int IKCP_DEADLINK = 20;
        private const int IKCP_THRESH_INIT = 2;
        private const int IKCP_THRESH_MIN = 2;
        private const int IKCP_PROBE_INIT = 7000;
        private const int IKCP_PROBE_LIMIT = 120000;

        private readonly uint _conv;
        private uint _mtu = IKCP_MTU_DEF;
        private uint _mss;
        private uint _state;
        private uint _snd_una;
        private uint _snd_nxt;
        private uint _rcv_nxt;
        private uint _ts_recent;
        private uint _ts_lastack;
        private uint _ssthresh = IKCP_THRESH_INIT;
        private int _rx_rttval;
        private int _rx_srtt;
        private int _rx_rto = IKCP_RTO_DEF;
        private int _rx_minrto = IKCP_RTO_MIN;
        private uint _snd_wnd = IKCP_WND_SND;
        private uint _rcv_wnd = IKCP_WND_RCV;
        private uint _rmt_wnd = IKCP_WND_RCV;
        private uint _cwnd;
        private uint _incr;
        private uint _probe;
        private uint _interval = IKCP_INTERVAL;
        private uint _ts_flush = IKCP_INTERVAL;
        private uint _xmit;
        private uint _nodelay;
        private uint _updated;
        private uint _ts_probe;
        private uint _probe_wait;
        private uint _dead_link = IKCP_DEADLINK;
        private uint _current;
        private int _fastresend;
        private int _fastlimit = 5;
        private int _nocwnd;
        private int _stream;

        private readonly List<Segment> _snd_queue = new();
        private readonly List<Segment> _rcv_queue = new();
        private readonly List<Segment> _snd_buf = new();
        private readonly List<Segment> _rcv_buf = new();
        private readonly List<uint> _acklist = new();
        private byte[] _buffer;
        private readonly Action<byte[], int> _output;

        private class Segment
        {
            public uint conv;
            public uint cmd;
            public uint frg;
            public uint wnd;
            public uint ts;
            public uint sn;
            public uint una;
            public uint resendts;
            public uint rto;
            public uint fastack;
            public uint xmit;
            public byte[] data;
        }

        public Kcp(uint conv, Action<byte[], int> output)
        {
            _conv = conv;
            _output = output;
            _mss = _mtu - IKCP_OVERHEAD;
            _buffer = new byte[(_mtu + IKCP_OVERHEAD) * 3];
        }

        public void SetNoDelay(int nodelay, int interval, int resend, int nc)
        {
            if (nodelay >= 0)
            {
                _nodelay = (uint)nodelay;
                _rx_minrto = nodelay != 0 ? IKCP_RTO_NDL : IKCP_RTO_MIN;
            }
            if (interval >= 0)
            {
                _interval = interval < 10 ? 10 : (uint)interval;
            }
            if (resend >= 0)
            {
                _fastresend = resend;
            }
            if (nc >= 0)
            {
                _nocwnd = nc;
            }
        }

        public void SetWindowSize(int sndwnd, int rcvwnd)
        {
            if (sndwnd > 0) _snd_wnd = (uint)sndwnd;
            if (rcvwnd > 0) _rcv_wnd = (uint)rcvwnd;
        }

        public void SetMtu(int mtu)
        {
            if (mtu < 50 || mtu < IKCP_OVERHEAD) return;
            _buffer = new byte[(mtu + IKCP_OVERHEAD) * 3];
            _mtu = (uint)mtu;
            _mss = _mtu - IKCP_OVERHEAD;
        }

        public int Send(byte[] buffer, int offset, int len)
        {
            if (len <= 0) return -1;

            var count = len <= (int)_mss ? 1 : (len + (int)_mss - 1) / (int)_mss;
            if (count > 255) return -2;
            if (count == 0) count = 1;

            for (var i = 0; i < count; i++)
            {
                var size = len > (int)_mss ? (int)_mss : len;
                var seg = new Segment
                {
                    data = new byte[size],
                    frg = (uint)(count - i - 1)
                };
                Buffer.BlockCopy(buffer, offset, seg.data, 0, size);
                _snd_queue.Add(seg);
                offset += size;
                len -= size;
            }

            return 0;
        }

        public int Input(byte[] data, int offset, int size)
        {
            var oldUna = _snd_una;
            if (size < IKCP_OVERHEAD) return -1;

            while (true)
            {
                if (size < IKCP_OVERHEAD) break;

                var conv = BitConverter.ToUInt32(data, offset);
                if (conv != _conv) return -1;

                var cmd = data[offset + 4];
                var frg = data[offset + 5];
                var wnd = BitConverter.ToUInt16(data, offset + 6);
                var ts = BitConverter.ToUInt32(data, offset + 8);
                var sn = BitConverter.ToUInt32(data, offset + 12);
                var una = BitConverter.ToUInt32(data, offset + 16);
                var len = BitConverter.ToInt32(data, offset + 20);

                offset += IKCP_OVERHEAD;
                size -= IKCP_OVERHEAD;

                if (size < len) return -2;

                if (cmd != IKCP_CMD_PUSH && cmd != IKCP_CMD_ACK && cmd != IKCP_CMD_WASK && cmd != IKCP_CMD_WINS)
                    return -3;

                _rmt_wnd = wnd;
                ParseUna(una);
                ShrinkBuf();

                if (cmd == IKCP_CMD_ACK)
                {
                    if (_current >= ts)
                    {
                        UpdateAck((int)(_current - ts));
                    }
                    ParseAck(sn);
                    ShrinkBuf();
                }
                else if (cmd == IKCP_CMD_PUSH)
                {
                    if (sn < _rcv_nxt + _rcv_wnd)
                    {
                        _acklist.Add(sn);
                        _acklist.Add(ts);

                        if (sn >= _rcv_nxt)
                        {
                            var seg = new Segment
                            {
                                conv = conv,
                                cmd = cmd,
                                frg = frg,
                                wnd = wnd,
                                ts = ts,
                                sn = sn,
                                una = una,
                                data = new byte[len]
                            };
                            if (len > 0)
                                Buffer.BlockCopy(data, offset, seg.data, 0, len);
                            ParseData(seg);
                        }
                    }
                }
                else if (cmd == IKCP_CMD_WASK)
                {
                    _probe |= IKCP_ASK_TELL;
                }

                offset += len;
                size -= len;
            }

            if (_snd_una > oldUna && _cwnd < _rmt_wnd)
            {
                var mss = _mss;
                if (_cwnd < _ssthresh)
                {
                    _cwnd++;
                    _incr += mss;
                }
                else
                {
                    if (_incr < mss) _incr = mss;
                    _incr += (mss * mss) / _incr + (mss / 16);
                    if ((_cwnd + 1) * mss <= _incr)
                    {
                        _cwnd = (_incr + mss - 1) / ((mss > 0) ? mss : 1);
                    }
                }
                if (_cwnd > _rmt_wnd)
                {
                    _cwnd = _rmt_wnd;
                    _incr = _rmt_wnd * mss;
                }
            }

            return 0;
        }

        private void ParseUna(uint una)
        {
            for (var i = _snd_buf.Count - 1; i >= 0; i--)
            {
                if (_snd_buf[i].sn < una)
                    _snd_buf.RemoveAt(i);
            }
        }

        private void ParseAck(uint sn)
        {
            if (sn < _snd_una || sn >= _snd_nxt) return;
            for (var i = _snd_buf.Count - 1; i >= 0; i--)
            {
                if (_snd_buf[i].sn == sn)
                {
                    _snd_buf.RemoveAt(i);
                    break;
                }
            }
        }

        private void ParseData(Segment newseg)
        {
            var sn = newseg.sn;
            if (sn >= _rcv_nxt + _rcv_wnd || sn < _rcv_nxt) return;

            var insertIdx = _rcv_buf.Count;
            var repeat = false;
            for (var i = _rcv_buf.Count - 1; i >= 0; i--)
            {
                if (_rcv_buf[i].sn == sn)
                {
                    repeat = true;
                    break;
                }
                if (sn > _rcv_buf[i].sn)
                {
                    insertIdx = i + 1;
                    break;
                }
                insertIdx = i;
            }

            if (!repeat)
                _rcv_buf.Insert(insertIdx, newseg);

            while (_rcv_buf.Count > 0)
            {
                var seg = _rcv_buf[0];
                if (seg.sn == _rcv_nxt && _rcv_queue.Count < _rcv_wnd)
                {
                    _rcv_buf.RemoveAt(0);
                    _rcv_queue.Add(seg);
                    _rcv_nxt++;
                }
                else
                {
                    break;
                }
            }
        }

        private void ShrinkBuf()
        {
            _snd_una = _snd_buf.Count > 0 ? _snd_buf[0].sn : _snd_nxt;
        }

        private void UpdateAck(int rtt)
        {
            if (_rx_srtt == 0)
            {
                _rx_srtt = rtt;
                _rx_rttval = rtt / 2;
            }
            else
            {
                var delta = rtt - _rx_srtt;
                if (delta < 0) delta = -delta;
                _rx_rttval = (3 * _rx_rttval + delta) / 4;
                _rx_srtt = (7 * _rx_srtt + rtt) / 8;
                if (_rx_srtt < 1) _rx_srtt = 1;
            }
            var rto = _rx_srtt + Math.Max((int)_interval, 4 * _rx_rttval);
            _rx_rto = Math.Clamp(rto, _rx_minrto, IKCP_RTO_MAX);
        }

        public void Update(uint current)
        {
            _current = current;
            if (_updated == 0)
            {
                _updated = 1;
                _ts_flush = _current;
            }

            var slap = (int)(_current - _ts_flush);
            if (slap >= 10000 || slap < -10000)
            {
                _ts_flush = _current;
                slap = 0;
            }

            if (slap >= 0)
            {
                _ts_flush += _interval;
                if (_current >= _ts_flush)
                    _ts_flush = _current + _interval;
                Flush();
            }
        }

        private void Flush()
        {
            if (_updated == 0) return;

            var seg = new Segment { conv = _conv, cmd = IKCP_CMD_ACK, wnd = (uint)(_rcv_wnd - _rcv_queue.Count), una = _rcv_nxt };
            var offset = 0;

            void MakeSpace(int space)
            {
                if (offset + space > (int)_mtu)
                {
                    _output(_buffer, offset);
                    offset = 0;
                }
            }

            void FlushBuffer()
            {
                if (offset > 0)
                {
                    _output(_buffer, offset);
                    offset = 0;
                }
            }

            for (var i = 0; i < _acklist.Count; i += 2)
            {
                MakeSpace(IKCP_OVERHEAD);
                seg.sn = _acklist[i];
                seg.ts = _acklist[i + 1];
                offset += EncodeSegment(_buffer, offset, seg);
            }
            _acklist.Clear();

            if (_rmt_wnd == 0)
            {
                if (_probe_wait == 0)
                {
                    _probe_wait = IKCP_PROBE_INIT;
                    _ts_probe = _current + _probe_wait;
                }
                else if (_current >= _ts_probe)
                {
                    if (_probe_wait < IKCP_PROBE_INIT) _probe_wait = IKCP_PROBE_INIT;
                    _probe_wait += _probe_wait / 2;
                    if (_probe_wait > IKCP_PROBE_LIMIT) _probe_wait = IKCP_PROBE_LIMIT;
                    _ts_probe = _current + _probe_wait;
                    _probe |= IKCP_ASK_SEND;
                }
            }
            else
            {
                _ts_probe = 0;
                _probe_wait = 0;
            }

            if ((_probe & IKCP_ASK_SEND) != 0)
            {
                seg.cmd = IKCP_CMD_WASK;
                MakeSpace(IKCP_OVERHEAD);
                offset += EncodeSegment(_buffer, offset, seg);
            }

            if ((_probe & IKCP_ASK_TELL) != 0)
            {
                seg.cmd = IKCP_CMD_WINS;
                MakeSpace(IKCP_OVERHEAD);
                offset += EncodeSegment(_buffer, offset, seg);
            }
            _probe = 0;

            var cwnd = Math.Min(_snd_wnd, _rmt_wnd);
            if (_nocwnd == 0) cwnd = Math.Min(_cwnd, cwnd);

            while (_snd_nxt < _snd_una + cwnd && _snd_queue.Count > 0)
            {
                var newseg = _snd_queue[0];
                _snd_queue.RemoveAt(0);
                newseg.conv = _conv;
                newseg.cmd = IKCP_CMD_PUSH;
                newseg.wnd = seg.wnd;
                newseg.ts = _current;
                newseg.sn = _snd_nxt++;
                newseg.una = _rcv_nxt;
                newseg.resendts = _current;
                newseg.rto = (uint)_rx_rto;
                newseg.fastack = 0;
                newseg.xmit = 0;
                _snd_buf.Add(newseg);
            }

            var resent = _fastresend > 0 ? (uint)_fastresend : 0xffffffff;
            var rtomin = _nodelay == 0 ? (uint)(_rx_rto >> 3) : 0;
            var change = 0;
            var lost = 0;

            foreach (var segment in _snd_buf)
            {
                var needsend = false;
                if (segment.xmit == 0)
                {
                    needsend = true;
                    segment.xmit++;
                    segment.rto = (uint)_rx_rto;
                    segment.resendts = _current + segment.rto + rtomin;
                }
                else if (_current >= segment.resendts)
                {
                    needsend = true;
                    segment.xmit++;
                    _xmit++;
                    segment.rto += _nodelay == 0 ? (uint)_rx_rto : (uint)(_rx_rto / 2);
                    segment.resendts = _current + segment.rto;
                    lost = 1;
                }
                else if (segment.fastack >= resent)
                {
                    if (segment.xmit <= _fastlimit || _fastlimit <= 0)
                    {
                        needsend = true;
                        segment.xmit++;
                        segment.fastack = 0;
                        segment.resendts = _current + segment.rto;
                        change++;
                    }
                }

                if (needsend)
                {
                    segment.ts = _current;
                    segment.wnd = seg.wnd;
                    segment.una = _rcv_nxt;

                    var need = IKCP_OVERHEAD + segment.data.Length;
                    MakeSpace(need);
                    offset += EncodeSegment(_buffer, offset, segment);
                    if (segment.data.Length > 0)
                    {
                        Buffer.BlockCopy(segment.data, 0, _buffer, offset, segment.data.Length);
                        offset += segment.data.Length;
                    }

                    if (segment.xmit >= _dead_link)
                    {
                        _state = 0xffffffff;
                    }
                }
            }

            FlushBuffer();

            if (change != 0)
            {
                var inflight = _snd_nxt - _snd_una;
                _ssthresh = inflight / 2;
                if (_ssthresh < IKCP_THRESH_MIN) _ssthresh = IKCP_THRESH_MIN;
                _cwnd = _ssthresh + resent;
                _incr = _cwnd * _mss;
            }

            if (lost != 0)
            {
                _ssthresh = cwnd / 2;
                if (_ssthresh < IKCP_THRESH_MIN) _ssthresh = IKCP_THRESH_MIN;
                _cwnd = 1;
                _incr = _mss;
            }

            if (_cwnd < 1)
            {
                _cwnd = 1;
                _incr = _mss;
            }
        }

        private int EncodeSegment(byte[] buffer, int offset, Segment seg)
        {
            var ptr = offset;
            Buffer.BlockCopy(BitConverter.GetBytes(seg.conv), 0, buffer, ptr, 4); ptr += 4;
            buffer[ptr++] = (byte)seg.cmd;
            buffer[ptr++] = (byte)seg.frg;
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)seg.wnd), 0, buffer, ptr, 2); ptr += 2;
            Buffer.BlockCopy(BitConverter.GetBytes(seg.ts), 0, buffer, ptr, 4); ptr += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(seg.sn), 0, buffer, ptr, 4); ptr += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(seg.una), 0, buffer, ptr, 4); ptr += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(seg.data?.Length ?? 0), 0, buffer, ptr, 4); ptr += 4;
            return ptr - offset;
        }

        public int PeekSize()
        {
            if (_rcv_queue.Count == 0) return -1;
            var seg = _rcv_queue[0];
            if (seg.frg == 0) return seg.data.Length;

            if (_rcv_queue.Count < seg.frg + 1) return -1;

            var length = 0;
            foreach (var s in _rcv_queue)
            {
                length += s.data.Length;
                if (s.frg == 0) break;
            }
            return length;
        }

        public int Recv(byte[] buffer, int offset, int len)
        {
            if (_rcv_queue.Count == 0) return -1;

            var peeksize = PeekSize();
            if (peeksize < 0) return -2;
            if (peeksize > len) return -3;

            var recover = _rcv_queue.Count >= _rcv_wnd;
            var count = 0;
            var length = 0;

            foreach (var seg in _rcv_queue)
            {
                Buffer.BlockCopy(seg.data, 0, buffer, offset, seg.data.Length);
                offset += seg.data.Length;
                length += seg.data.Length;
                count++;
                if (seg.frg == 0) break;
            }

            if (count > 0)
                _rcv_queue.RemoveRange(0, count);

            while (_rcv_buf.Count > 0)
            {
                var seg = _rcv_buf[0];
                if (seg.sn == _rcv_nxt && _rcv_queue.Count < _rcv_wnd)
                {
                    _rcv_buf.RemoveAt(0);
                    _rcv_queue.Add(seg);
                    _rcv_nxt++;
                }
                else
                {
                    break;
                }
            }

            if (_rcv_queue.Count < _rcv_wnd && recover)
            {
                _probe |= IKCP_ASK_TELL;
            }

            return length;
        }
    }
}
