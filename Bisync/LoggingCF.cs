using System;
using System.Diagnostics;
using System.Threading;

namespace Bisync
{


    // Summary:
    //     Identifies the type of event that has caused the trace.
    public enum TraceEventType
    {
        // Summary:
        //     Fatal error or application crash.
        Critical = 1,
        //
        // Summary:
        //     Recoverable error.
        Error = 2,
        //
        // Summary:
        //     Noncritical problem.
        Warning = 4,
        //
        // Summary:
        //     Informational message.
        Information = 8,
        //
        // Summary:
        //     Debugging trace.
        Verbose = 16,
        //
        // Summary:
        //     Starting of a logical operation.
        
        Start = 256,
        //
        // Summary:
        //     Stopping of a logical operation.
        
        Stop = 512,
        //
        // Summary:
        //     Suspension of a logical operation.
        
        Suspend = 1024,
        //
        // Summary:
        //     Resumption of a logical operation.
        
        Resume = 2048,
        //
        // Summary:
        //     Changing of correlation identity.
       
        Transfer = 4096,
    }


    class Logging
    {
        

        public static bool IsTraceLevelOn(TraceEventType evt)
        {
            return false;
        }

        public static void Trace(TraceEventType evt, string msg)
        {
            
        }

        public static void TraceHex(TraceEventType evt, string msg, byte[] buffer, int offset, int size)
        {
           
        }

        public static void TraceEnter(string scope)
        {

        }

        public static void TraceExit(string scope)
        {
            
        }

        public static void TraceException(Exception ex)
        {
            
        }


        
    }



}