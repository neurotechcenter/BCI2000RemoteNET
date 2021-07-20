///////////////////////////////////////////////////////////////////////
// Author: tytbutler@yahoo.com
// Description: A class for connecting to BCI2000 Remotely.
//      See BCI2000Remote for a more in-depth description
//
//      Adapted from the C++ BCI2000Connection
//
//
// (C) 2000-2021, BCI2000 Project
// http://www.bci2000.org
///////////////////////////////////////////////////////////////////////


using System;
using System.IO;
using System.Text;
using System.Net.Sockets;

namespace BCI2000RemoteNET
{
    public class BCI2000Connection
    {
        private const int defaultTimeout = 1000;
        private const string defaultTelnetIp = "127.0.0.1";
        private const Int32 defaultTelnetPort = 3999;
        private const int defaultWindowVisible = 1;
        private const string defaultWindowTitle = "";
        private const string defaultLogFile = "remoteLog.txt";
        private const bool defaultLogStates = false;
        private const bool defaultLogPrompts = false;


        private TcpClient tcp;

        //response stuff
        private const string ReadlineTag = "\\AwaitingInput:";
        private const string AckTag = "\\AcknowledgedInput";
        private const string ExitCodeTag = "\\ExitCode";
        private const string TerminationTag = "\\Terminating";
        private const string Prompt = ">";

        private string logFile;
        public string LogFile
        {
            get
            {
                return logFile;
            }
            set
            {
                logFile = value;
                if (Log == null)
                    return;
                if (Log != null)
                    Log.Close();
            }
        }
        protected StreamWriter Log { get; set; }
        
        
        private bool LastLogState { get; set; } //was the last thing sent a command to set state

        public bool LogStates { get; set; } //sets whether to log commands to set state, along with the received prompts afterwards
        public bool LogPrompts { get; set; } //sets whether to log all received prompts


        //changes to these will only take effect on Connect()
        public int Timeout { get; set; } //send and recieve timeout in ms
        public string TelnetIp { get; set; }
        public Int32 TelnetPort { get; set; }
        public string OperatorPath { get; set; }




