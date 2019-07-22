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
        private ByteBuffer _buffer;
        private Queue<ByteBuffer> _packet = new Queue<ByteBuffer>(32);

        public void Clear()
        {
            if (_buffer != null)
            {
                _buffer.Release();
                _buffer = null;
            }
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
                if (_buffer == null)
                {
                    _buffer = ByteBufferPooledAllocator.Default.Alloc(PacketSizeMax);
                }
                if (_length <= 0)
                {
                    var need = HeadSize - _buffer.writerIndex;
                    if (size < need)
                    {
                        _buffer.WriteBytes(data, offset, size);
                        return 0;
                    }
                    _buffer.WriteBytes(data, offset, need);
                    var left = size - need;
                    offset += need;
                    _length = _buffer.ReadInt32() - HeadSize;
                    _buffer.writerIndex = 0;
                    _buffer.readerIndex = 0;
                    return InputBytes(data, offset, left);
                }
                else
                {
                    var need = _length - (int)_buffer.writerIndex;
                    if (size < need)
                    {
                        _buffer.WriteBytes(data, offset, size);
                        return 0;
                    }
                    _buffer.WriteBytes(data, offset, need);
                    _NewPacket();
                    var left = size - need;
                    offset += need;
                    return 1 + InputBytes(data, offset, left);
                }
            }
            return 0;
        }

        private void _NewPacket()
        {
            _packet.Enqueue(_buffer);
            _buffer = null;
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
