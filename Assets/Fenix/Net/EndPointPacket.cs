using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;

namespace Fenix.Net {
    using Fenix.IO;

    public delegate void EndPointPacketHandler(ByteBuffer buffer);
}
