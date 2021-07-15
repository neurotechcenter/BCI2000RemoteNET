using System;
using System.Collections.Generic;
using System.Text;

namespace BCI2000RemoteNET
{
    public class BCI2000Remote : BCI2000Connection
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
                if (Connected())
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
                if (Connected())
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
                if (Connected())
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
            int outCode = 0;
            for (int i = 0; i < modules.Count; i++)
            {
                Execute("start executable " + modules[i] + " --local", ref outCode);
            }


            return true;
        }


        
    }
}
