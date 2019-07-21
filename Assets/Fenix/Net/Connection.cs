using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace Fenix.Net
{
    using UnityEngine;
    using Fenix.IO;

    public delegate void LogHandler(string msg);

    public abstract class Connection 
    {
        public Action Connected;
        public Action Connecting;
        public Action Closed;

        private byte[] _RECV_BUFFER;
        protected IPEndPoint _remoteEndPoint;
        protected Socket _sock;
        private LogHandler _logHandler;

        private EndPointPacketHandler _endPointPacketHandler;
        private List<ByteBuffer> _endPointPacketsCopy = new List<ByteBuffer>();

        public bool debugMode;

        protected ulong _sentBytes;
        protected ulong _recvBytes;

        protected int _unused;
        protected ByteBufferQueue _sendQueue = new ByteBufferQueue();
        protected ByteBufferQueue _recvQueue = new ByteBufferQueue();
        protected ConnectionState _state;
        protected ConnectionError _error;

        private AutoResetEvent _sendEvent = new AutoResetEvent(false);
        private Thread _sendThread;
        private Thread _recvThread;

        public ConnectionState state { get { return _state; } }

        public ConnectionError error { get { return _error; } }

        public ulong sentBytes { get { return _sentBytes; } }
        public ulong recvBytes { get { return _recvBytes; } }

        public bool isRunning { get { return _state == ConnectionState.Running; } }

        public Socket socket { get { return _sock; } }

        public IPEndPoint localEndPoint { get { return _sock != null ? (IPEndPoint)_sock.LocalEndPoint : null; } }

        public IPEndPoint remoteEndPoint { get { return _remoteEndPoint; } }

        protected Connection() {}

        public void SetLogHandler(LogHandler logHandler) {
            this._logHandler = logHandler;
        }

        public void SetEndPointPacketHandler(EndPointPacketHandler handler) {
            _endPointPacketHandler = handler;
        }

        protected ConnectionError FromSocketError(SocketError error) {
            switch (error) {
                case SocketError.NetworkUnreachable:
                case SocketError.HostUnreachable: return ConnectionError.Unreachable;
                case SocketError.ConnectionRefused: return ConnectionError.Refused;
                case SocketError.InvalidArgument: return ConnectionError.Unreachable;
                case SocketError.Success: return ConnectionError.None;
                default: return ConnectionError.ConnectionError;
            }
        }

        // 注册自动调用， 或者直接手工调用
        // 检查内部状态，检查组包状态等
        public void OnUpdate()
        {
            switch (_state)
            {
                case ConnectionState.Running: PeekMessage(); return;
                case ConnectionState.Connected: OnConnected(); return;
                case ConnectionState.Closing: Close(); return;
                case ConnectionState.Closed: return;
                default: return;
            }
        }

        protected void PeekMessage() {
            if (_recvQueue.TransferTo(_endPointPacketsCopy) > 0) {
                try {
                    if (_endPointPacketHandler != null) {
                        for (int i = 0, size = _endPointPacketsCopy.Count; i < size; ++i) {
                            var buffer = _endPointPacketsCopy[i];

                            _endPointPacketHandler(buffer);
                        }
                    }
                } finally {
                    for (int i = 0, size = _endPointPacketsCopy.Count; i < size; ++i) {
                        var buffer = _endPointPacketsCopy[i];
                        
                        buffer.Release();
                    }
                    _endPointPacketsCopy.Clear();
                }
            }
        }

        public void SetRemoteEndPoint(IPEndPoint remoteEndPoint) {
            try {
                if (remoteEndPoint != _remoteEndPoint) {
                    Close();
                    _remoteEndPoint = remoteEndPoint;
                    if (_remoteEndPoint != null) {
                        BeginConnect();
                    } else {
                        _error = ConnectionError.Unreachable;
                    }
                }
            } catch (Exception e) {
                _error = ConnectionError.ConnectionError;
                _state = ConnectionState.Closing;
                _ThreadedLog(e.ToString());
            }
        }

        public abstract void BeginConnect();

        protected virtual void OnConnected() {
            _error = ConnectionError.None;
            _state = ConnectionState.Running;
            _RECV_BUFFER = new byte[_sock.ReceiveBufferSize];

            _sendThread = new Thread(new ThreadStart(_SendThreadEntry));
            _recvThread = new Thread(new ThreadStart(_RecvThreadEntry));
            _sendThread.Start();
            _recvThread.Start();
            // Debug.Log("Connection.SetRunning");
            if (Connected != null) {
                Connected();
            }
        }

        public void Send(ByteBuffer buffer) {
            if (buffer != null) {
                buffer.Retain();
                _sendQueue.PushBack(buffer);
                if (!_sendEvent.Set()) {
                    Debug.LogError("SendEvent.Set() failed");
                }
            }
        }

        private void _SendThreadEntry() {
            ByteBuffer buffer = null;
            try {
                while (true) {
                    // wait for queue
                    _sendEvent.WaitOne();

                    buffer = _sendQueue.Pop();
                    while (true) {
                        if (_state == ConnectionState.Closing || _state == ConnectionState.Closed) {
                            //_ThreadedLog("Send 回调时已经请求关闭socket");
                            if (buffer != null) {
                                _sendQueue.PushFront(buffer);
                                buffer = null;
                            }
                            return;
                        }

                        if (buffer != null) {
                            // 这里改成发送到 _remoteEndPoint， 如果需要可以通过UDP指定多个发送端，则需要改造 SendQueue，需要管理一个携带EndPoint信息的ByteBuffer队列
                            //_ThreadedLog(string.Format("[socket] sendto {0} {1}", buffer, _remoteEndPoint));
                            var sent = _sock.SendTo(buffer.data, buffer.readerIndex, buffer.readableBytes, SocketFlags.None, _remoteEndPoint);

                            if (sent == 0) {
                                _error = ConnectionError.Aborted;
                                _state = ConnectionState.Closing;
                                _ThreadedLog(" !! OnSend: 远程主机强行关闭连接");
                                return;
                            }

                            if (debugMode) {
                                UnityEngine.Debug.LogFormat("[socket] sent {0}/{1} bytes (total: {2})", sent, buffer.readableBytes, _sentBytes);
                            }

                            _sentBytes += (ulong)sent;
                            // Debug.LogFormat("::sent {0}", sent);

                            buffer.ReadBytes(sent);
                            if (buffer.readableBytes > 0) {
                                // 未完成，重发
                            } else {
                                buffer.Release();
                                buffer = null;
                                buffer = _sendQueue.Pop();
                            }
                        } else {
                            break;
                        }
                    }
                }
            } catch (SocketException socketException) {
                if (_state == ConnectionState.Running) {
                    _error = FromSocketError(socketException.SocketErrorCode);
                    _state = ConnectionState.Closing;
                    _ThreadedLog(socketException.ToString());
                }
            } catch (Exception err) {
                if (_state == ConnectionState.Running) {
                    _error = ConnectionError.ConnectionError;
                    _state = ConnectionState.Closing;
                    _ThreadedLog(err.ToString());
                }
            } finally {
                if (buffer != null) {
                    buffer.Release();
                    buffer = null;
                }
            }
        }

        private void _RecvThreadEntry() {
            ByteBuffer buffer = null;
            try {
                while (true) {
                    if (_state == ConnectionState.Closing || _state == ConnectionState.Closed || _sock == null) {
                        //_ThreadedLog("Recv 回调时已经请求关闭socket");
                        return;
                    }

                    var remoteEndPoint = (EndPoint)_remoteEndPoint;
                    var recv = _sock.ReceiveFrom(_RECV_BUFFER, 0, _RECV_BUFFER.Length, SocketFlags.None, ref remoteEndPoint);

                    if (recv > 0) {
                        _recvBytes += (ulong)recv;
                        // Debug.LogFormat("::recv {0}", recv);
                        
                        buffer = ByteBufferPooledAllocator.Default.Alloc(recv);
                        buffer.WriteBytes(_RECV_BUFFER, 0, recv);
                        _recvQueue.PushBack(buffer);
                        buffer = null;
                    } else { // recv <= 0
                        if (_state == ConnectionState.Running) {
                            _error = ConnectionError.Aborted;
                            _state = ConnectionState.Closing;
                            _ThreadedLog(" !! OnRecv: 远程主机强行关闭连接");
                            return;
                        }
                    } // end if recv > 0
                } // end while true
            } catch (SocketException socketException) {
                if (_state == ConnectionState.Running) {
                    _error = FromSocketError(socketException.SocketErrorCode);
                    _state = ConnectionState.Closing;
                    _ThreadedLog(socketException.ToString());
                }
            } catch (Exception err) {
                if (_state == ConnectionState.Running) {
                    _error = ConnectionError.ConnectionError;
                    _state = ConnectionState.Closing;
                    _ThreadedLog(err.ToString());
                }
            } finally {
                if (buffer != null) {
                    buffer.Release();
                    buffer = null;
                }
            }
        }

        public override string ToString() {
            return _sock != null && _sock.RemoteEndPoint != null ? _sock.RemoteEndPoint.ToString() : "Connection";
        }

        protected void _ThreadedLog(string msg)
        {
            if (_logHandler != null) {
                _logHandler(msg);
            }
        }

        // 关闭连接 (不会自动情况发送队列, 但是清空接受数据缓冲)
        public void Close() {
            if (_state == ConnectionState.Closed) {
                return;
            }
            _state = ConnectionState.Closed;
            if (_sendThread != null) {
                // _sendThread.Abort();
                _sendThread = null;
            }
            if (_recvThread != null) {
                // _recvThread.Abort();
                _recvThread = null;
            }
            if (_sock != null) {
                if (_sock.Connected) {
                    try {
                        if (_sock.ProtocolType == ProtocolType.Tcp) {
                            _sock.Shutdown(SocketShutdown.Both);
                        }
                        // _sock.Disconnect(false);
                    } catch (Exception exception) {
                        Debug.LogError(exception);
                    }
                }
                _sock.Close();
                _sock = null;
            }
            do {
                var buffer = _recvQueue.Pop();
                if (buffer == null) {
                    break;
                }
                buffer.Release();
            } while (true);
            _recvQueue.Clear();
            if (Closed != null) {
                Closed();
            }
            _error = ConnectionError.None;
        }

        // 清空发送队列
        public void Clear() {
            do {
                var buffer = _sendQueue.Pop();
                if (buffer == null) {
                    break;
                }
                buffer.Release();
            } while (true);
            _sendQueue.Clear();
        }

        public void SetUpdater(MonoBehaviour mb) {
            mb.StartCoroutine(_AutoUpdate());
        }

        private IEnumerator _AutoUpdate()
        {
            while (true)
            {
                OnUpdate();
                yield return 1;
            }
        }
    }
}
