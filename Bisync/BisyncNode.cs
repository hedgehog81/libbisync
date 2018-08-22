using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Bisync
{
    public class Node : IDisposable
    {
        

        private             Bus  m_comm;
        private int         m_address;
        private bool        m_disposed;

        private CyclicBuffer m_recvBuffer;
        private CyclicBuffer m_sendBuffer;

        internal Node(Bus comm, int address, int readBuffer, int writeBuffer)
        {
            Debug.Assert(comm != null);
            Debug.Assert(address != 0);
            Debug.Assert(readBuffer != 0);
            Debug.Assert(writeBuffer != 0);

            m_recvBuffer = new CyclicBuffer(readBuffer);
            m_sendBuffer = new CyclicBuffer(writeBuffer);

            m_comm = comm;
            m_address = address;
        }

        

        public void Dispose()
        {
            //TODO
        }

        public void Send(byte[] buffer, int offset, int size,int timeout)
        {
            CheckObjectState();

            m_sendBuffer.Put(buffer, offset, size,timeout);

            m_comm.ScheduleSend(this);
            
        }

        public int Receive(byte[] buffer, int offset, int size, int timeout)
        {
            CheckObjectState();

            return m_recvBuffer.Get(buffer, offset, size,timeout);
        }

        public int GetStatus()
        {
            CheckObjectState();
            
            return 0;
        }

        public int Address
        {
            

            get
            {
                CheckObjectState();
                return m_address;
            }
        }


        public void Close()
        {
            CheckObjectState();

            if (!m_disposed)
            {
                m_comm.RemoveNode(this);
                m_disposed = true;
                m_sendBuffer.Close();
                m_recvBuffer.Close();
            }
        }


        internal CyclicBuffer sendQueue
        {

            get
            {
                CheckObjectState();
                return m_sendBuffer;
            }
        }


        internal CyclicBuffer recvQueue
        {
            

            get
            {
                CheckObjectState();
                return m_recvBuffer;
            }
        }


  
        private void CheckObjectState()
        {
            if (m_disposed)
                throw new ObjectDisposedException("The Bisync node is closed");
        }

    }
}
