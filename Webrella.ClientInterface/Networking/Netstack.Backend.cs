using System;
namespace Webrella.ClientInterface.Networking
{
	public partial class NetStack
	{
		/// <summary>Send a handshake to check the channel.</summary>
		void SendHandshake()
		{
			int HT = (int)MessageType.Handshake;
			byte[] Hdata = new byte[4] { (byte)(HT % 256), (byte)(HT / 256 % 256), (byte)(HT / 65536 % 256), (byte)(HT / 16777216 % 256) };
			byte[] Reply = ControlChannel.SendReceive(1, Hdata, 4);
			if (!((Reply[0] == Hdata[0]) & (Reply[1] == Hdata[1]) & (Reply[2] == Hdata[2]) & (Reply[3] == Hdata[3])))
				throw new System.Net.ProtocolViolationException("Handshake failed");
		}

		/// <summary>Sends an open reference request to the remote peer and creates the associated channel.</summary>
		void SendOpenReference(int Reference)
		{
			int RT = (int)MessageType.OpenReference;
			byte[] data = new byte[8] { (byte)(RT % 256), (byte)(RT / 256 % 256), (byte)(RT / 65536 % 256), (byte)(RT / 16777216 % 256),
				(byte)(Reference % 256), (byte)(Reference / 256 % 256), (byte)(Reference / 65536 % 256), (byte)(Reference / 16777216 % 256) };
			byte[] IV = ControlChannel.SendReceive(1, data, 8);
			CreateChannel(Reference, IV);
		}

		/// <summary>Sends text on a given channel.</summary>
		string SendReceiveText(INetworkEndpoint Channel, int Reference, string Message)
		{
			byte[] Data = System.Text.Encoding.UTF8.GetBytes(Message);
			Data = Channel.SendReceive(Reference, Data, Data.Length);
			return System.Text.Encoding.UTF8.GetString(Data);
		}

		/// <summary>Reply message for initial handshake.</summary>
		byte[] NSReply0(byte[] IMsg, int IOf, int IOc, out int OOf, out int OOc)
		{
			string Message = System.Text.Encoding.UTF8.GetString(IMsg, IOf, IOc);
			OOf = 0;
			OOc = 0;
			if (Message != "WNS-C")
				return new byte[0];
			byte[] Data = System.Text.Encoding.UTF8.GetBytes("WNS-S");
			OOc = Data.Length;
			return Data;
		}

		/// <summary>Handler for messages on the control channel.</summary>
		byte[] ControlReply(byte[] IMsg, int IOf, int IOc, out int OOf, out int OOc)
		{
			int MsgT = GetInt(IMsg, IOf);
			MessageType Type = (MessageType)MsgT;
			int Reference;
			switch (Type)
			{
				case MessageType.Handshake:
					OOf = IOf; OOc = IOc;
					return IMsg;
				case MessageType.OpenReference:
					byte[] IV = EncryptWrapper.RequestIV();
					Reference = GetInt(IMsg, IOf + 4);
					CreateChannel(Reference, IV);
					OOf = 0; OOc = IV.Length;
					return IV;
				case MessageType.CloseReference:
					Reference = GetInt(IMsg, IOf + 4);
					ControlChannel.UnregisterCallback(Reference);
					lock (DataChannels)
						DataChannels.Remove(Reference);
					OOf = IOf; OOc = IOc;
					return IMsg;
			}
			OOf = 0; OOc = 0;
			return new byte[0];
		}

		/// <summary>Creates a new channel in the network stack with a given encryption IV.</summary>
		void CreateChannel(int Reference, byte[] IV)
		{
			EncryptWrapper ew = new EncryptWrapper(Endpoint, (byte[])Key.Clone(), IV);
			lock (DataChannels)
				DataChannels.Add(Reference, ew);
		}

		/// <summary>Parses an int from the given array starting from given position (in little endian order).</summary>
		int GetInt(byte[] Arr, int Offset) =>
		 Arr[Offset + 0] + Arr[Offset + 1] * 256 + Arr[Offset + 2] * 65536 + Arr[Offset + 3] * 16777216;

	}
}
