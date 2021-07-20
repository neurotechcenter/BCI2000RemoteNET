BCI2000RemoteNET
===
Implementation of BCI2000Remote as .NET Standard
---

Github
[Link](https://github.com/Personator01/BCI2000RemoteNET)
BCI2000
[Link](https://www.BCI2000.org)

Description
---
Consists of a class, `BCI2000Remote`, which handles connection and communication with BCI2000's operator by sending commands over tcp,
essentially an automated command line.

Commmunication
---
BCI2000Remote communicates with BCI2000 over tcp, by sending Operator Scripting Commands ([Link](https://www.bci2000.org/mediawiki/index.php/User_Reference:Operator_Module_Scripting)), and receiving and processing responses.
After sending a command, BCI2000Remote will usually receive either a '>' (Prompt), if the command produces no output and no errors,
the output of the command, or any error that the command produces.



Options and Properties
---
`Timeout`
The timeout, in milliseconds, for sending and receiving commands, default 1000.

`TelnetIP`
The IP at which to connect to the Operator, default 127.0.0.1
Note: Don't use "localhost" when setting this, as this causes errors due to ambiguity in name resolution.

`TelnetPort`
The port to connect to the Operator, default 3999.

`OperatorPath`
The path to the Operator module to start up when there is no running operator at the IP and port specified, will always connect on localhost, at the previously given port.

`LogFile`
The path to the file to write the log. This is overwritten on `Connect()`, default "logFile.txt", in the directory where BCI2000RemoteNet is run.

`LogStates`
Whether or not to log commands to set a state, as well as the received Prompt if the command produces no errors, default false.

`LogPrompts`
Whether or not to log any Prompt, in which case only received output and errors will be logged, default true.

`StopOnQuit`
Whether or not to stop the Operator run when ending BCI2000Remote, default true.

`DisconnectOnQuit` Whether or not to disconnect from and/or terminate the Operator module when ending BCI2000Remote, default true.


Logging and Debugging Properties
---
All of these properties are written to the `LogFile` whenever they are changed, except for the cases set by `LogStates` and `LogPrompts`.

`Result`
The result of a method, usually represents any error in execution of a method. Always set either by the instance of BCI2000Remote it is a member of, and reset whenever `Execute()` is called.

`Sending`
The command being sent to the operator.

`Received`
The received data from the operator after a command is sent, as a string. Set whenever `Execute()` is called.

`Response`
The received data after it has been processed by BCI2000Remote. Generally only for internal use.


Methods
---
All methods return a boolean representing their successful execution, while any data output is handled by passing in a reference.

`Connect()`
Attempts to connect to a running operator at `TelnetIp:TelnetPort`, then attempts to start the operator at `OperatorPath`, and connect at `127.0.0.1:TelnetPort`.

`Disconnect()`
Terminates operator if it was started by a previous `Connect()` call, then closes tcp connection.

`Connected()`
Returns whether `BCI2000Remote` is connected to the operator.

`StartupModules(Dictionary<string module, List<string> arguments>)`
Starts up modules in the Operator's directory.
Takes a dictionary of type <string, List<string>>, with the keys being the module name, and the values being whatever command line arguments are being passed to the module. Automatically appends "--" to the beginning of arguments and "--local" to the list of arguments.

`LoadParametersRemote(string path)`
Loads parameters from a file on the Operator's machine.

`LoadParametersLocal(string path)`
Loads parameters from a local parameter file
Might not work at the moment, recommended to use `LoadParametersRemote` if running operator locally.

`SetParameter(string name, string value)`
Sets a parameter to a value.

`GetParameter(string name, ref string value)`
Gets a parameter's value, and stores it in the given string reference.

`Start()`
Starts a new run of the operator.

`Stop()`
Stops the operator if it is running.

`AddStateVariable(string name, int bitWidth, double startingValue)`
Adds a state variable.

`SetStateVariable(string name, double value)`
Sets a state variable.

`GetStateVariable(string name, ref double value)`
Gets a state variable's value and stores it at the given reference.

`GetSystemState(ref string state)`
Gets the system's state, and stores it at the given reference.

`Execute(string command, optional ref int outCode)`
Executes a given command. The outCode is used when a command returns a value that is easily interpretable as true or false, in which case it will be 1 for true and 0 for false, or -1 for anything not interpretable. If the command returns a number for an exit code, the outCode will be 0. This is reversed from the original BCI2000Remote due to BCI2000RemoteNET using 1 to represent true and success, and to avoid confusion about 0 being true. If a command needs to return something other than an exit code, don't use outCode, as the response will be stored at `Received` or `Response`. In most cases the override `Execute(string command)` will be used.