using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;

namespace Webrella.ClientInterface.Networking
{
	class NetLibEndpoint : INetworkEndpoint
	{
		/// <summary>The connection manager, see <see cref="LiteNetLib"/>.</summary>
		NetManager Mgr;
		/// <summary>Component for registering packet handlers, see <see cref="LiteNetLib"/>.</summary>
		EventBasedNetListener Comm;
		const DeliveryMethod Method = DeliveryMethod.ReliableUnordered;
		Action<string> Logger;
		/// <summary>Access key, filter connection attempts at library level.</summary>
		string AccessKey;
		/// <summary>Remote host.</summary>
		NetPeer remote;

		/// <summary>Registered callbacks, by reference (channel number).</summary>
		Dictionary<int, NEPCallback> DataCallbacks;
		/// <summary>To synchronize requests by reference.</summary>
		Dictionary<int, PeerWaiter> Waiter;
		/// <summary>Thread synchronization for <see cref="Waiter"/>.</summary>
		Dictionary<int, object> SyncRoots;

		/// <summary>Represents a callback wait.</summary>
		class PeerWaiter
		{
			public int Num;
			public byte[] Data;
			public AutoResetEvent Barrier;
		}

		public NetLibEndpoint(Action<string> Logger)
		{
			Comm = new EventBasedNetListener();
			Comm.NetworkReceiveEvent += NetworkReceive;
			Comm.ConnectionRequestEvent += ConnRequest;
			Mgr = new NetManager(Comm);
			Mgr.NatPunchEnabled = true;
			Mgr.PingInterval = 1000;
			DataCallbacks = new Dictionary<int, NEPCallback>();
			Waiter = new Dictionary<int, PeerWaiter>();
			SyncRoots = new Dictionary<int, object>();
			this.Logger = Logger;
		}

		/// <summary>
		/// Connects to <paramref name="Host"/> at <paramref name="Port"/> with <paramref name="Key"/>.
		/// </summary>
		/// <param name="Host">Remote host address.</param>
		/// <param name="Port">Remote host port.</param>
		/// <param name="Key">Encryption key.</param>
		public void Connect(string Host, int Port, string Key)
		{
			Mgr.Start(); 
			remote = Mgr.Connect(Host, Port, Key);
			Thread th = new Thread(this.SpinPoll);
			th.Start();
		}

		/// <summary>
		/// Listen on specified <paramref name="Port"/> with the given encryption <paramref name="Key"/>.
		/// </summary>
		/// <param name="Port">Local listening port.</param>
		/// <param name="Key">Encryption key.</param>
		public void Listen(int Port, string Key)
		{
			AccessKey = Key; 
			Mgr.Start(Port);
			Thread th = new Thread(this.SpinPoll);
			th.Start();
		}

		/// <summary>Poll method for the <see cref="LiteNetLib"/> events.</summary>
		void SpinPoll()
		{ while (true) Mgr.PollEvents(); }

		void ConnRequest(ConnectionRequest request) => request.AcceptIfKey(AccessKey);

		public void RegisterCallback(int Reference, NEPCallback Callback)
		{ DataCallbacks.Add(Reference, Callback); }

		public void UnregisterCallback(int Reference)
		{ DataCallbacks.Remove(Reference); }

		/// <summary>
		/// Send followed by receive.
		/// </summary>
		/// <param name="Reference">Data reference.</param>
		/// <param name="Data">Data array.</param>
		/// <param name="Count">Amount of bytes from the data array to send.</param>
		public byte[] SendReceive(int Reference, byte[] Data, int Count)
		{
			object lockroot;
			lock (SyncRoots)
				if (!SyncRoots.ContainsKey(Reference))
					SyncRoots.Add(Reference, lockroot = new object());
				else lockroot = SyncRoots[Reference];

			lock (lockroot)
			{
				/* Create waiter */
				PeerWaiter pw = new PeerWaiter() { Num = Reference, Data = Data, Barrier = new AutoResetEvent(false) };
				lock (Waiter) Waiter.Add(Reference, pw);

				byte[] DtS = new byte[Count + 4];
				DtS[0] = (byte)(Reference % 256);
				DtS[1] = (byte)(Reference / 256 % 256);
				DtS[2] = (byte)(Reference / 65536 % 256);
				DtS[3] = (byte)(Reference / 16777216 % 256);
				Buffer.BlockCopy(Data, 0, DtS, 4, Count);
				remote.Send(DtS, Method);

				/* Wait for reply */
				pw.Barrier.WaitOne();
				lock (Waiter) Waiter.Remove(Reference);
				pw.Barrier.Dispose();
				return pw.Data;
			}
		}

		/// <summary>
		/// Receives data from the network and handles it appropriately.
		/// </summary>
		/// <param name="peer">Peer.</param>
		/// <param name="reader">Reader.</param>
		/// <param name="deliveryMethod">Delivery method.</param>
		void NetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
		{
			if (deliveryMethod != Method)
				Logger("Wrong delivery method from peer " + peer.EndPoint.ToString() + ".");

			int Reference = reader.GetByte() + reader.GetByte() * 256 + reader.GetByte() * 65536 + reader.GetByte() * 16777216;
			remote = peer;

			/* Is a reply? */
			bool IsPWaited;
			PeerWaiter Wtr = null;
			lock (Waiter)
				if (Waiter.ContainsKey(Reference))
				{
					IsPWaited = true;
					Wtr = Waiter[Reference];
				}
				else IsPWaited = false;

			/* Pass back reply */
			if (IsPWaited)
			{
				Wtr.Data = reader.GetRemainingBytes();
				Wtr.Barrier.Set();
				return;
			}

			/* Handle request */
			byte[] Resp = DataCallbacks[Reference](reader.RawData, reader.UserDataOffset + 4, reader.UserDataSize - 4, out int Of, out int Oc);
			byte[] DtS = new byte[Resp.Length + 4];
			DtS[0] = (byte)(Reference % 256);
			DtS[1] = (byte)(Reference / 256 % 256);
			DtS[2] = (byte)(Reference / 65536 % 256);
			DtS[3] = (byte)(Reference / 16777216 % 256);
			Buffer.BlockCopy(Resp, Of, DtS, 4, Oc);
			peer.Send(DtS, Method);
		}


	}
}
