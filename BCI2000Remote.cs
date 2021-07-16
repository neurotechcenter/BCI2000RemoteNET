using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace BCI2000RemoteNET
{
    public class BCI2000Remote : BCI2000Connection //All public methods are boolean and return true if they succeed, false if they fail. Data output is handled by passing a reference.
    {
        private string subjectID;
        public string SubjectID
        {
            get
            {
                return subjectID;
            }
            set
            {
                subjectID = value;
                if (Connected() && !String.IsNullOrEmpty(subjectID))
                    Execute("set parameter SubjectName \"" + subjectID + "\"");
            }
        }

        private string sessionID;
        public string SessionID
        {
            get
            {
                return sessionID;
            }
            set
            {
                sessionID = value;
                if (Connected() && !String.IsNullOrEmpty(sessionID))
                    Execute("set parameter SubjectSession \"" + sessionID + "\"");
            }
        }

        private string dataDirectory;
        public string DataDirectory
        {
            get
            {
                return dataDirectory;
            }
            set
            {
                dataDirectory = value;
                if (Connected() && !String.IsNullOrEmpty(dataDirectory))
                    Execute("set parameter DataDirectory \"" + dataDirectory + "\"");
            }
        }


        public BCI2000Remote()
        {

        }

        public override bool Connect()
        {
            bool success = base.Connect();
            if (success) {
                if (!String.IsNullOrEmpty(SubjectID))
                    SubjectID = subjectID;
                if (!String.IsNullOrEmpty(SessionID))
                    SessionID = sessionID;
                if (!String.IsNullOrEmpty(DataDirectory))
                    DataDirectory = dataDirectory;
            }
            return success;
        }


        public bool StartupModules(List<string> modules)
        {
            Execute("shutdown system");
            Execute("startup system localhost");
            StringBuilder errors = new StringBuilder();
            int outCode = 0;
            for (int i = 0; i < modules.Count; i++)
            {
                Execute("start executable " + modules[i] + " --local", ref outCode);
                if (outCode != 1)
                {
                    errors.Append('\n' + modules[i] + " returned code " + outCode);
                }
                else if (!String.IsNullOrEmpty(Result))
                {
                    errors.Append('\n' + Result);
                }
            }
            if (!String.IsNullOrWhiteSpace(errors.ToString())) //errors while starting up modules
            {
                Result = "Could not start modules: " + errors.ToString();
                return false;
            }
            WaitForSystemState("Connected");
            
            return true;
        } 

        public bool SetConfig()
        {
            SubjectID = subjectID;
            SessionID = sessionID;
            DataDirectory = dataDirectory;
            Execute("capture messages none warnings errors");
            string tempResult = "";
            if (SimpleCommand("set config"))
                WaitForSystemState("Resting|Initialization");
            else
                tempResult = Result;
            Execute("capture messages none");
            Execute("get system state");
            bool success = !ResultContains("Resting");
            Execute("flush messages");
            if (!String.IsNullOrWhiteSpace(tempResult))//set config caused errors
                Result = tempResult + '\n' + Result;
            return success;
        }

        public bool Start()
        {
            bool success = true;
            Execute("get system state");
            if (ResultContains("Running"))
            {
                Result = "System is already running";
                success = false;
            }
            else if (ResultContains("Resting") || ResultContains("Suspended"))
                success = SetConfig();
            if (success)
                success = SimpleCommand("start system");
            return success;
        }

        public bool Stop()
        {
            Execute("get system state");
            if (!ResultContains("Running"))
            {
                Result = "System is not running";
                return false;
            }
            return SimpleCommand("stop system");
        }

        public bool SetParameter(string name, string value)
        {
            return SimpleCommand("set parameter \"" + name + "\" \"" + value + "\"");
        }

        bool GetParameter(string name, ref string outValue)//uses a ref to avoid the problem of returning a value if the command fails
        {
            int outCode = 0;
            Execute("is parameter \"" + name + "\"", ref outCode);
            if (outCode == 1)//name is a valid parameter
            {
                Execute("get parameter \"" + name + "\"");
                outValue = Result;
                return true;
            }
            else
            {
                Result = name + " is not a valid parameter name";
                return false;
            }
        }

        public bool LoadParametersLocal(string filename) //loads parameters from local (does not matter if running BCI2K locally, just use remote)
        {//Also it probably doesnt work at the moment
            StreamReader file;
            try {
                file = File.OpenText(filename);
            }
            catch (Exception ex)
            {
                Result = "Could not open file " + filename + ", " + ex.Message;
                return false;
            }
            string line;
            int errors = 0;
            while ((line = file.ReadLine()) != null)
            {
                errors += Convert.ToInt32(!SimpleCommand("set parameter " + EscapeSpecialChars(line)));//adds number of parameter adds which fail, inverted because a failure will return a false or 0
            }
            if (Convert.ToBoolean(errors))
            {
                Result = "Could not add " + errors + " parameter(s)";
                return false;
            }
            return true;
        }

        public bool LoadParametersRemote(string filename) //loads parameters on the machine on which BCI2K is running
        {
            return SimpleCommand("load parameters \"" + filename + "\"");
        }

        public bool AddStateVariable(string name, UInt32 bitWidth, double initialValue)
        {
            return SimpleCommand("add state \"" + name + "\" " + bitWidth + ' ' + initialValue);
        }

        public bool SetStateVariable(string name, double value)
        {
            return SimpleCommand("set state \"" + name + "\" " + value.ToString());
        }
        public bool GetStateVariable(string name, ref double outValue)
        {
            if (SimpleCommand("get state \"" + name + "\""))
            {
                try
                {
                    outValue = Double.Parse(Result);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        public bool WaitForSystemState(string state)
        {

            int timeout = Timeout - 1;
            return SimpleCommand("wait for " + state + " " + timeout.ToString());
        }

        public bool SimpleCommand(string command)
        {
            Execute(command);
            return string.IsNullOrWhiteSpace(Result) || Atoi(Result) != 0; //returns true if Result is empty or nonzero
        }

        private string EscapeSpecialChars(string str)
        {
            string escapeChars = "#\"${}`&|<>;\n";
            StringBuilder stringFinal = new StringBuilder();
            char[] chars = str.ToCharArray();
            for (int c = 0; c < chars.Length; c++)
            {
                Byte charBit = Convert.ToByte(chars[c]);
                if (escapeChars.Contains(chars[c]) || charBit < 32 || charBit > 128)
                {
                    Byte CharBitChanged = (Byte)((charBit >> 4) | (charBit & 0xf));
                    stringFinal.Append('%' + BitConverter.ToString(new byte[] { CharBitChanged })); 
                }
                else
                    stringFinal.Append(chars[c]);
            }
            return stringFinal.ToString();
        }



        private int Atoi(string str)//implementation of c atoi() since original code uses it
        {
            int result;
            try
            {
                result = int.Parse(str);
            }
            catch (FormatException)
            {
                result = 0;
            }
            return result;
        }

        private bool Stricmp(string str1, string str2) // implementation of c stricmp
        {
            return str1.IndexOf(str2, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private bool ResultContains(string str1)//using stricmp on result is annoying
        {
            return Stricmp(Result, str1);
        }
    }
}
