using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Bisync
{
    class Logging
    {
        private static readonly TraceSource s_trace = new TraceSource("Bisync");

        public static bool IsTraceLevelOn(TraceEventType evt)
        {
            return s_trace.Switch.ShouldTrace(evt);
        }

        public static void Trace(TraceEventType evt, string msg)
        {
            s_trace.TraceEvent(evt,0,msg);
        }

        public static void TraceHex(TraceEventType evt,string msg, byte[] buffer,int offset, int size)
        {
            if ( s_trace.Switch.ShouldTrace(evt) )
            {
                s_trace.TraceEvent(evt, 0, "{0} {1}", msg, ToHex(buffer,offset,size));
            } 
        }

        public static void TraceEnter(string scope)
        {
            if (IsTraceLevelOn(TraceEventType.Verbose))
            {
                s_trace.TraceEvent(TraceEventType.Verbose, 0, "Entering {0}", scope);
            }
        }

        public static void TraceExit(string scope)
        {
            if (IsTraceLevelOn(TraceEventType.Verbose))
            {
                s_trace.TraceEvent(TraceEventType.Verbose, 0, "Exiting {0}", scope);
            }
        }

        public static void TraceException(Exception ex)
        {
            if (IsTraceLevelOn(TraceEventType.Error))
            {
                s_trace.TraceEvent(TraceEventType.Error, 0, "Exception {0}", ex.ToString());
            }
        }


        private static string ToHex(byte[] buffer, int offset, int size)
        {
            Debug.Assert(buffer != null);

            StringBuilder builder = new StringBuilder(size * 2);

            int limit = size + offset;

            for (int i = offset; i < limit; ++i)
            {
                builder.AppendFormat("{0:X2}",buffer[i]);
            }

            return builder.ToString();
        }

    }
}
