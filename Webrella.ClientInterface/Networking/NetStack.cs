using System;
using System.Collections.Generic;

namespace Webrella.ClientInterface.Networking
{
	public partial class NetStack
	{
		/// <summary>The communication layer.</summary>
		NetLibEndpoint Endpoint;
		/// <summary>Control channel handle.</summary>
		EncryptWrapper ControlChannel;
		/// <summary>Open channels handles.</summary>
		Dictionary<int, EncryptWrapper> DataChannels;
		/// <summary>Control channel IV.</summary>
		static byte[] IV0 = System.Text.Encoding.UTF8.GetBytes("WebrellaNetStack");
		/// <summary>Encryption key used in the connections.</summary>
		byte[] Key;
		Action<string> Log;
		/// <summary>Channel ID generator.</summary>
		private int ChannelCounter;

		public NetStack(Action<string> Logger)
		{
			Log = Logger;
			ChannelCounter = 10;
			DataChannels = new Dictionary<int, EncryptWrapper>();
		}

		/// <summary>
		/// Connects to the remote <paramref name="Host"/> on the given <paramref name="Port"/>, using the given keys.
		/// </summary>
		/// <param name="Host">Remote host.</param>
		/// <param name="Port">Remote port.</param>
		/// <param name="NEKey">Non-encryption key. Used for fast filtering on the remote host.</param>
		/// <param name="EKey">Encryption key.</param>
		public void EstablishConnection(string Host, int Port, string NEKey, byte[] EKey)
		{
			Key = (byte[])EKey.Clone();
			Endpoint = new NetLibEndpoint(Log);
			Endpoint.Connect(Host, Port, NEKey);
			string Message = SendReceiveText(Endpoint, 0, "WNS-C");
			if (Message != "WNS-S")
				throw new System.Net.ProtocolViolationException("Remote host does not follow Webrella Network Stack");
			ControlChannel = new EncryptWrapper(Endpoint, EKey, IV0);
			ControlChannel.RegisterCallback(1, ControlReply);

			SendHandshake();
		}

		/// <summary>
		/// Listens on the give <paramref name="Port"/> with the given keys.
		/// </summary>
		/// <param name="Port">Local listening port.</param>
		/// <param name="NEKey">Non-encryption key. Used for fast filtering incoming connections.</param>
		/// <param name="EKey">Encryption key.</param>
		public void CreateListener(int Port, string NEKey, byte[] EKey)
		{
			Key = EKey;
			Endpoint = new NetLibEndpoint(Log);
			Endpoint.RegisterCallback(0, NSReply0);
			ControlChannel = new EncryptWrapper(Endpoint, EKey, IV0);
			ControlChannel.RegisterCallback(1, ControlReply);

			Endpoint.Listen(Port, NEKey);
		}

		/// <summary>
		/// Registers a channel handler.
		/// </summary>
		/// <param name="Reference">Reference (channel number).</param>
		/// <param name="Callback">Handler.</param>
		public void RegisterChannel(int Reference, NEPCallback Callback)
		{
			lock (DataChannels)
				DataChannels[Reference].RegisterCallback(Reference, Callback);
		}

		/// <summary>
		/// Creates a new channel.
		/// </summary>
		/// <returns>The new channel's number.</returns>
		/// <param name="Endpoint">The new channel.</param>
		public int CreateNewChannel(out INetworkEndpoint Endpoint)
		{
			int ChNum;
			lock (this)
				ChNum = ChannelCounter++;

			Endpoint = CreateChannel(ChNum);
			return ChNum;
		}

		private INetworkEndpoint CreateChannel(int Reference)
		{
			SendOpenReference(Reference);
			lock (DataChannels)
				return DataChannels[Reference];
		}

		public INetworkEndpoint OpenChannel(int Reference)
		{ lock (DataChannels) return DataChannels[Reference]; }

	}
}
