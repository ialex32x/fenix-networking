using System;
using System.Collections.Generic;

namespace Fenix.Net
{
    public struct DataBlock
    {
        public static DataBlock Empty;

        static DataBlock()
        {
            Empty = new DataBlock();
        }

        private bool _dump;
        private byte[] _data;
        private int _readOffset;

        public DataBlock(byte[] data, bool dump)
        {
            _dump = dump;
            _data = data;
            _readOffset = 0;
        }

        public bool IsEmpty()
        {
            return _data == null;
        }

        public byte[] data { get { return _data; } }

        public bool isDump { get { return _dump; } }

        public int size { get { return _data.Length - _readOffset; } }

        public int offset
        {
            get { return _readOffset; }
            set { _readOffset = value; }
        }
    }
}
