using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace BCI2000RemoteNET
{
    public class BCI2000Connection
    {
        private static double defaultTimeout = 5.0;

        private static string defaultTelnetAddress = "localhost:3999";
        

        private TcpClient tcp;

        public double Timeout { get; set; }
        public string TelnetAddress { get; set; }
        public string OperatorPath { get; set; }
        public string WindowTitle { get; set; }
        public string Result { get; private set; }
        public BCI2000Connection()
        {
            Timeout = defaultTimeout;
            TelnetAddress = defaultTelnetAddress;
        }

    }
}
