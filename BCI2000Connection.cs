using System;
using System.Collections.Generic;
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

        private TcpClient tcp;

        //response stuff
        private const string ReadlineTag = "\\AwaitingInput:";
        private const string AckTag = "\\AcknowledgedInput";
        private const string ExitCodeTag = "\\ExitCode";
        private const string TerminationTag = "\\Terminating";
        private const string Prompt = ">";



        public int Timeout { get; set; } //send and recieve timeout in ms
        public string TelnetIp { get; set; }
        public Int32 TelnetPort { get; set; }
        public string OperatorPath { get; set; }
        public string Result { get; private set; }
        private bool TerminateOperator { get; set; }


        private string windowTitle;
        public string WindowTitle {
            get {
                return windowTitle;
            }

            set {
                windowTitle = value;
                if (Connected())
                    Execute("set title \"" + value + "\"");
            } 
        }
        private int windowVisible;
        public int WindowVisible {
            get {
                return windowVisible;
            }
            set {
                windowVisible = value;
                if (value == 0)
                    Execute("hide window");
                if (value == 1)
                    Execute("show window");
            }
        }


        public BCI2000Connection()
        {
            Timeout = defaultTimeout;
            TelnetIp = defaultTelnetIp;
            TelnetPort = defaultTelnetPort;
            WindowVisible = defaultWindowVisible;
            WindowTitle = defaultWindowTitle;
        }

        //Ends connection to operator, terminates operator if it was started by a previous Connect() call
        public bool Disconnect()
        {
            Result = "";
            if (tcp != null)
                tcp.Close();

            if (TerminateOperator)
            {
                TerminateOperator = false;
                if (Connected())
                    Quit();
            }

            return true;
        }

        public virtual bool Connect() //Connects to operator module, starts operator if not running
        {
            Disconnect();

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
                Result = "Failed to connect to " + TelnetIp + ":" + TelnetPort;


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
            }

            tcp.SendTimeout = Timeout;

            WindowTitle = windowTitle;
            WindowVisible = windowVisible;

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
            if (!tcp.Connected)
            {
                Result = "Not connected, call BCI2000Connection.Connect() to connect.";
                return false;
            }

            string response;

            try
            {
                tcp.Client.Send(stringToBytes(command + "\r\n"));
                Byte[] responseData = new Byte[1024];
                tcp.Client.Receive(responseData);
                response = bytesToString(responseData);
            }
            catch (SocketException ex)
            {
                Result = "SocketException: " + ex + ", socket error code " + ex.SocketErrorCode;
                return false;
            }
            return ProcessResponse(response, ref outCode);
        }

        public bool ProcessResponse(string response, ref int outCode)
        {
            if (response.Contains(ReadlineTag))
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
            else if (response.Contains(ExitCodeTag))
            {
                outCode = response.Length;
            }
            else if (response.Contains(TerminationTag))
            {
                tcp.Client.Close();
                tcp.Close();
                TerminateOperator = false;
                return true;
            }
            else {
                if (!OnOutput(response))
                {
                    StringBuilder ResultNew = new StringBuilder(Result);
                    if (!String.IsNullOrEmpty(Result))
                        ResultNew.Append('\n');
                    ResultNew.Append(response);
                    Result = ResultNew.ToString();
                }
                if (response.Equals("true", StringComparison.OrdinalIgnoreCase))
                    outCode = 0;
                else if (response.Equals("false", StringComparison.OrdinalIgnoreCase))
                    outCode = 1;
                else if (String.IsNullOrWhiteSpace(response))
                    outCode = 0;
                else
                    outCode = -1;
            }
            return true;
        }

        public bool Connected()
        {
            return tcp.Connected;
        }

        public bool Quit()
        {
            Execute("quit");
            return true;
        }

        public virtual bool OnInput()//input and output handler methods to be overridden
        {
            return false;
        }
        public virtual bool OnOutput(string output)
        {
            return false;
        }



        private Byte[] stringToBytes(string str)
        {
            return System.Text.Encoding.ASCII.GetBytes(str);
        }
        private string bytesToString(Byte[] bytes)
        {
            return System.Text.Encoding.ASCII.GetString(bytes);
        }


    }
}
