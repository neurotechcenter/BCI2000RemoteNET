using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace BCI2000RemoteNET
{
    public class BCI2000Connection
    {
        private static int defaultTimeout = 5000;
        private static string defaultTelnetIp = "localhost";
        private static Int32 defaultTelnetPort = 3999;
        
        private TcpClient tcp;
        private NetworkStream tcpStream;


        public int Timeout { get; set; } //send and recieve timeout in ms
        public string TelnetIp { get; set; }
        public Int32 TelnetPort { get; set; }
        public string OperatorPath { get; set; }
        public string WindowTitle { get; set; }
        public string Result { get; private set; }
        private bool TerminateOperator { get; set; }

        public BCI2000Connection()
        {
            Timeout = defaultTimeout;
            TelnetIp = defaultTelnetIp;
            TelnetPort = defaultTelnetPort;
        }

        //Ends connection to operator, terminates operator if it was started by a previous Connect() call
        public bool Disconnect()
        {
            Result = "";
            if (tcpStream != null) 
                tcpStream.Close();
            if (tcp != null)
                tcp.Close();

            if (TerminateOperator)
            {
                //todo: kill the running operator
            }

            return true;
        }

        public bool Connect() //Connects to operator module, starts operator if not running
        {
            if (TelnetIp == "" || TelnetIp == null)
                TelnetIp = defaultTelnetIp;
            if (TelnetPort == 0)
                TelnetPort = defaultTelnetPort;

            try{
                tcp = new TcpClient(TelnetIp, TelnetPort);
                tcpStream = tcp.GetStream();
            }
            catch (ArgumentNullException ex){
                Result = "ArgumentNullException: " + ex;
                return false;
            }
            catch (SocketException ex){
                Result = "SocketException: " + ex;
                return false;
            }

            tcp.SendTimeout = Timeout;

            if (tcp.Connected == true)
                Result = "Connected at address " + TelnetIp + ":" + TelnetPort;
            if (tcp.Connected != true){
                Result = "Not Connected at address " + TelnetIp + ":" + TelnetPort;
                return false;
            }
            return true;
        }

        public bool Execute(string command)
        {
            Result = "";
            if (!tcp.Connected){
                Result = "Not connected, call BCI2000Connection.Connect() to connect.";
                return false;
            }

            tcpStream.Write(toBytes(command), 0, toBytes(command).Length);
            


            return true;
        }

        public bool Connected()
        {
            return tcp.Connected;
        }


        private Byte[] toBytes(string str)
        {
            return System.Text.Encoding.ASCII.GetBytes(str);
        }
    }
}
