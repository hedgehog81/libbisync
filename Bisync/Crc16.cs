using System;
using System.Collections.Generic;
using System.Text;

namespace Bisync
{
    class Crc16
    {
        private const ushort polynomial = 0xA001;
        private static ushort[] table = new ushort[256];
        private ushort m_crc;


        public void Init()
        {
            m_crc = 0;
        }

        public ushort End()
        {
            return (ushort)(((m_crc >> 8) & 0xFF) | (m_crc << 8));
        }


        public void Update(byte[] bytes, int length)
        {
            for (int i = 0; i < length; ++i)
            {
                Update(bytes[i]);
            }
        }

        public void Update(byte ch)
        {
            byte index = (byte)(m_crc ^ ch);
            m_crc = (ushort)((m_crc >> 8) ^ table[index]);
        }


        public ushort ComputeChecksum(byte[] bytes, int length)
        {

            Init();

            Update(bytes, length);

            return End();
        }

        public byte[] ComputeChecksumBytes(byte[] bytes, int length)
        {
            ushort crc = ComputeChecksum(bytes, length);
            return BitConverter.GetBytes(crc);
        }

        static Crc16()
        {
            ushort value;
            ushort temp;
            for (ushort i = 0; i < table.Length; ++i)
            {
                value = 0;
                temp = i;
                for (byte j = 0; j < 8; ++j)
                {
                    if (((value ^ temp) & 0x0001) != 0)
                    {
                        value = (ushort)((value >> 1) ^ polynomial);
                    }
                    else
                    {
                        value >>= 1;
                    }
                    temp >>= 1;
                }
                table[i] = value;
            }
        }
    }


}
