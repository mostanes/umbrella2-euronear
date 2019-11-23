using System;
using Umbrella2.IO;
using Umbrella2.Pipeline.Serialization.Remoting;

namespace Webrella.ClientInterface.Networking
{
	public static class SurrogatePassing
	{
		/// <summary>
		/// Generates the surrogate pairs for the <paramref name="Image"/>. Sets up the local wrapper on the given <paramref name="Stack"/>.
		/// </summary>
		/// <returns>The <see cref="SerializationSurrogate"/> that needs to be sent to the remote domain.</returns>
		public static SerializationSurrogate GenerateSurrogatePair(NetStack Stack, Image Image)
		{
			ImageServer ims = new ImageServer(new LocalImageWrapper(Image));
			int Channel = Stack.CreateNewChannel(out INetworkEndpoint nep);
			Stack.RegisterChannel(Channel, ims.ServeImage);
			SerializationSurrogate srg = new SerializationSurrogate(Channel, Image);
			return srg;
		}

		/// <summary>
		/// Unpacks the given <see cref="SerializationSurrogate"/> to the given network stack and returns the resulting surrogate.
		/// </summary>
		public static RemoteImageSurrogate UnpackSurrogate(NetStack Stack, SerializationSurrogate Surrogate)
		{
			INetworkEndpoint nep = Stack.OpenChannel(Surrogate.Reference);
			ImageClient imc = new ImageClient(nep);
			return Surrogate.GetRemoteSurrogate(imc);
		}
	}
}
