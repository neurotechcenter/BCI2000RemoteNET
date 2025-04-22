using System;
using System.Net;
using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using System.Linq;


namespace BCI2000RemoteNET {

///
/// Implements BCI2000 time synchronization protocol. 
/// A single message consists of the magic number 0xbc12bc12, 
/// the message type,
/// and zero, one, three, or five unsigned big-endian 64 bit integers, representing time since system start.
///
class Synchronization {

	private const uint sync_magic = 0xbc12bc12;
	private static byte[] sync_magic_bytes = HostToNetF(BitConverter.GetBytes(sync_magic));

	public static UInt16 ListenPort = 12122;
	private static UdpClient listener = new UdpClient(ListenPort);


	enum MessageType : byte {
		Req = 01,
		Resp = 02,
	}


	public static long Synchronize(UdpClient conn, Stopwatch timer, int timeout, int attempts) {
		var syncs = new (long, long, long)[attempts];
		for (int i = 0; i < attempts; i++) {
			syncs[i] = SyncOnce(conn, timer, timeout);
		}

		//Get offset with smallest delta between the two latencies.
		long offset = syncs
			.Select<(long offset, long lat1, long lat2), (long, long)>(
					(t) => (t.offset, Math.Abs(t.lat1 - t.lat2))
					)
			.Aggregate<(long offset, long d_lat)>((t, t2) => t2.d_lat < t.d_lat ? t2 : t)
			.offset;


		return offset;
	}

	///
	/// Does a single synchonization operation, and return the calculated time offset, and calculated latency for both directions
	///
	private static (long, long, long) SyncOnce(UdpClient conn, Stopwatch timer, int timeout) {
		var (_, _, t1, t1p) = ReqResp(conn, timeout, timer);
		var (t2, t2p, _, _) = ReqResp(conn, timeout, timer);

		long offset = (t1p - t1 - t2p + t2)/2;

		return (offset, t1p - t1 - offset, t2p - t2 + offset);
	}


	private static (long, long, long, long) ReqResp(UdpClient conn, int timeout, Stopwatch timer) {
		byte[] reqMsg = new byte[4 + 1 + 2];	
		BufCpy(sync_magic_bytes, reqMsg, 0, 4);
		reqMsg[4] = (byte)MessageType.Req;
		byte[] curPort = BitConverter.GetBytes(ListenPort);
		HostToNet(curPort);
		BufCpy(curPort, reqMsg, 5, 2);
		

		long t_send = TimeSpanToNanos(timer.Elapsed);
		conn.Send(reqMsg, 7);

		var respTask = listener.ReceiveAsync();

		if (!respTask.Wait(timeout)) {
			throw new BCI2000ConnectionException("Timed out waiting for BCI2000 time server");
		}

		byte[] resp = respTask.Result.Buffer;
		long t_recv = TimeSpanToNanos(timer.Elapsed);
		if (resp.Length != 4 + 1 + 8 + 8) {
			throw new BCI2000ConnectionException($"Expected response of length {4+1+8+8} but received response of length {resp.Length}");
		}
		if (resp[0..4].Equals(sync_magic_bytes)) {
			throw new BCI2000ConnectionException($"Expected {BitConverter.ToString(sync_magic_bytes)} at index 0, instead received {BitConverter.ToString(resp[0..4])}");
		}
		if (resp[4] != (byte)MessageType.Resp) {
			throw new BCI2000ConnectionException($"Expected 0x02 at index 4, instead received {resp[5]}");
		}
		long t_other_recv = ParseTimeAt(resp, 5);
		long t_other_send = ParseTimeAt(resp, 5 + 8); 

		return (t_send, t_other_recv, t_other_send, t_recv);
	}

	private static void BufCpy(byte[] src, byte[] dest, int offset, int size) {
		for (int i = 0; i < size; i++) {
			dest[offset + i] = src[i];
		}
	}

	private static long ParseTimeAt(byte[] src, int offset) {
		byte[] nanos = src[offset..(offset+8)];
		
		if (BitConverter.IsLittleEndian) {
			Array.Reverse(nanos);
		}

		return BitConverter.ToInt64(nanos);
	}

	private static void WriteTimeAt(long time, byte[] dest, int offset) {
		byte[] nanos = BitConverter.GetBytes(time);

		if (BitConverter.IsLittleEndian) {
			Array.Reverse(nanos);
		}

		BufCpy(nanos, dest, offset, 8);
	}

	private static void HostToNet(byte[] val) {
		if (BitConverter.IsLittleEndian) {
			Array.Reverse(val);
		}
	}

	private static byte[] HostToNetF(byte[] val) {
		byte[] cp = (byte[]) val.Clone();
		if (BitConverter.IsLittleEndian) {
			Array.Reverse(cp);
		}
		return cp;
	}


	private static long TimeSpanToNanos(TimeSpan span) {
		long nanos = (span.Ticks * 100);
		return nanos;
	}


	



	private struct SyncResult {
		long offset;
		long latency1;
		long latency2;

		public SyncResult(long offset, long latency1, long latency2) {
			this.offset = offset;
			this.latency1 = latency1;
			this.latency2 = latency2;
		}
	}

}

}
