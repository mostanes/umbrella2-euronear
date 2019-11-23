using System;
using System.Collections.Generic;
using Umbrella2.Pipeline.Serialization.Remoting;

namespace Webrella.ClientInterface.Networking
{
	/// <summary>
	/// Performs conversion of commands from the image surrogate form to the network stack form.
	/// </summary>
	class ImageClient : IFitsRemoteChannel
	{
		Stack<byte[]> CerasBuffers;
		/// <summary>Serializers set for multi-threaded operation.</summary>
		Stack<Ceras.CerasSerializer> Serializers;
		INetworkEndpoint Network;

		public ImageClient(INetworkEndpoint Endpoint)
		{ Network = Endpoint; Serializers = new Stack<Ceras.CerasSerializer>(); }

		public void Swap(RemoteData RD)
		{
			Ceras.CerasSerializer cser;

			lock (Serializers)
				if (Serializers.Count == 0)
				{
					Ceras.SerializerConfig Config = new Ceras.SerializerConfig();
					Config.VersionTolerance.Mode = Ceras.VersionToleranceMode.Standard;
					Ceras.CerasSerializer Ser = new Ceras.CerasSerializer(Config);
					cser = Ser;
				}
				else cser = Serializers.Pop();

			byte[] CerasBuffer;
			lock (CerasBuffers)
				if (CerasBuffers.Count == 0) CerasBuffer = null;
				else CerasBuffer = CerasBuffers.Pop();

			int Len = cser.Serialize(RD, ref CerasBuffer);

			int Ref = RD.Reference;

			byte[] Reply = Network.SendReceive(Ref, CerasBuffer, Len);

			cser.Deserialize(ref RD, Reply);

			lock (Serializers)
				Serializers.Push(cser);
			lock (CerasBuffers)
				CerasBuffers.Push(CerasBuffer);
		}
	}
}
