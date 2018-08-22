using System;
using System.IO.Ports;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using Bisync;
using System.IO;

namespace Tester
{


    class Program
    {
        private static byte[] DLRI = { 0x00, (byte)'$', (byte)'i', (byte)0x0d };
        private static byte[] DLR0 = { 0x01, (byte)'$', (byte)'0', (byte)0x0d };
        private static byte[] DLRHELLO = { 0x00, (byte)'$', (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o', (byte)0x0d };
        private static byte[] DLRIO = System.Text.Encoding.ASCII.GetBytes("\x0$IO\n");
        private static byte[] DLRG = System.Text.Encoding.ASCII.GetBytes("\x0$G\r\n");
        private static byte[] DLRLSTOP = System.Text.Encoding.ASCII.GetBytes("$GetLastStop\r\n");
        private static byte[] DLRVNAME = System.Text.Encoding.ASCII.GetBytes("$VehicleName\r\n");
        private static byte[] DLRVCROUTE = System.Text.Encoding.ASCII.GetBytes("$RequestCurrentRoute\r\n");

        private static byte[] DLRTTT = new byte[300];


        private static byte[] DLRLSTOP1 = System.Text.Encoding.ASCII.GetBytes("$GetLa");
        private static byte[] DLRLSTOP2 = System.Text.Encoding.ASCII.GetBytes("stStop\r\n");
        private static List<byte[]> s_cmd = new List<byte[]>();


        private static Bisync.Bus s_comm = new Bisync.Bus("COM11",5000);
        private static volatile int p = 0;


        static void Main(string[] args)
        {


            int i = 0;
            s_comm.Start();

            s_cmd.Add(DLRI);
            s_cmd.Add(DLR0);
            s_cmd.Add(DLRIO);
            s_cmd.Add(DLRG);


            Node node = s_comm.CreateNode(1);

            byte[] data = new byte[200];

            int sendBytes = 0;
            int recvBytes = 0;
            int maxSize = 0;
            int reqResp = 0;
            int time;
            int period = Environment.TickCount;



         
            while (true)
            {

                time = Environment.TickCount;
               
                node.Send(s_cmd[i],0,s_cmd[i].Length,1000);

                sendBytes += s_cmd[i].Length;

                int ret = node.Receive(data, 0, data.Length,Timeout.Infinite);

                time = Environment.TickCount - time;

                if (ret == -1)
                {
                    Debug.WriteLine("Read timeout\n");
                    continue;
                }



                recvBytes += ret;

                if (reqResp < time)
                {
                    reqResp = time;
                }

                if (ret > maxSize)
                {
                    maxSize = ret;
                }

                if (Environment.TickCount - period >= 1000)
                {
                    Debug.WriteLine(String.Format("recv bytes/sec {0} send bytes/sec {1} max size {2} reqResp {3}",recvBytes,sendBytes,maxSize,reqResp));
                    recvBytes  =0;
                    sendBytes = 0;
                    maxSize = 0;
                    reqResp = 0;
                    period = Environment.TickCount;
                }


                i = (i + 1) % s_cmd.Count;


            }
            


            s_comm.Stop();
        }

    }

}
