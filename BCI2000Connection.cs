using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace BCI2000RemoteNET
{
    public class BCI2000Connection
    {
        private static double defaultTimeout = 5.0;
        private static string defaultTelnetIp = "localhost";
        private static Int32 defaultTelnetPort = 3999;
        
        private TcpClient tcp;
        private NetworkStream tcpStream;


        public double Timeout { get; set; }
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


        public bool Disconnect()
        {
            Result = "";
            if (tcpStream != null)
                tcpStream.Close();
            if (tcp != null)
                tcp.Close();

            return true;
        }

        public bool Connect()
        {
            if (TelnetIp == "" || TelnetIp == null)
                TelnetIp = defaultTelnetIp;
            if (TelnetPort == 0)
                TelnetPort = defaultTelnetPort;

            try
            {
                tcp = new TcpClient(TelnetIp, TelnetPort);
                tcpStream = tcp.GetStream();
            }
            catch (ArgumentNullException ex)
            {
                Result = "ArgumentNullException: " + ex;
                return false;
            }
            catch (SocketException ex)
            {
                Result = "SocketException: " + ex;
                return false;
            }
            if (tcp.Connected == true)
                Result = "Connected at address " + TelnetIp + ":" + TelnetPort;
            if (tcp.Connected != true)
                Result = "Not Connected at address " + TelnetIp + ":" + TelnetPort; 
            
            

            return true;

        }

    }
}
