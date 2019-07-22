using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace Fenix.IO
{
    public class ByteBufferPooledAllocator : ByteBufferAllocator
    {
#if UNITY_EDITOR
        public static readonly ByteBufferPooledAllocator Default = new ByteBufferPooledAllocator(16, ByteBufferAllocator.DEFAULT_SIZE, ByteBufferAllocator.DEFAULT_MAX_CAPACITY, true);
#else
        public static readonly ByteBufferPooledAllocator Default = new ByteBufferPooledAllocator(16, ByteBufferAllocator.DEFAULT_SIZE, ByteBufferAllocator.DEFAULT_MAX_CAPACITY, false);
#endif

        private int _maxCapacity;
        private object __mutex = new object();
        private List<ByteBuffer> __freelist;

        // 预分配缓冲池
        public ByteBufferPooledAllocator(int count, int initialCapacity, int maxCapacity, bool traceMemoryLeak)
        {
            _maxCapacity = maxCapacity;
            _traceMemoryLeak = traceMemoryLeak;

            lock (__mutex)
            {
                __freelist = new List<ByteBuffer>(count);
                while (count-- > 0)
                {
                    __freelist.Add(new ByteBuffer(initialCapacity, maxCapacity, this));
                }
            }
        }

        // 返回一个由对象池分配的 ByteBuffer 对象, 大小至少为 size
        // Alloc、Release 调用为线程安全的，其余操作需要调用者自己保证线程安全性
        public override ByteBuffer Alloc(int size)
        {
            lock (__mutex)
            {
                var count = __freelist.Count;
                if (count > 0)
                {
                    var free = __freelist[count - 1];
                    __freelist.RemoveAt(count - 1);
                    free.Retain();
                    return free;
                }
            }
            return new ByteBuffer(size, _maxCapacity, this).Retain();
        }

        public override void Recycle(ByteBuffer byteBuffer)
        {
            lock (__mutex)
            {
                __freelist.Add(byteBuffer);
            }
        }
    }
}
