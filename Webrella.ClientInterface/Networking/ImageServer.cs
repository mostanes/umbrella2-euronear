using System;
using System.Collections.Generic;
using Umbrella2.Pipeline.Serialization.Remoting;

namespace Webrella.ClientInterface.Networking
{
	/// <summary>
	/// Performs conversion of commands between the <see cref="LocalImageWrapper"/> and the network stack.
	/// </summary>
	class ImageServer
	{
		LocalImageWrapper LocWrap;
		/// <summary>Serializers set for multi-threaded operation.</summary>
		Stack<Ceras.CerasSerializer> Serializers;

		public ImageServer(LocalImageWrapper Wrapper)
		{ LocWrap = Wrapper; Serializers = new Stack<Ceras.CerasSerializer>(); }

		public byte[] ServeImage(byte[] Request, int IOffset, int ICount, out int OOffset, out int OCount)
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

			byte[] CerasBuffer = null;

			RemoteData RD = null;
			int Off = IOffset;
			cser.Deserialize(ref RD, Request, ref Off);
			if (Math.Abs(Off - IOffset - ICount) > 1)
				throw new System.Runtime.Serialization.SerializationException("Data size mismatch");
			LocWrap.Swap(RD);

			int NL = cser.Serialize(RD, ref CerasBuffer);

			OOffset = 0; OCount = NL;
			return CerasBuffer;
		}
	}
}
