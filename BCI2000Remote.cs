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

        private const bool defaultStopOnQuit = true;
        private const bool defaultDisconnectOnQuit = true;

        public bool StopOnQuit { get; set; }
        public bool DisconnectOnQuit { get; set; }


        private char[] trimChars =  new char[] { '\r', '\n', ' ', '>' };

        public BCI2000Remote()
        {
            StopOnQuit = defaultStopOnQuit;
            DisconnectOnQuit = defaultDisconnectOnQuit;

        }

        ~BCI2000Remote()
        {
            if (StopOnQuit)
                Stop();
            if (DisconnectOnQuit)
                Disconnect();
        }

        public override bool Connect()
        {
            bool success = base.Connect();
            if (success)
            {
                if (!String.IsNullOrEmpty(SubjectID))
                    SubjectID = subjectID;
                if (!String.IsNullOrEmpty(SessionID))
                    SessionID = sessionID;
                if (!String.IsNullOrEmpty(DataDirectory))
                    DataDirectory = dataDirectory;
            }
            return success;
        }

        /**
         * 
         * takes module and arguments in the form of a dictionary with the keys being module names and value being a list of arguments
         * uses lists and dictionary because parsing strings is annoying
         * pass null as a value for no arguments other than --local
         * arguments don't need "--" in front, and whitespace is removed
         * 
         * 
         * **/
        public bool StartupModules(Dictionary<string, List<string>> modules)
        {
            Execute("shutdown system");
            Execute("startup system localhost");
            StringBuilder errors = new StringBuilder();
            int outCode = 0;
            foreach (KeyValuePair<string, List<string>> module in modules)
            {
                StringBuilder moduleAndArgs = new StringBuilder(module.Key + ' ');
                bool containsLocal = false;
                if (module.Value != null && module.Value.Count > 0)
                {
                    foreach (string argument in module.Value)
                    {
                        string argumentNoWS = new string(argument.Where(c => !Char.IsWhiteSpace(c)).ToArray());
                        if (!argumentNoWS.StartsWith("--"))//add dashes to beginning
                            argumentNoWS = "--" + argumentNoWS;
                        if (argumentNoWS.IndexOf("--local", StringComparison.OrdinalIgnoreCase) >= 0)
                            containsLocal = true;
                        moduleAndArgs.Append(argumentNoWS + ' ');
                    }
                }
                if (!containsLocal)//according to original, all modules start with option --local; appends --local to command
                    moduleAndArgs.Append("--local ");

                Execute("start executable " + moduleAndArgs.ToString(), ref outCode);
                if (outCode != 1)
                {
                    errors.Append('\n' + module.Key + " returned " + outCode);
                }
                Result = errors.ToString();
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
                tempResult = Response;
            Execute("capture messages none");
            Execute("get system state");
            //bool success = !ResponseContains("Resting");
            Execute("flush messages");
            if (!String.IsNullOrWhiteSpace(tempResult) && !tempResult.Equals(">"))//set config caused errors
                Result = tempResult + '\n' + Response;
            bool success = true;
            return success;
        }

        public bool Start()
        {
            bool success = true;
            Execute("get system state");
            if (ResponseContains("Running"))
            {
                Result = "System is already running";
                success = false;
            }
            else if (!ResponseContains("Resting") && !ResponseContains("Suspended"))
                success = SetConfig();
            if (success)
                success = SimpleCommand("start system");
            return success;
        }

        public bool Stop()
        {
            Execute("get system state");
            if (!ResponseContains("Running"))
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
                outValue = Response;
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
            try
            {
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
                errors += Convert.ToInt32(!SimpleCommand("add parameter " + EscapeSpecialChars(line)));//adds number of parameter adds which fail, inverted because a failure will return a false or 0
            }
            if (Convert.ToBoolean(errors))
            {
                Result = "Could not add " + errors + " parameter(s)";
            }

            errors = 0;
            while ((line = file.ReadLine()) != null)
            {
                errors += Convert.ToInt32(!SimpleCommand("set parameter " + EscapeSpecialChars(line)));//adds number of parameter adds which fail, inverted because a failure will return a false or 0
            }
            if (Convert.ToBoolean(errors))
            {
                Result = "Could not set " + errors + " parameter(s)";
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
                    string res = "";
                    if (Response.Contains('>')) {
                        res = Response.TrimEnd(trimChars);
                        }
                    outValue = Double.Parse(res);
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
            return SimpleCommand("wait for " + state);
        }

        public bool GetSystemState(ref string outState)
        {
            bool success = SimpleCommand("get system state");
            outState = Response;
            return success;
        }

        public bool SimpleCommand(string command)
        {
            Execute(command);
            return string.IsNullOrWhiteSpace(Response) || Atoi(Response) != 0 || Response.Equals(">"); //returns true if Result is empty or nonzero
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
                    //Byte CharBitChanged = (Byte)((charBit >> 4) | (charBit & 0xf));
                    stringFinal.Append('%' + BitConverter.ToString(new byte[] { charBit }));
                }
                else
                    stringFinal.Append(chars[c]);
            }
            return stringFinal.ToString();
        }



        private int Atoi(string str)//implementation of c atoi() since original code uses it
        {
            int output;
            try
            {
                output = int.Parse(str);
            }
            catch (FormatException)
            {
                output = 0;
            }
            return output;
        }

        private bool Stricmp(string str1, string str2) // implementation of c stricmp
        {
            if (str1 == null || str2 == null)
                return false;
            return str1.IndexOf(str2, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private bool ResponseContains(string str1)//using stricmp on result is annoying
        {
            return Stricmp(Response, str1);
        }
    }
}
