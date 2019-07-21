using System;
using System.Collections.Generic;

namespace Fenix.Net
{
    public class DataBlockQueue
    {
        private LinkedList<DataBlock> _sendQ = new LinkedList<DataBlock>();

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

        public DataBlock Peek()
        {
            lock (_sendQ)
            {
                if (_sendQ.Count != 0)
                    return _sendQ.First.Value;
                else
                    return DataBlock.Empty;
            }
        }

        public DataBlock Pop()
        {
            DataBlock safeGet = DataBlock.Empty;

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

        public void PushFront(DataBlock d)
        {
            lock (_sendQ)
            {
                _sendQ.AddFirst(d);
            }
        }

        public void PushBack(DataBlock d)
        {
            lock (_sendQ)
            {
                _sendQ.AddLast(d);
            }
        }
    }
}
