using System;

namespace BCI2000
{
/**
    *  Exception indicating that a command sent to BCI2000 has failed
    */
    internal class BCI2000CommandException : Exception
    {
        internal BCI2000CommandException(string msg) : base(msg) { }
    }

    internal class BCI2000ConnectionException : Exception 
    {
        internal BCI2000ConnectionException(string message) : base(message) { }
    }
}