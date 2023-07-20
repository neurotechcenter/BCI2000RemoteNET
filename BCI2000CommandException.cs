using System;
/**
 *  Exception indicating that a command sent to BCI2000 has failed
 */
class BCI2000CommandException : Exception
{
	public BCI2000CommandException(string msg) : base(msg) { }
}