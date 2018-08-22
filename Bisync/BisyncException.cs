using System;


namespace Bisync
{
    public class BisyncException : Exception
    {
        public BisyncException(string msg) : base(msg)
        {
            
        }

        public BisyncException(string msg,Exception inner) : base(msg,inner)
        {

        }


    }
}
