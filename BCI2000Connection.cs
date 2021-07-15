using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace BCI2000RemoteNET
{
    public class BCI2000Connection
    {
        private static int defaultTimeout = 1000;
        private static string defaultTelnetIp = "127.0.0.1";
        private static Int32 defaultTelnetPort = 3999;
        private static int defaultWindowVisible = 1;
        private static string defaultWindowTitle = "";

        private TcpClient tcp;


        public int Timeout { get; set; } //send and recieve timeout in ms
        public string TelnetIp { get; set; }
        public Int32 TelnetPort { get; set; }
        public string OperatorPath { get; set; }
        public string WindowTitle { get; set; }
        public int WindowVisible { get; set; }
        public string Result { get; private set; }
        private bool TerminateOperator { get; set; }

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

        public bool Connect() //Connects to operator module, starts operator if not running
        {
            Disconnect();

            if (TelnetIp == "" || TelnetIp == null)
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
                success = false;
            }

            if (!success && (OperatorPath == "" || OperatorPath == null))
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
                    Result = "Failed to connect to operator at 127.0.0.1:" + TelnetPort;
                }
            }

            tcp.SendTimeout = Timeout;
            if (tcp.Connected == true)
                Result = "Connected at address " + TelnetIp + ":" + TelnetPort;
            if (tcp.Connected != true)
            {
                Result = "Not Connected at address " + TelnetIp + ":" + TelnetPort;
                return false;
            }
            SetWindowTitle(WindowTitle);
            SetWindowVisible(WindowVisible);

            return true;
        }

        public bool Execute(string command)
        {
            Result = "";
            if (!tcp.Connected)
            {
                Result = "Not connected, call BCI2000Connection.Connect() to connect.";
                return false;
            }

            try
            {
                tcp.Client.Send(stringToBytes(command + "\r\n"));
                Byte[] responseData = new Byte[1024];
                tcp.Client.Receive(responseData);
                string response = bytesToString(responseData);
            }
            catch (SocketException ex)
            {
                Result = "SocketException: " + ex + ", socket error code " + ex.SocketErrorCode;
                return false;
            }
            return true;
        }

        public bool Connected()
        {
            return tcp.Connected;
        }

        public bool SetWindowTitle(string title)
        {
            WindowTitle = title;
            if (Connected())
            {
                Execute("set title \"" + title + "\"");
                return true;
            }
            return false;
        }
        public bool SetWindowVisible(int visible)
        {
            WindowVisible = visible;
            if (visible == 0)
                Execute("hide window");
            if (visible == 1)
                Execute("show window");
            return true;
        }


        public bool Quit()
        {
            Execute("quit");
            return true;
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
