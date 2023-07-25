///////////////////////////////////////////////////////////////////////
// Author: tytbutler@yahoo.com
// Description: A class for controlling BCI2000 remotely from a .NET
//      application. Does not depend on BCI2000 framework.
//      On Error, a function returns false, and errors raised by 
//      the class are stored in Result, and errors raised by the
//      Operator are stored in Received.
//
//      Adapted from the C++ BCI2000Remote
// (C) 2000-2021, BCI2000 Project
// http://www.bci2000.org
///////////////////////////////////////////////////////////////////////


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


        private readonly char[] TRIM_CHARS =  new char[] { '\r', '\n', ' ', '>' };

        private readonly string[] SYSTEM_STATES = new string[] {"unavailable",
            "idle",
            "startup",
            "initialization",
            "resting",
            "suspended",
            "paramsmodified",
            "running",
            "termination",
            "busy"}; 

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
	
	//Connects to operator and immediately runs BCI2000Shell commands given as an argument.
        public bool Connect(string[] initCommands, string[] eventNames)
        {
            bool success = Connect();
            foreach (string command in initCommands)
            {
                SimpleCommand(command);
            }
            foreach (string evn in eventNames)
            {
                AddEvent(evn, 32, 0);
            }
            return success;
        }
        public bool Connect(string[] initCommands)
        {
            return Connect(initCommands, new string[0]);
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
            SimpleCommand("set config");
            WaitForSystemState("Resting|Initialization");
            Execute("capture messages none");
            Execute("get system state");
            //bool success = !ResponseContains("Resting");
            Execute("flush messages");
            bool success = true;
            return success;
        }

        public void Start()
        {
            bool success = true;
            Execute("get system state");
            if (ResponseContains("Running"))
            {
                Console.Write("System is already running");
            }
            else if (!ResponseContains("Resting") && !ResponseContains("Suspended"))
                SetConfig();
            SimpleCommand("start system");
        }

        public void Stop()
        {
            Execute("get system state");
            if (!ResponseContains("Running"))
            {
                Console.Write("System is not running");
            }
            SimpleCommand("stop system");
        }

        public void SetParameter(string name, string value)
        {
            SimpleCommand("set parameter \"" + name + "\" \"" + value + "\"");
        }

        public string GetParameter(string name)
        {
            int outCode = 0;
            Execute("is parameter \"" + name + "\"", ref outCode);
            if (outCode == 1)//name is a valid parameter
            {
                Execute("get parameter \"" + name + "\"");
                return Response;
            }
            else
            {
                throw new BCI2000CommandException(name + " is not a valid parameter name");
            }
        }

        public void LoadParameters(string filename) //loads parameters on the machine on which BCI2K is running
        {
            SimpleCommand("load parameters \"" + filename + "\"");
        }

        public void AddStateVariable(string name, UInt32 bitWidth, double initialValue)
        {
            SimpleCommand("add state \"" + name + "\" " + bitWidth + ' ' + initialValue);
        }

        public void SetStateVariable(string name, double value)
        {
            SimpleCommand("set state \"" + name + "\" " + value.ToString());
        }
        public double GetStateVariable(string name)
        {
            SimpleCommand("get state \"" + name + "\"");
            return double.Parse(GetResponseWithoutPrompt());
        }

        public void AddEvent(string name, UInt32 bitWidth, UInt32 initialValue) {
            SimpleCommand("add event \"" + name + "\" " + bitWidth + " " + initialValue);
        }

        public void SetEvent(string name, UInt32 value)
        {
            SimpleCommand("set event " + name + " " + value);
        }

        public void PulseEvent(string name, UInt32 value)
        {
            SimpleCommand("pulse event " + name + " " + value);
        }
        public int GetEvent(string name)
        {
            SimpleCommand("get event " + name);
            return int.Parse(GetResponseWithoutPrompt());
        }

        public double GetSignal(uint channel, uint element)
        {
            SimpleCommand("get signal(" + channel + "," + element + ")");
            return Double.Parse(GetResponseWithoutPrompt());
        }

        public void  WaitForSystemState(string state)
        {
            SimpleCommand("wait for " + state);
        }

        public string GetSystemState()
        {
            try { 
                SimpleCommand("get system state");
                throw new Exception("BCI2000RemoteNET error: Unreachable code reached");
            } catch (BCI2000CommandException e)
            {
                string res = GetResponseWithoutPrompt();
                if (SYSTEM_STATES.Any(state => res.ToLower().Contains(state.ToLower()))) {
                    return res;
                } else
                {
                    throw e;
                }
            }
        }

        public void SimpleCommand(string command)
        {
            Execute(command);
            //fails if Result is not empty or nonzero
            //This does mean that SimpleCommand will always throw an exception when receiving a text response.
            //All methods in the BCI2000Remote class are designed with tbis in mind, and will catch the error if necessary.
            if (!(string.IsNullOrWhiteSpace(Response) || Atoi(Response) != 0 || Response.Contains(">")))
            {
                throw new BCI2000CommandException("Command \"" + command + "\" failed. Check BCI2000 log for details. Response: " + Response);
            } 
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


        private string GetResponseWithoutPrompt()
        {
            return Response.Trim(TRIM_CHARS);
        }


        // Equivalents of C functions used in BCI2000Remote
        private int Atoi(string str)
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

        private bool Stricmp(string str1, string str2) 
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
