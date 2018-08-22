using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Bisync
{
    class Portable
    {
#if     COMPACT_FRAMEWORK
        [DllImport("coredll.dll", SetLastError = true)]  
        public static extern uint WaitForMultipleObjects(uint nCount, IntPtr[] handles, bool bWaitAll, uint dwMilliseconds);

        [DllImport("coredll.dll", EntryPoint = "CreateSemaphoreW", SetLastError=true)]
        private static extern IntPtr CreateSemaphore(IntPtr securityAttributes, int initialCount, int maximumCount, string name);

        [DllImport("coredll.dll", SetLastError = true)]
        private static extern bool ReleaseSemaphore(IntPtr handle, int releaseCount, out int previousCount);

        [DllImport("coredll.dll", EntryPoint = "CloseHandle", SetLastError = true)]
        private static extern bool NativeCloseHandle(IntPtr hObject);

        [DllImport("coredll.dll", EntryPoint = "WaitForSingleObject", SetLastError = true)]
        private static extern UInt32 NativeWaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);
#else
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForMultipleObjects(uint nCount, IntPtr[] handles, bool bWaitAll, uint dwMilliseconds);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateSemaphore(IntPtr securityAttributes, int initialCount, int maximumCount, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReleaseSemaphore(IntPtr handle, int releaseCount, out int previousCount);

        [DllImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
        private static extern bool NativeCloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", EntryPoint = "WaitForSingleObject", SetLastError = true)]
        private static extern UInt32 NativeWaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);
#endif




        public static uint WAIT_OBJECT_0 = 0;
        public static uint WAIT_ABANDONED_0 = 0x00000080;
        public static uint WAIT_TIMEOUT = 0x00000102;
        public static uint WAIT_FAILED = 0xFFFFFFFF;


        public static int WaitAny(IntPtr[] handles, uint timeout)
        {
            uint ret = WaitForMultipleObjects((uint)handles.Length, handles, false, timeout);

            if (ret == WAIT_TIMEOUT)
            {
                return (int)Portable.WAIT_TIMEOUT;
            }
            else if (ret >= WAIT_ABANDONED_0)
            {
#if     COMPACT_FRAMEWORK                
                
                throw new Exception();

#else
                throw new AbandonedMutexException();
#endif
            }
            else if (ret >= WAIT_OBJECT_0)
            {
                return (int)(ret - WAIT_OBJECT_0);
            }

            return (int)Portable.WAIT_TIMEOUT;
        }

        public static  bool WaitOne(IntPtr hHandle, int dwMilliseconds)
        {
            uint ret = NativeWaitForSingleObject(hHandle, (uint)dwMilliseconds);

            if (ret == WAIT_ABANDONED_0)
            {
#if     COMPACT_FRAMEWORK

                throw new Exception();

#else
                throw new AbandonedMutexException();
#endif
            }

            return (ret == WAIT_OBJECT_0);

        }


        public static IntPtr CreateSemaphore(int initial, int max)
        {
            return CreateSemaphore(IntPtr.Zero, initial, max, null);
        }

        public static bool ReleaseSemaphore(IntPtr handle, int count)
        {
            int prevCount = 0;
            return ReleaseSemaphore(handle, count, out prevCount);
        }


        public static void CloseHandle(IntPtr hHandle)
        {
            NativeCloseHandle(hHandle);
        }

    }





}
