using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace Fenix.IO
{
    using UnityEngine;

    public static class ConvertUtils
    {

        public static byte[] GetBytes(long value)
        {
#if !MB_FUCKED_LONG_ORDER
            var data = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(value));
#else
            var v = (ulong)System.Net.IPAddress.HostToNetworkOrder(value);
            var data = BitConverter.GetBytes(((v >> 32) & 0x00000000ffffffff) | ((v << 32) & 0xffffffff00000000));
#endif
            return data;
        }

        public static byte[] GetBytes(ulong value)
        {
#if !MB_FUCKED_LONG_ORDER
            var data = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((long)value));
#else
            var v = (ulong)System.Net.IPAddress.HostToNetworkOrder((long)value);
            var data = BitConverter.GetBytes(((v >> 32) & 0x00000000ffffffff) | ((v << 32) & 0xffffffff00000000));
#endif
            return data;
        }

        public static long ReadInt64(byte[] data, int offset)
        {
#if !MB_FUCKED_LONG_ORDER
            var v = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt64(data, offset));
#else
            var uv = (ulong)System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt64(data, offset));
            var v = (long)(((uv >> 32) & 0x00000000ffffffff) | ((uv << 32) & 0xffffffff00000000));
#endif
            return v;
        }

        public static ulong ReadUInt64(byte[] data, int offset)
        {
#if !MB_FUCKED_LONG_ORDER
            var v = (ulong)System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt64(data, offset));
#else
            var uv = (ulong)System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt64(data, offset));
            var v = ((uv >> 32) & 0x00000000ffffffff) | ((uv << 32) & 0xffffffff00000000);
#endif
            return v;
        }
    }
}
