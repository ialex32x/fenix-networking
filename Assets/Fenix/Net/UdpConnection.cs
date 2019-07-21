using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace Fenix.Net {
    using UnityEngine;

    public class UdpConnection : Connection {
        // 创建udp连接, port可以指定端口 (0 表示由系统自动分配)
        public UdpConnection(int localPort) {
            var localEndPoint = new IPEndPoint(IPAddress.Any, localPort);
            var socket = new Socket(localEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(localEndPoint);

            var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            this._sock = socket;
            this._remoteEndPoint = remoteEndPoint;
            this._state = ConnectionState.Connected;
        }

        public UdpConnection(int localPort, string remoteAddress, int remotePort) {
            IPAddress ipAddress;
            if (string.IsNullOrEmpty(remoteAddress) || !IPAddress.TryParse(remoteAddress, out ipAddress)) {
                throw new Exception();
            }

            var localEndPoint = new IPEndPoint(IPAddress.Any, localPort);
            var socket = new Socket(localEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(localEndPoint);

            var remoteEndPoint = new IPEndPoint(ipAddress, remotePort);
            this._sock = socket;
            this._remoteEndPoint = remoteEndPoint;
            this._state = ConnectionState.Connected;
        }

        public override void BeginConnect() {
            // UDP 无需显式连接
        } 
    }
}
