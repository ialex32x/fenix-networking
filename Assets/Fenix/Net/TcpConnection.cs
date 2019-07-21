using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace Fenix.Net {
    using UnityEngine;

    public class TcpConnection : Connection {
        public TcpConnection() {
            this._state = ConnectionState.Closed;
        }
        
        public TcpConnection(IPEndPoint remoteEndPoint) {
            this._remoteEndPoint = remoteEndPoint;
            this._state = ConnectionState.Closed;
        }

        // tcp method
        public override void BeginConnect() {
            try {
                Close();
                _sock = new Socket(_remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _state = ConnectionState.Connecting;
                _sock.BeginConnect(_remoteEndPoint, _OnBeginConnect, _sock);
                // _Log(string.Format("正在连接 {0}", _remoteEndPoint));
                if (Connecting != null)
                    Connecting();
            } catch (Exception e) {
                _error = ConnectionError.ConnectionError;
                _state = ConnectionState.Closing;
                _ThreadedLog(e.ToString());
            }
        }

        // tcp method
        private void _OnBeginConnect(IAsyncResult ar) {
            try {
                if (_state == ConnectionState.Closing || _state == ConnectionState.Closed) {
                    //_ThreadedLog("Connect 回调时已经请求关闭socket");
                    return;
                }
                var sock = (Socket)ar.AsyncState;
                sock.EndConnect(ar);

                if (sock.Connected) {
                    _state = ConnectionState.Connected;
                } else {
                    _ThreadedLog("fail to connect");
                    _error = ConnectionError.ConnectionError;
                    _state = ConnectionState.Closing;
                }
            } catch (SocketException socketException)  {
                // var err = socketException.SocketErrorCode;
                _error = FromSocketError(socketException.SocketErrorCode);
                _state = ConnectionState.Closing;
                _ThreadedLog(socketException.ToString());
            } catch (Exception err) {
                _error = ConnectionError.ConnectionError;
                _state = ConnectionState.Closing;
                _ThreadedLog(err.ToString());
            }
        } // end _OnBeginConnect
    }
}
