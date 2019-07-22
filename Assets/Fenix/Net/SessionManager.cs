using System;
using System.Collections.Generic;

namespace Fenix.Net
{
    using UnityEngine;
    using Fenix.IO;

    public abstract class MessageManager : MonoBehaviour
    {
        public Action onConnecting;
        public Action onConnected;
        public Action onClosed;
        public Action<ByteBuffer> onReceived;

        protected Connection _conn;

        public ConnectionError error
        {
            get { return _conn != null ? _conn.error : ConnectionError.ConnectionError; }
        }

        public bool IsConnecting
        {
            get { return _conn != null && _conn.state == ConnectionState.Connecting; }
        }

        void Awake()
        {
            OnInitialize();
        }

        protected virtual void Update()
        {
            if (_conn != null)
            {
                _conn.OnUpdate();
            }
        }

        void OnDestroy()
        {
            onConnecting = null;
            onConnected = null;
            onClosed = null;
            onReceived = null;
            Close();
        }

        public void Close()
        {
            if (_conn != null)
            {
                _conn.Close();
            }
        }

        protected virtual void Send(ByteBuffer buffer)
        {
            _conn.Send(buffer);
        }

        protected virtual void OnInitialize()
        {
            _conn.Connecting = OnConnectionConnecting;
            _conn.Connected = OnConnectionConnected;
            _conn.Closed = OnConnectionClosed;
            _conn.SetLogHandler(OnConnectionLog);
            _conn.SetEndPointPacketHandler(OnConnectionPacketReceived);
        }

        public void Connect(string address, int port)
        {
            System.Net.IPAddress ipAddress;
            if (!System.Net.IPAddress.TryParse(address, out ipAddress))
            {
                _conn.SetRemoteEndPoint(null);
                OnConnectionClosed();
                return;
            }
            _conn.SetRemoteEndPoint(new System.Net.IPEndPoint(ipAddress, port));
        }

        protected abstract void OnConnectionPacketReceived(ByteBuffer buffer);

        private void OnConnectionConnecting()
        {
            if (onConnecting != null)
                onConnecting();
        }

        private void OnConnectionConnected()
        {
            if (onConnected != null)
                onConnected();
        }

        private void OnConnectionClosed()
        {
            if (onClosed != null)
                onClosed();
        }

        private void OnConnectionLog(string text)
        {
            Debug.Log(text);
        }

        // 发送数据，无返回。不可靠连接时不保证接收端接收成功。
        public void Post(short msg_id, byte[] msg)
        {
            this.Send(0, msg_id, msg);
        }

        public void Send(int session_id, short msg_id, byte[] msg)
        {
            var buffer = ByteBufferPooledAllocator.Default.Alloc(1024);

            try
            {
                buffer.WriteInt32(sizeof(int) + sizeof(int) + sizeof(short) + msg.Length);
                buffer.WriteInt16(msg_id);
                buffer.WriteInt32(session_id);
                buffer.WriteBytes(msg);
                this.Send(buffer);
            }
            finally
            {
                if (buffer != null)
                {
                    buffer.Release();
                }
            }
        }

        protected void OnReceived(ByteBuffer byteBuffer)
        {
            if (onReceived != null)
            {
                onReceived(byteBuffer);
            }
        }
    }

    public class StreamSessionManager : MessageManager
    {
        private StreamPacketizer _packetizer;

        protected override void OnInitialize()
        {
            _packetizer = new StreamPacketizer();
            _conn = new TcpConnection();
            base.OnInitialize();
        }

        protected override void OnConnectionPacketReceived(ByteBuffer recv_buffer)
        {
            var packets = _packetizer.InputBytes(recv_buffer.data, 0, recv_buffer.readableBytes);

            if (packets > 0)
            {
                while (true)
                {
                    var packet = _packetizer.CreatePacket();
                    if (packet == null)
                    {
                        break;
                    }
                    try
                    {
                        OnReceived(packet);
                    }
                    finally
                    {
                        packet.Release();
                    }
                }
            }
        }
    }

    public class KcpSessionManager : StreamSessionManager
    {
        private KCP _kcp;
        private static readonly long epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        protected override void OnInitialize()
        {
            _kcp = new KCP(0, _OnKcpSend);
            _conn = new UdpConnection(0);
            base.OnInitialize();
        }

        protected override void Send(ByteBuffer buffer)
        {
            _kcp.Send(buffer.data, buffer.readableBytes);
        }

        protected override void Update()
        {
            var now = (DateTime.UtcNow.Ticks - epoch) / 10000;

            base.Update();
            _kcp.Update((uint)now); //TODO: 改成通过check进行schedule的方式可以节省cpu
        }

        protected override void OnConnectionPacketReceived(ByteBuffer recv_buffer)
        {
            _kcp.Input(recv_buffer.data, recv_buffer.readableBytes);

            var length = _kcp.PeekSize();
            if (length > 0)
            {
                var buffer = ByteBufferPooledAllocator.Default.Alloc(length);

                try
                {
                    var size = _kcp.Recv(buffer.data); //接受数据
                    buffer._SetPosition(size); // 不想改kcp的实现细节，所以这里简单地强制改buffer长度标记
                    base.OnConnectionPacketReceived(buffer);
                }
                finally
                {
                    buffer.Release();
                }
            }
        }

        private void _OnKcpSend(byte[] data, int length)
        {
            var buffer = ByteBufferPooledAllocator.Default.Alloc(length);
            _conn.Send(buffer);
            buffer.Release();
        }
    }
}