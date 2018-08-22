using System;
using System.Diagnostics;
using System.Threading;

namespace Bisync
{
    class CyclicBuffer 
    {

        private ManualResetEvent m_readEvent = new ManualResetEvent(false);
        private ManualResetEvent m_writeEvent = new ManualResetEvent(false);

        private int m_size;
        private int m_rd;
        private int m_wr;
        private byte[] m_data;
        private bool m_closed;


        public CyclicBuffer(int size)
        {
            Debug.Assert(size > 0);
            m_data = new byte[size];
        }


        public void Put(byte[] data, int offset, int size)
        {
            Put(data, offset, size, Timeout.Infinite);
        }

        public bool Put(byte[] data, int offset, int size,int timeout)
        {
            
            
            
            while (true) 
            {

                lock (this)
                {
                    CheckState();

                    if (size <= FreeSpace)
                    {
                        internalPut(data, offset, size);
                        m_readEvent.Set();

                        if (FreeSpace == 0)
                        {
                            m_writeEvent.Reset();
                        }
                        
                        return true;
                    }
                    else if (timeout == 0)
                    {
                        return false;
                    }
                }

                

                bool ret = m_writeEvent.WaitOne(timeout,false);

                if (timeout != Timeout.Infinite && !ret)
                {
                    return false;
                }

            }
        }


        private void internalPut(byte[] data, int offset, int size)
        {
            if (data == null)
                throw new ArgumentNullException("data can't be null");

            if (offset + size > data.Length)
                throw new ArgumentOutOfRangeException("offset + size greater than the input data size");

            if (size > FreeSpace)
                throw new OverflowException("The size of input data is bigger than the available space.");
            
            
            int freeLength = (m_data.Length - m_wr);
            int myLength = size;

            if (freeLength < myLength)
            {
                myLength = freeLength;
            }

            Buffer.BlockCopy(data, offset, m_data, m_wr, myLength);
           
            offset += myLength;
            myLength = (size - myLength);

            if (myLength != 0)
            {
                Buffer.BlockCopy(data, offset, m_data, 0, myLength);
            }


            AdvanceWritePtr(size);

        }


        public int Get(byte[] data, int offset, int size)
        {
            return Get(data, offset, size, Timeout.Infinite);
        }

        public int Get(byte[] data, int offset, int size, int timeout)
        {
            while (true)
            {

                lock (this)
                {

                    CheckState();

                    if (m_size != 0)
                    {
                        int ret = internalGet(data, offset, size,true);
                        m_writeEvent.Set();

                        if (m_size == 0)
                        {
                            m_readEvent.Reset();
                        }

                        return ret;
                    }
                    else if (timeout == 0)
                    {
                        return 0;
                    }

                }

                bool waitres = m_readEvent.WaitOne(timeout,false);

                if (timeout != Timeout.Infinite && !waitres)
                {
                    return 0;
                }

            }
        }


        public int Peek(byte[] data, int offset, int size,int timeout)
        {
            while (true)
            {

                lock (this)
                {
                    CheckState();

                    if (m_size != 0)
                    {
                        int ret = internalGet(data, offset, size, false);
                        
                        return ret;
                    }
                    else if (timeout == 0)
                    {
                        return 0;
                    }
                }

                bool waitres = m_readEvent.WaitOne(timeout,false);

                if (timeout != Timeout.Infinite && !waitres)
                {
                    return 0;
                }

            }
        }


        public void Close()
        {
            lock (this)
            {
                m_closed = true;

                m_size = 0;
                m_wr = 0;
                m_rd = 0;

                m_readEvent.Set();
                m_writeEvent.Close();

            }
        }


        private void CheckState()
        {

            if (m_closed)
            {

#if     COMPACT_FRAMEWORK

                throw new Exception("The queue has been closed.");

#else
                throw new OperationCanceledException("The queue has been closed.");
#endif

            }
        }


        public void Pop(int size)
        {
           
                lock (this)
                {
                    if (m_size < size)
                    {
                        throw new ArgumentOutOfRangeException("size is greater than avaliable data.");
                    }
                    
                    AdvanceReadPtr(size);

                    m_writeEvent.Set();

                    if (m_size == 0)
                    {
                       m_readEvent.Reset();
                    }
                        
                    
                }

        }




        private int internalGet(byte[] data, int offset, int size, bool advance)
        {
            if (data == null)
                throw new ArgumentNullException("data can't be null");

            if (offset + size > data.Length)
                throw new ArgumentOutOfRangeException("offset + size greater than the input data size");


            if (size > m_size)
            {
                size = m_size;
            }


            int freeLength = (m_data.Length - m_rd);

            int myLength = size;

            if (freeLength < myLength)
            {
                myLength = freeLength;
            }

            Buffer.BlockCopy(m_data, m_rd, data, offset, myLength);
            
            offset += myLength;
            myLength = (size - myLength);

            if (myLength != 0)
            {
                Buffer.BlockCopy(m_data, 0, data, offset, myLength);
            }

            if (advance)
            {
                AdvanceReadPtr(size);
            }

            return size;
        }


        public int Capacity
        {
            get
            {
                return m_data.Length;
            }
        }

        public int Size
        {
            get
            {
                lock (this)
                {
                    return m_size;
                }
            }
        }

        public int FreeSpace
        {
            get
            {
                lock (this)
                {
                    return m_data.Length - m_size;
                }
            }
        }


         

        private int AdvancePtr(int ptr, int distance)
        {
            return ((ptr + distance) % m_data.Length); 
        }

        private int AdvanceReadPtr(int distance)
        {
            m_rd = AdvancePtr(m_rd,distance);
                       
            m_size -= distance;

            return m_rd;
        }

        private int AdvanceWritePtr(int distance)
        {
            m_wr = AdvancePtr(m_rd, distance);

            m_size += distance;

            return m_wr;
        }
        
        


    }
}
