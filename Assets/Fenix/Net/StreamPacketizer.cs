using System;
using System.Collections.Generic;
using System.Net;

namespace Fenix.Net
{
    using System.IO;
    using Fenix.IO;

    // 非线程安全
    public class StreamPacketizer
    {
        public const int HeadSizeTranslate = sizeof(Int32); // 长度信息 - Translate = 协议体长度
        public const int HeadSize = sizeof(Int32);
        public const Int32 PacketSizeMax = 1024 * 512;

        private Int32 _length;
        private MemoryStream _buffer = new MemoryStream();
        private Queue<ByteBuffer> _packet = new Queue<ByteBuffer>(32);

        public void Clear()
        {
            _buffer.Seek(0, SeekOrigin.Begin);
            _buffer.SetLength(0);
            _length = default(Int32);
            var packet = _packet.Dequeue();
            while (packet != null)
            {
                packet.Release();
                packet = _packet.Dequeue();
            }
            _packet.Clear();
        }

        public int InputBytes(byte[] data, int offset, int size)
        {
            if (size > 0)
            {
                if (_length < 0)
                {
                    var need = HeadSize - (int)_buffer.Position;
                    if (size < need)
                    {
                        _buffer.Write(data, offset, size);
                        return 0;
                    }
                    _buffer.Write(data, offset, need);
                    var left = size - need;
                    offset += need;
                    _length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(_buffer.ToArray(), 0)) - HeadSize;
                    _buffer.SetLength(0);
                    return InputBytes(data, offset, left);
                }
                else
                {
                    var need = _length - (int)_buffer.Position;
                    if (size < need)
                    {
                        _buffer.Write(data, offset, size);
                        return 0;
                    }
                    _buffer.Write(data, offset, need);
                    _NewPacket();
                    var left = size - need;
                    offset += need;
                    _length = -1;
                    _buffer.SetLength(0);
                    return 1 + InputBytes(data, offset, left);
                }
            }
            return 0;
        }

        private void _NewPacket()
        {
            _buffer.Seek(0L, SeekOrigin.Begin);
            var size = (int)_buffer.Length;
            var byteBuffer = ByteBufferPooledAllocator.Default.Alloc(size);
            byteBuffer.WriteBytes(_buffer, size);

            _packet.Enqueue(byteBuffer);
            _buffer.Seek(0L, SeekOrigin.Begin);
            _buffer.SetLength(0L);
            _length = default(Int32);
        }

        public ByteBuffer CreatePacket()
        {
            if (_packet.Count != 0)
            {
                ByteBuffer packet = _packet.Dequeue();
                return packet;
            }
            return null;
        }

        public static void _Dump(byte[] data)
        {
            _Dump("NONAME", data, 0, data.Length);
        }

        public static void _Dump(string name, byte[] data)
        {
            _Dump(name, data, 0, data.Length);
        }

        public static void _Dump(string title, byte[] data, int offset, int size)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendFormat("Dump ({0}) for {1}:", size - offset, title);
            sb.AppendLine();
            for (int i = offset; i < size; ++i)
            {
                sb.AppendFormat("0x{0:x2},", data[i]);
                sb.Append(' ');
            }
            sb.AppendLine();
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