        private string result; //three properties for results and logging
        public string Result
        {
            get
            {
                return result;
            }

            protected set
            {
                result = value;
                WriteLog("Result: ", value);
            }
        }
        private string sending;
        public string Sending
        {
            get
            {
                return sending;
            }
            set
            {
                sending = value;
                if (value.IndexOf("set state", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LastLogState = true;
                    if (!LogStates)// if logging states is disabled
                        return;
                }
                WriteLog("Sent: ", value);
            }
        }
        private string received;
        public string Received
        {
            get
            {
                return received;
            }
            private set
            {
                received = value;

                if (value.IndexOf(Prompt) == 0)
                {
                    if (LastLogState)
                    {
                        LastLogState = false;
                        if (!LogStates) //don't log prompts as received from 'set state'
                        {
                            return;
                        }
                    }
                    if (!LogPrompts) //don't log prompts
                        return;
                }
                WriteLog("Received: ", value);
            }
        }
        public string Response { get; protected set; }

        //result is set by methods in this class, response is the response from BCI2000, as Execute() shouldn't overwrite Result
        private bool TerminateOperator { get; set; }

        //Changes to these will result in a call to the Operator if connected
        private string windowTitle;
        public string WindowTitle
        {
            get
            {
                return windowTitle;
            }

            set
            {
                windowTitle = value;
                if (Connected())
                    Execute("set title \"" + value + "\"");
            }
        }
        private int windowVisible;
        public int WindowVisible
        {
            get
            {
                return windowVisible;
            }
            set
            {
                windowVisible = value;
                if (Connected())
                {
                    if (value == 0)
                        Execute("hide window");
                    if (value == 1)
                        Execute("show window");
                }
            }
        }


        public BCI2000Connection()
        {
            Timeout = defaultTimeout;
            TelnetIp = defaultTelnetIp;
            TelnetPort = defaultTelnetPort;
            WindowVisible = defaultWindowVisible;
            WindowTitle = defaultWindowTitle;
            LogFile = defaultLogFile;
            LogStates = defaultLogStates;
            LogPrompts = defaultLogPrompts;
        }


        //Ends connection to operator, terminates operator if it was started by a previous Connect() call
        public bool Disconnect()
        {
            Result = "";
            if (TerminateOperator)
            {
                TerminateOperator = false;
                if (Connected())
                    Quit();
            }
            if (tcp != null)
                tcp.Close();
            return true;
        }

        public virtual bool Connect() //Connects to operator module, starts operator if not running
        {
            
            if (String.IsNullOrEmpty(TelnetIp))
                TelnetIp = defaultTelnetIp;
            if (TelnetPort == 0)
                TelnetPort = defaultTelnetPort;


            tcp = new TcpClient();
            bool success = true;
            try
            {
                tcp.Connect(TelnetIp, TelnetPort);
            }
            catch (SocketException ex)
            {
                Result = "Failed to connect to " + TelnetIp + ":" + TelnetPort + ", " + ex.Message;
                success = false;
            }

            if (!success && (String.IsNullOrEmpty(OperatorPath)))
            {
                Result = "Failed to connect to " + TelnetIp + ":" + TelnetPort;
                return false;
            }

            if (!success) //tcp has not connected to running operator, so it will try to open a new operator and connect
            {
                tcp.Close();
                tcp = new TcpClient();

                try //run operator, must be local
                {
                    StringBuilder arguments = new StringBuilder();
                    arguments.Append("--Telnet \"127.0.0.1" + ':' + TelnetPort.ToString() + "\" ");
                    arguments.Append("--StartupIdle ");
                    if (WindowTitle != "" && WindowTitle != null)
                        arguments.Append("--Title \"" + WindowTitle + "\" ");
                    if (WindowVisible != 1)
                        arguments.Append("--Hide ");

                    System.Diagnostics.Process.Start(OperatorPath, arguments.ToString());
                }
                catch (InvalidOperationException)
                {
                    Result = "Failed to run operator at " + OperatorPath;
                    return false;
                }
                try//connect to started operator
                {
                    tcp.Connect("127.0.0.1", TelnetPort);
                }
                catch (SocketException ex)
                {
                    Result = "Failed to connect to operator at 127.0.0.1:" + TelnetPort + ", " + ex.Message;
                    return false;
                }

                TerminateOperator = true;
            }

            tcp.SendTimeout = Timeout;
            tcp.ReceiveTimeout = Timeout;

            WindowTitle = windowTitle;
            WindowVisible = windowVisible;

            Execute("change directory $BCI2000LAUNCHDIR");

            return true;
        }
        public bool Execute(string command)
        {
            int unused = 0;
            return Execute(command, ref unused);
        }
        public bool Execute(string command, ref int outCode)
        {
            Result = "";
            if (!Connected())
            {
                Result = "Not connected, call BCI2000Connection.Connect() to connect.";
                return false;
            }

            string responseUnprocessed;
            Sending = command;
            try
            {
                tcp.Client.Send(stringToBytes(command + "\r\n"));
                Byte[] responseData = new Byte[1024];
                tcp.Client.Receive(responseData);
                responseUnprocessed = bytesToString(responseData);
            }
            catch (SocketException ex)
            {
                Result = "SocketException: " + ex + ", socket error code " + ex.SocketErrorCode;
                return false;
            }
            Received = responseUnprocessed;
            return ProcessResponse(responseUnprocessed, ref outCode);
        }

        public bool ProcessResponse(string responseUnprocessed, ref int outCode)
        {
            Response = "";
            byte[] buffer = new byte[1024];
            while (Connected() && (!(responseUnprocessed.IndexOf(Prompt, StringComparison.OrdinalIgnoreCase) >= 0)) || tcp.Client.Available > 0)
            {
                if (responseUnprocessed.Contains(ReadlineTag))
                {
                    string input = "";
                    if (OnInput())
                    {
                        tcp.Client.Send(stringToBytes(input + "\r\n"));
                        Byte[] recvBuf = new byte[1024];
                        tcp.Client.Receive(recvBuf);
                        if (!bytesToString(recvBuf).Contains(AckTag))
                        {
                            Result = "Did not receive input acknowledgement";
                            return false;
                        }
                    }
                    else
                    {
                        Result = "Could not handle request for input: Override BCI2000Connection.OnInput to handle input";
                        return false;
                    }
                }
                else if (responseUnprocessed.Contains(ExitCodeTag))
                {
                    outCode = responseUnprocessed.Length;
                }
                else if (responseUnprocessed.Contains(TerminationTag))
                {
                    tcp.Client.Close();
                    tcp.Close();
                    TerminateOperator = false;
                    return true;
                }
                else
                {
                    if (!OnOutput(responseUnprocessed))
                    {
                        Response = responseUnprocessed;
                    }
                    if (responseUnprocessed.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0)//this is rewritten from the original code, but changed so that 1 reflects true and 0 reflects false
                        outCode = 1;
                    else if (responseUnprocessed.IndexOf("false", StringComparison.OrdinalIgnoreCase) >= 0)
                        outCode = 0;
                    else if (String.IsNullOrWhiteSpace(responseUnprocessed))
                        outCode = 1;
                    else
                        outCode = -1;
                }
                Array.Clear(buffer, 0, 1024);
                tcp.Client.Receive(buffer);
                responseUnprocessed = responseUnprocessed + bytesToString(buffer);
            }
            if (!Connected())
            {
                Result = "Lost Connection to BCI2000";
                return false;
            }
            if (responseUnprocessed.IndexOf(Prompt, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Response = responseUnprocessed;
                outCode = 1;
                return true;
            }
            return true;
        }

        public bool Connected()
        {
            if (tcp != null)
                return tcp.Connected;
            else
                return false;
        }

        public bool Quit()
        {
            Execute("quit");
            return String.IsNullOrWhiteSpace(Response);
        }

        public virtual bool OnInput()//input and output handler methods to be overridden
        {
            return false;
        }
        public virtual bool OnOutput(string output)
        {
            return false;
        }


        public void WriteLog(string toLog)//for writing to log from outside the class
        {
            WriteLog("External: ", toLog);
        }
        private void WriteLog(string logPreface, string toLog)
        {
            if (Log == null)
                Log = new StreamWriter(LogFile);
            if (Log != null && !String.IsNullOrWhiteSpace(toLog))
            {
                Log.WriteLine(DateTime.Now.ToString("HH:mm:ss:fff") + ' ' + logPreface + ' ' + toLog);
                Log.Flush();
            }
        }


        private Byte[] stringToBytes(string str)//utility methods
        {
            return System.Text.Encoding.ASCII.GetBytes(str);
        }
        private string bytesToString(Byte[] bytes)
        {
            return System.Text.Encoding.ASCII.GetString(bytes).Replace("\0", String.Empty);
        }


    }
}
