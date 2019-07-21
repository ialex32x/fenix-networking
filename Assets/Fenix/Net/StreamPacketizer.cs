using System;
using System.Collections.Generic;
using System.Net;

namespace Fenix.Net
{
    using LengthType = Int32;
    using System.IO;
    using Fenix.IO;

    // 非线程安全
    public class StreamPacketizer
    {
        public const int HeadSizeTranslate = sizeof(LengthType); // 长度信息 - Translate = 协议体长度
        public const int HeadSize = sizeof(LengthType);
        public const LengthType PacketSizeMax = 1024 * 512;

        public static LengthType ToLength(byte[] data, int index)
        {
            return (LengthType)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, index));
        }

        private LengthType _len;
        private MemoryStream _ms = new MemoryStream();
        private Queue<ByteBuffer> _packet = new Queue<ByteBuffer>(32);

        public void Clear()
        {
            _ms.Seek(0, SeekOrigin.Begin);
            _ms.SetLength(0);
            _len = default(LengthType);
            var packet = _packet.Dequeue();
            while (packet != null)
            {
                packet.Release();
                packet = _packet.Dequeue();
            }
            _packet.Clear();
        }

        public int InputBytes(byte[] inData, int inDataOffset, int inDataLenAll)
        {
            var inDataLenRelative = inDataLenAll - inDataOffset;
            // 现有数据 + 可加入数据的总长度
            var sizeInStream = _ms.Position + inDataLenRelative;

            if (_len == default(LengthType))
            {
                // 已经写入+可写入是否可以组成头部长度
                if (sizeInStream < HeadSize)
                {
                    _ms.Write(inData, inDataOffset, inDataLenRelative);
                    return 0;
                }
                else
                {
                    // 还需补充多少字节才能构成头部长度
                    var writeBytes = HeadSize - (int)_ms.Position;

                    if (writeBytes > 0)
                    {
                        _ms.Write(inData, inDataOffset, writeBytes);
                    }
                    _ms.Flush();
                    var fuck_data = _ms.ToArray();
                    var raw_size = ToLength(fuck_data, 0);

                    if (raw_size > PacketSizeMax)
                    {
                        throw new Exception("包长度错误");
                    }
                    // 剔除长度占用的部分(得到的长度包括长度自身), 然后从缓冲区丢弃
                    _len = raw_size - HeadSizeTranslate;
                    _ms.Seek(0, SeekOrigin.Begin);
                    _ms.SetLength(0);

                    // 得到原始数据流剩余数据
                    var leftSize = inDataLenRelative - writeBytes;

                    //UnityEngine.Debug.LogFormat("len: {0} leftSize: {1} writeBytes: {2}", _len, leftSize, writeBytes);
                    if (leftSize == 0)
                    {
                        // 如果服务器的包体有可能为空的, 注释掉该条件
                        return 0;
                    }

                    if (leftSize < _len)
                    {
                        // 剩余数据少于指定长度，直接写入即可
                        _ms.Write(inData, inDataOffset + writeBytes, leftSize);
                        return 0;
                    }

                    if (leftSize == _len)
                    {
                        // 剩余数据刚好等于指定长度，直接组包即可
                        _NewPacket(inData, inDataOffset + writeBytes);
                        return 1;
                    }

                    // 1、剩余数据大于当前packet长度，写入长度内的部分组包
                    // 2、递归调用InputBytes写入超出部分
                    _ms.Write(inData, inDataOffset + writeBytes, (int)_len);
                    writeBytes += (int)_len;
                    _NewPacket();
                    return 1 + InputBytes(inData, inDataOffset + writeBytes, inDataLenAll);
                }
            }
            else
            {
                // 如果总长度 少于 需要读取的长度，那么可以直接全部写入
                if (sizeInStream < _len)
                {
                    _ms.Write(inData, inDataOffset, inDataLenRelative);
                    return 0;
                }

                // 如果总长度 等于 需要读取的长度，那么全部写入，并且组包
                if (sizeInStream == _len)
                {
                    _ms.Write(inData, inDataOffset, inDataLenRelative);
                    _NewPacket();
                    return 1;
                }

                // 如果总长度 超过 需要读取的总长度，那么写入一部分
                var writeBytes = (int)_len - (int)_ms.Position;
                _ms.Write(inData, inDataOffset, writeBytes);
                _NewPacket();
                return 1 + InputBytes(inData, inDataOffset + writeBytes, inDataLenAll);
            }
        }

        private void _NewPacket(byte[] data, int offset)
        {
            var byteBuffer = ByteBufferPooledAllocator.Default.Alloc(_len);
            byteBuffer.WriteBytes(data, offset, _len);
            _packet.Enqueue(byteBuffer);
            _ms.Seek(0L, SeekOrigin.Begin);
            _ms.SetLength(0L);
            _len = default(LengthType);
        }

        private void _NewPacket() {
            _ms.Seek(0L, SeekOrigin.Begin);
            var size = (int)_ms.Length;
            var byteBuffer = ByteBufferPooledAllocator.Default.Alloc(size);
            byteBuffer.WriteBytes(_ms, size);

            _packet.Enqueue(byteBuffer);
            _ms.Seek(0L, SeekOrigin.Begin);
            _ms.SetLength(0L);
            _len = default(LengthType);
        }

        public ByteBuffer CreatePacket() {
            if (_packet.Count != 0) {
                ByteBuffer packet = _packet.Dequeue();
                return packet;
            }
            return null;
        }

        public static void _Dump(byte[] data) {
            _Dump("NONAME", data, 0, data.Length);
        }

        public static void _Dump(string name, byte[] data) {
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
