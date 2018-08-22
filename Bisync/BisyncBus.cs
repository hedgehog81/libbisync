using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace Bisync
{


    public class Bus : IDisposable
    {

  
 
        enum RcvState
        {
            Idle,
            DLE_START,
            DLE,
            Data,
            CHS_1,
            CHS_2,
            Done
        }

        enum ControlChars
        {
            ENQ = 0x05,
            EOT = 0x04,
            ACK0 = 0x30,
            ACK1 = 0x31,
            NAK = 0x15,
            ETB = 0x17,
            STB = 0x00,
            STX = 0x02,
            ETX = 0x03,
            DLE = 0x10,
            PAD = 0xFF,
        }


        enum PacketType
        {
            Command,
            Data
        }


        private const int DEFAULT_NODE_BUFFER_SIZE = 512;

        private static byte[] ACK0 = { (byte)ControlChars.DLE, (byte)ControlChars.ACK0, (byte)ControlChars.PAD};
        private static byte[] ACK1 = { (byte)ControlChars.DLE, (byte)ControlChars.ACK1, (byte)ControlChars.PAD };
        private static byte[] ENQ = { (byte)ControlChars.ENQ, (byte)ControlChars.PAD, (byte)ControlChars.PAD };
        private static byte[] NAK = { (byte)ControlChars.NAK, (byte)ControlChars.PAD, (byte)ControlChars.PAD };


        private RcvState m_rcvState;

        private SerialPort m_port;             
        private Thread              m_dispatchThread;
        private ManualResetEvent    m_stopEvent         = new ManualResetEvent(false);
        private ManualResetEvent    m_startEvent        = new ManualResetEvent(false);
 
        
        private Crc16 m_crc             = new Crc16();

        private byte[] m_buffer         = new byte[1024];
        private byte[] m_tempBuffer     = new byte[512];
        private int    m_bufferSize;
        
        private PacketType m_packetType;
        private List<Node> m_nodes = new List<Node>();

        private List<Node> m_scheduledSends = new List<Node>();
        private IntPtr m_scheduleSem        = Portable.CreateSemaphore(0,15);
        private int m_readTimeout;



        public Bus(string PortName,int readTimeout)
        {
            m_dispatchThread = new Thread(new ThreadStart(Dispatcher));
            m_dispatchThread.IsBackground = true;

            m_port = new SerialPort(PortName, 38400, Parity.None, 8, StopBits.One);
            m_port.ReadTimeout = SerialPort.InfiniteTimeout;
            m_readTimeout = readTimeout;

        }


        public void Dispose()
        {
            m_port.Dispose();
            Portable.CloseHandle(m_scheduleSem);
            m_startEvent.Close();
            m_stopEvent.Close();

        }   

        public void Start()
        {
         
            Debug.Assert(!m_port.IsOpen);

            Logging.Trace(TraceEventType.Information, "Staring bus dispatcher");

            m_port.Open();
            m_stopEvent.Reset();
            m_startEvent.Reset();

            m_dispatchThread = new Thread(new ThreadStart(Dispatcher));
            m_dispatchThread.Start();

            if (!m_startEvent.WaitOne(1000,false))
            {
                Logging.Trace(TraceEventType.Error, "Unable to start the dispatcher thread");

                m_dispatchThread.Abort();
                m_dispatchThread.Join();
                m_dispatchThread = null;

                throw new BisyncException("Timeout while starting the dispatcher thread");
            }


        }


        public void Stop()
        {
            
            Logging.Trace(TraceEventType.Information, "Stopping bus dispatcher");

            m_stopEvent.Set();
            m_dispatchThread.Join();
            m_port.Close();

            m_scheduledSends.Clear();
            m_nodes.Clear();

            while (Portable.WaitOne(m_scheduleSem,0));

        }



        internal void ScheduleSend(Node node)
        {
            lock (m_scheduledSends)
            {
                if (!m_scheduledSends.Contains(node))
                {
                    m_scheduledSends.Add(node);
                    Portable.ReleaseSemaphore(m_scheduleSem,1);
                   
                }
            }
        }

        internal void CancelSend(Node node)
        {
            lock (m_scheduledSends)
            {
                if (m_scheduledSends.Contains(node))
                {
                    m_scheduledSends.Remove(node);
                    Portable.WaitOne(m_scheduleSem, Timeout.Infinite);
                }
            }
        }

        private Node GetScheduledNode()
        {
            lock(m_scheduledSends)
            {
                Node node =   m_scheduledSends[0];
                m_scheduledSends.RemoveAt(0);

                return node;
            }
        }

      
        public Node CreateNode(int address)
        {
            return CreateNode(address, DEFAULT_NODE_BUFFER_SIZE, DEFAULT_NODE_BUFFER_SIZE);
        }

        public Node CreateNode(int address,int readBuffer, int writeBuffer)
        {

            lock (this)
            {
                for (int i = 0; i < m_nodes.Count; ++i)
                {
                    if (m_nodes[i].Address == address)
                    {
                        return null;
                    }
                }


                Node node = new Node(this, address,readBuffer,writeBuffer);
                m_nodes.Add(node);

                return node;
            }
        }



        internal void RemoveNode(Node node)
        {
            Debug.Assert(node != null);

            lock (this)
            {
                m_nodes.Remove(node);
            }
        }





        private void Dispatcher()
        {

            m_startEvent.Set();
            
            IntPtr[] handles = { m_stopEvent.Handle, m_scheduleSem };


            while(true)
            {
                int res = Portable.WaitAny(handles, 2000);
                
                if (res == 0)
                {
                    return;
                }
                else if (res == 1)
                {

                    Node node = GetScheduledNode();

                    Debug.Assert(node != null);

                    if (SendToSlave(node))
                    {
                        PollSlave(node);
                    }
                    else
                    {
                        ScheduleSend(node);
                    }




                }
                else if (res == Portable.WAIT_TIMEOUT)
                {
                    Node[] nodes = null;
                    
                    lock (this)
                    {
                        nodes = m_nodes.ToArray();
                    }

                    if (nodes != null)
                    {

                        for (int i = 0; i < nodes.Length; ++i)
                        {
                            PollSlave(nodes[i]);
                        }
                    }


                }
                


            }

        }


        private enum RFrameType
        {
            Select,
            Poll
        }

        private int BuildRequestFrame(RFrameType type,int address)
        {
                   
 
            byte haddress  = 0;
            int indx = 0;
            
            m_buffer[indx++] = (byte)ControlChars.EOT;

            if (type == RFrameType.Select)
            {
                
                haddress = (byte)(0x80 | (address & 0x0F));
            }
            else
            {
                haddress = (byte)(0xC0 | (address & 0x0F));
            }

            m_buffer[indx++] = haddress;
            m_buffer[indx++] = haddress;

            m_buffer[indx++] = (byte)ControlChars.ENQ;

            return indx;

        }


        private int BuildDataFrame(byte[] data, int size)
        {
            int indx = 0;

            m_buffer[indx++] = (byte)ControlChars.DLE;
            m_buffer[indx++] = (byte)ControlChars.STX;

            for (int i = 0; i < size; ++i)
            {
                if (data[i] == (byte)ControlChars.DLE)
                {
                    m_buffer[indx++] = (byte)ControlChars.DLE;
                }

                m_buffer[indx++] = data[i];

            }

            m_buffer[indx++] = (byte)ControlChars.DLE;
            m_buffer[indx++] = (byte)ControlChars.ETX;

            m_crc.Init();
            m_crc.Update(data, size);
            m_crc.Update((byte)ControlChars.ETX);
            ushort crc = m_crc.End();

            m_buffer[indx++] = (byte)((crc >> 8) & 0xFF);
            m_buffer[indx++] = (byte)(crc & 0xFF);
            m_buffer[indx++] = (byte)ControlChars.PAD;


            return indx;
        }


        


        private int PortRead(byte[] buffer,int offset,int size)
        {
            try
            {
                return m_port.Read(m_tempBuffer, 0, m_tempBuffer.Length);
            }
            catch (TimeoutException ex)
            {
                Logging.Trace(TraceEventType.Warning, "Bus port read timeout");
            }

            return 0;
        }


        private bool ReceivePacket()
        {
            m_rcvState = RcvState.Idle;
            m_bufferSize = 0;

            int timeout = Environment.TickCount + m_readTimeout;


            while (Environment.TickCount < timeout)
            {


                int read = PortRead(m_tempBuffer, 0, m_tempBuffer.Length);

                if (read > 0)
                {

                    if (ProcessData(m_tempBuffer, read))
                    {

                        Logging.TraceHex(TraceEventType.Verbose, "Rt Pkt", m_buffer, 0, m_bufferSize);
                        
                        return true;
                    }

                }

            }

            Logging.Trace(TraceEventType.Warning,"Error reading packet from the bus.");

            return false;
        }




        private bool ProcessData(byte[] buffer, int length)
        {
            for (int i = 0; i < length; ++i )
            {
                byte ch = buffer[i];

                switch (m_rcvState)
                {
                    case RcvState.Idle:
                        {

                            switch (ch)
                            {
                                case (byte)ControlChars.DLE:
                                        m_rcvState = RcvState.DLE_START;
                                        
                                    break;
                                
                                case (byte)ControlChars.STX:
                                        m_rcvState = RcvState.Data;
                                    break;
                                
                                case (byte)ControlChars.NAK:
                                case (byte)ControlChars.EOT:
                                        m_packetType = PacketType.Command;
                                        m_buffer[m_bufferSize++] = ch;
                                        m_rcvState = RcvState.Done;

                                    break;
                            }
                            
                            
                           
                        }
                        break;
                    case RcvState.DLE_START:
                        {

                            switch (ch)
                            {
                                
                                case (byte)ControlChars.STX:
                                        m_rcvState = RcvState.Data;
                                        m_packetType = PacketType.Data;
                                    break;


                                case (byte)ControlChars.ACK0:
                                case (byte)ControlChars.ACK1:
                                        m_buffer[m_bufferSize++] = ch;
                                        m_rcvState = RcvState.Done;
                                        m_packetType = PacketType.Command;
                                    break;

                            }
                            
                            
                        }
                        break;

                    case RcvState.DLE:
                        {
                            if (ch == (byte)ControlChars.DLE)
                            {
                                m_buffer[m_bufferSize++] = ch;
                                m_rcvState = RcvState.Data;
                             
                            }
                            else if (ch == (byte)ControlChars.ETX || ch == (byte)ControlChars.ETB)
                            {
                                m_buffer[m_bufferSize++] = ch;
                                m_rcvState = RcvState.CHS_1;
                            }
                        }
                        break;
              
                    case RcvState.Data:
                        {
                            switch (ch)
                            {
                                case (byte)ControlChars.DLE:
                                        m_rcvState = RcvState.DLE;
                                    break;
                                

                                default:
                                    m_buffer[m_bufferSize++] = ch;
                                    break;
                            }
                            
                        }
                        break;
                    case RcvState.CHS_1:
                            m_buffer[m_bufferSize++] = ch;
                            m_rcvState = RcvState.CHS_2;
                        break;
                    
                    case RcvState.CHS_2:
                        {
                            m_buffer[m_bufferSize++] = ch;
                            m_rcvState = RcvState.Done;

                        }
                        break;
                    case RcvState.Done:
                        break;
                }

                if (m_rcvState == RcvState.Done)
                    return true;
            }

            return false;
        }


  
 
        private bool SendPacket(byte[] data, int size)
        {
            
            SendPacketAsync(data, size);

            return ReceivePacket();
        }


        private void SendPacketAsync(byte[] data, int size)
        {
            m_port.Write(data, 0, size);
        }





        private bool SelectSlave(int address)
        {

            int reqsize = BuildRequestFrame(RFrameType.Select, address);

            if (SendPacket(m_buffer, reqsize))
            {
                if (m_packetType == PacketType.Command)
                {

                    if (m_buffer[0] == (byte)ControlChars.ACK0)
                    {
                        return true;
                    }

                }

            }

            return false;

        }



        private bool SendToSlave(Node node)
        {
            
            
            int retries = 3;
            int state = 0;
            

            while (state < 2 && retries > 0)
            {

                

                switch (state)
                {
                    
                    case 0:
                        {

                              Logging.Trace(TraceEventType.Verbose,String.Format("Attempting to select slave {0}",node.Address));
                              
                              if (SelectSlave(node.Address))
                              {
                                    state += 1;
                              }
                              else
                              {
                                   Logging.Trace(TraceEventType.Verbose, "Error selectig slave");
                                   return false;
                              }
                                    
                              
                        }
                        break;

                    case 1:
                        {

                         

                            int read = node.sendQueue.Peek(m_tempBuffer, 0, m_tempBuffer.Length,Timeout.Infinite);

                            int len = BuildDataFrame(m_tempBuffer, read);

                            Logging.TraceHex(TraceEventType.Verbose,"Data Snd",m_tempBuffer,0,read);


                            if (SendPacket(m_buffer, len))
                            {
                                if (m_packetType == PacketType.Command)
                                {
                                    if (m_buffer[0] == (byte)ControlChars.ACK1)
                                    {
                                        state += 1;
                                        node.sendQueue.Pop(read);
                                    }
                                    else
                                    {
                                        retries -= 1;
                                    }
                                    
                                }
                            }
                            

                        }
                        break;



                        

                }


                
            }

            return (state == 2 && retries != 0);
  
        }

        

        private void PollSlave(Node node)
        {
            
            
          
            int retries = 3;
            int state = 0;
            byte ack = 0;

           
            while (state < 3 && retries > 0)
            {



                switch (state)
                {

                    case 0:
                        {
                            int reqsize = BuildRequestFrame(RFrameType.Poll, node.Address);

                            SendPacketAsync(m_buffer, reqsize);

                            state = 1;

                            
                        }
                        break;


                    case 1:
                        {


                            if (ReceivePacket())
                            {
                                if (m_packetType == PacketType.Data)
                                {

                                    ushort origcrc = (ushort)((m_buffer[m_bufferSize - 2] << 8) | (m_buffer[m_bufferSize - 1] & 0xFF));
                                    ushort crc = m_crc.ComputeChecksum(m_buffer, m_bufferSize - 2);

                                    if (origcrc != crc)
                                    {
                                        Logging.Trace(TraceEventType.Warning,String.Format("CRC error {0} {1}\n", origcrc, crc));
                                        SendPacketAsync(NAK, NAK.Length);
                                    }
                                    else
                                    {

                                        if (node.recvQueue.Put(m_buffer, 0, m_bufferSize - 2, 0))
                                        {

                                            state++;
                                            ack += 1;

                                        }
                                        else
                                        {
                                            Logging.Trace(TraceEventType.Warning,"Output queue overflow");
                                            return;
                                        }
                                    }
                                }
                                else if (m_packetType == PacketType.Command)
                                {
                                    if (m_buffer[0] == (byte)ControlChars.EOT)
                                    {
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                state = 0;
                                retries -= 1;
                            }

                        }
                        break;

                    case 2:
                        {
                            byte[] data = (ack & 1) == 0 ? ACK0 : ACK1;

                            SendPacketAsync(data, data.Length);

                            state = 1;
                        }
                        break;


                }



            }
           
            
        }

        

    }
}
