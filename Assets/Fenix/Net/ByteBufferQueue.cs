using System;
using System.Collections.Generic;

namespace Fenix.Net
{
    using Fenix.IO;
    
    public class ByteBufferQueue
    {
        private LinkedList<ByteBuffer> _sendQ = new LinkedList<ByteBuffer>();

        public int count
        {
            get
            {
                lock (_sendQ)
                {
                    return _sendQ.Count;
                }
            }
        }

        public int TransferTo(List<ByteBuffer> copylist)
        {
            var count = 0;
            lock (_sendQ)
            {
                var item = _sendQ.First;
                while (item != null)
                {
                    copylist.Add(item.Value);
                    ++count;
                    item = item.Next;
                }
                _sendQ.Clear();
            }
            return count;
        }

        public ByteBuffer Peek()
        {
            lock (_sendQ)
            {
                if (_sendQ.Count != 0)
                {
                    return _sendQ.First.Value;
                }
            }
            return null;
        }

        public ByteBuffer Pop()
        {
            ByteBuffer safeGet = null;

            lock (_sendQ)
            {
                if (_sendQ.Count != 0)
                {
                    safeGet = _sendQ.First.Value;
                    _sendQ.RemoveFirst();
                }
            }
            return safeGet;
        }

        public void Clear()
        {
            lock (_sendQ)
            {
                _sendQ.Clear();
            }
        }

        public void PushFront(ByteBuffer d)
        {
            lock (_sendQ)
            {
                _sendQ.AddFirst(d);
            }
        }

        public void PushBack(ByteBuffer d)
        {
            lock (_sendQ)
            {
                _sendQ.AddLast(d);
            }
        }
    }
}
