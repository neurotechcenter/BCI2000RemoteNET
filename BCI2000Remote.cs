/////////////////////////////////////////////////////////////////////////////
/// This file is a part of BCI2000RemoteNET, a library
/// for controlling BCI2000 <http://bci2000.org> from .NET programs.
///
///
///
/// BCI20000RemoteNET is free software: you can redistribute it
/// and/or modify it under the terms of the GNU General Public License
/// as published by the Free Software Foundation, either version 3 of
/// the License, or (at your option) any later version.
///
/// This program is distributed in the hope that it will be useful,
/// but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
/// GNU General Public License for more details.
/// 
/// You should have received a copy of the GNU General Public License
/// along with this program.  If not, see <http://www.gnu.org/licenses/>.
///////////////////////////////////////////////////////////////////////////

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BCI2000RemoteNET {
    class BCI2000Remote {
	//Size of the input read buffer. Should be larger than the largest possible response from BCI2000.
	private const int READ_BUFFER_SIZE = 2048;

	///<summary>
	/// Timeout value (in milliseconds) of connection to BCI2000
	///</summary>
	public int Timeout{ get; set; } = 1000;

	///<summary>
	/// Terminate operator when this object is deleted
	/// </summary>
	public bool TerminateOperatorOnDisconnect { get; set; } = true;

	private string windowTitle = "";
	///<summary>
	/// The title of the BCI2000 window
	///</summary>
	public string WindowTitle {
	    get {
		return windowTitle;
	    }
	    set {
		windowTitle = value;
		if (Connected()) {
		    Execute($"set title \"{windowTitle}\"");
		}
	    }
	}

	private bool hideWindow;
	///<summary>
	/// Hide the BCI2000 window
	///</summary>
	private bool HideWindow {
	    get {
		return hideWindow;
	    }
	    set {
		hideWindow = value;
		if (Connected()) {
		    switch (hideWindow) {
			case false:
			    Execute("show window");
			    break;
			case true:
			    Execute("hide window");
			    break;
		    }
		}
	    }
	}


	~BCI2000Remote() {
	    Disconnect();
	}

	///<summary>
	///Disconnects from the operator. Terminated the operator if <see cref="TerminateOperatorOnDisconnect"/> is set.
	///</summary>
	public void Disconnect() {
	    if (TerminateOperatorOnDisconnect && Connected()) {
		Quit();
	    }
	    if (connection != null) {
		connection.Close();
		connection = null; //This might be redundant
	    }
	}

	///<summary>
	/// Starts an instance of the BCI2000 Operator on the local machine.
	///</summary>
	///<param name="operatorPath">The location of the operator binary</param>
	///<param name="address"> The address on which the Operator will listen for input. Leave as default if you will only connect from the local system.
	/// Note on security: BCI2000Remote uses an unencrypted, unsecured telnet connection. Anyone who can access the connection can run BCI2000 shell scripts. 
	/// As such, avoid leaving BCI2000 open with its telnet port open unsupervised if it is set to an adress other than localhost (127.0.0.1),
	/// and take caution when exposing the BCI2000 telnet interface on a port forwarded outside of your local network.
	///</param>
	///<param name="port"> The port on which the Operator will listen for input. Leave as default unless a specific port is needed.</param>
	public void StartOperator(string operatorPath, string address = "127.0.0.1", int port = 3999) {
	    if (port < 0 || port > 65535) {
		throw new BCI2000ConnectionException($"Port number {port} is not valid");
	    }
	    IPAddress addr = IPAddress.Parse(address);
	    connection = new TcpClient();
	    try {
		connection.Connect(address, port);
		connection.Close();
		connection = null;
		throw new BCI2000ConnectionException($"There is already something running at {address}:{port}, is BCI2000 already running?");
	    } catch (SocketException) {
		//Socket should not connect if BCI2000 is not already running
	    }
	    connection = null;

	    StringBuilder arguments = new StringBuilder();
	    arguments.Append($" --Telnet \"{addr.ToString()}:{port}\" ");
	    arguments.Append(" --StartupIdle ");
	    if (!string.IsNullOrEmpty(WindowTitle)) {
		arguments.Append($" --Title \"{WindowTitle}\" ");
	    }
	    if (HideWindow) {
		arguments.Append(" --Hide ");
	    }
	    try {
		System.Diagnostics.Process.Start(operatorPath, arguments.ToString());
	    } catch (Exception ex) {
		throw new BCI2000ConnectionException($"Could not start operator at path {operatorPath}: {ex.ToString()}");
	    }
	}

	///<summary>
	///Establishes a connection to an instance of BCI2000 running at the specified address and port.
	///</summary>
	///<param name="address">The IPv4 address to connect to. Note that this may not necessarily be the same as the one used in <see cref="StartOperator">StartOperator</see>, even if running BCI2000 locally. For example, if the operator was started on the local machine with address <c>0.0.0.0</c>, you would connect to it at address <c>127.0.0.1</c></param>
	///<param name="port">The port on which BCI2000 is listening. If BCI2000 was started locally with <see cref="StartOperator">StartOperator</see>, this must be the same value.</param>
	public void Connect(string address = "127.0.0.1", int port = 3999) {
	    if (port < 0 || port > 65535) {
		throw new BCI2000ConnectionException($"Port number {port} is not valid");
	    }
	    IPAddress addr = IPAddress.Parse(address);

	    if (Connected()) {
		throw new BCI2000ConnectionException("Connect() called while already connected. Call Disconnect() first.");
	    }
	    if (connection != null) {
		throw new BCI2000ConnectionException("Connect called while connection is null. This should not happen and is likely a bug. Please report to the maintainer.");
	    }
	    connection = new TcpClient();
	    try {
		connection.Connect(addr, port);
	    } catch (Exception ex) {
		throw new BCI2000ConnectionException($"Could not connect to operator at {addr.ToString()}:{port}, {ex.ToString()}");
	    }

	    op_stream = connection.GetStream();

	    connection.SendTimeout = Timeout;
	    connection.ReceiveTimeout = Timeout;

	    Execute("change directory $BCI2000LAUNCHDIR");
	}

	///<summary>
	///Gets whether or not this BCI2000Remote instance is currently connected to the BCI2000 Operator
	///</summary>
	///<returns>Whether or not this object is currently connected to BCI2000</returns>
	public bool Connected() {
	    return connection?.Connected ?? false;
	}

	///<summary>
	///Shuts down the connected BCI2000 instance
	///</summary>
	public void Quit() {
	    Execute("Quit");
	}

	///<summary>
	/// BCI2000 Operator states of operation, as documented on the <anchor xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="https://www.bci2000.org/mediawiki/index.php/User_Reference:Operator_Module_Scripting#WAIT_FOR_%3Csystem_state%3E_[%3Ctimeout_seconds%3E]">BCI2000 Wiki</anchor>
	/// </summary>
	public enum SystemState {
	    Idle,
	    Startup,
	    Connected,
	    Resting,
	    Suspended,
	    ParamsModified,
	    Running,
	    Termination,
	    Busy
	}
	///<summary>
	///Waits for the system to be in the specified state.
	///This will block until the system is in the specified state.
	///</summary>
	///<param name="timeout">The timeout value (in seconds) that the command will wait before failing. Leave as null to wait indefinitely.</param>
	///<returns>True if the system state was reached within the timeout time.</returns>
	public bool WaitForSystemState(SystemState state, double? timeout = null) {
	    return Execute<bool>($"wait for {nameof(state)} {timeout?.ToString() ?? ""}");
	}


	///<summary>
	///Executes the given command and returns the result as type <typeparamref name="T"/>. Throws if the response cannot be parsed as <typeparamref name="T"/>. If you are trying to execute a command which does not produce output, use <see cref="Execute(string, bool)"/>.
	///</summary>
	///<typeparam name="T">Type of the result of the command. Must implement <see cref="IParsable{TSelf}"/>.</typeparam> 
	///<param name="command">The command to execute</param>
	public T Execute<T>(string command) where T : IParsable<T> {
	    if (!Connected()) {
		throw new BCI2000ConnectionException("No connection to BCI2000 Operator");
	    }
	    return GetResponseAs<T>();
	}

	///<summary>
	///Executes the given command. Will throw if a non-blank response is received from BCI2000 and <paramref name="expectEmptyResponse"/> is not set to false. 
	///</summary>
	///<param name="command">The command to send to BCI2000</param>
	///<param name="expectEmptyResponse">By default, this function will throw if its command receives a non-empty response from BCI2000. This is because most BCI2000 commands which do not return a value will not send a response if they succeed. If set to false, this function will acceept non-empty responses from BCI2000.
	public void Execute(string command, bool expectEmptyResponse = true) {
	    if (!Connected()) {
		throw new BCI2000ConnectionException("No connection to BCI2000 Operator");
	    }
	    if (expectEmptyResponse) {
		ExpectEmptyResponse();
	    } else {
		DiscardResponse();
	    }
	}

	//Sends command to BCI2000
	private void SendCommand(string command){
	    try {
		op_stream.Write(System.Text.Encoding.ASCII.GetBytes(command + "\r\n"));
	    } catch (Exception ex) {
		throw new BCI2000ConnectionException($"Failed to send command to operator, {ex}");
	    }
	}

	//Gets the response from the operator and attempts to parse into the given type
	private T GetResponseAs<T>() where T : IParsable<T> {
	    string resp = ReceiveResponse();
	    try {
		T result = T.Parse(resp, null);
		return result;
	    } catch (Exception ex) {
		throw new BCI2000CommandException($"Could not parse response {resp} as type {nameof(T)}, {ex}");
	    }

	}

	//Receives response from operator and throws if response is not blank. Used with commands which expect no response, such as setting events and parameters.
	private void ExpectEmptyResponse() { 
	    string resp = ReceiveResponse();
	    if (!string.IsNullOrWhiteSpace(resp)) {
		throw new BCI2000CommandException($"Expected empty response but received {resp} instead");
	    }
	}

	//Receives response and discards the result.
	private void DiscardResponse() {
	    ReceiveResponse();
	}

	private byte[] recv_buffer = new byte[READ_BUFFER_SIZE];
	//Receives response from the operator. Blocks until the prompt character ('>') is received.
	private string ReceiveResponse() {
	    StringBuilder response = new StringBuilder();
	    while (true) {
		int read = op_stream.Read(recv_buffer, 0, recv_buffer.Length);
		if (read > 0) { 
		    string resp_fragment = System.Text.Encoding.ASCII.GetString(recv_buffer, 0, read);
		    if (EndsWithPrompt(resp_fragment) && !op_stream.DataAvailable) {
			//Stop reading if previous response ended with prompt and no data is available
			break;
		    } else {
			response.Append(resp_fragment);
		    }
		}
	    }
	    return response.ToString();
	}

        private bool EndsWithPrompt(string line)
        {
            string lineTrim = line.ToString().Trim();
            if (lineTrim.Length == 0) return false;
            return lineTrim.Substring(lineTrim.Length - 1).Equals(Prompt);
        }

	private TcpClient connection;
	private NetworkStream op_stream;

        private const string ReadlineTag = "\\AwaitingInput:";
        private const string AckTag = "\\AcknowledgedInput";
        private const string ExitCodeTag = "\\ExitCode";
        private const string TerminationTag = "\\Terminating";
        private const string Prompt = ">";
    }
}
