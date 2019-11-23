namespace Webrella.ClientInterface.Networking
{
	/// <summary>Interface implemented by the layers of the network stack.</summary>
	public interface INetworkEndpoint
	{
		/// <summary>
		/// Send data and wait for response.
		/// </summary>
		/// <returns>The response.</returns>
		/// <param name="Reference">Reference (channel number).</param>
		/// <param name="Data">Request data.</param>
		/// <param name="Count">Length of the data.</param>
		byte[] SendReceive(int Reference, byte[] Data, int Count);
		/// <summary>
		/// Registers a callback on the given channel.
		/// </summary>
		/// <param name="Reference">Reference (channel number).</param>
		/// <param name="Callback">Callback to register.</param>
		void RegisterCallback(int Reference, NEPCallback Callback);
		/// <summary>Removes the callback on the given channel.</summary>
		void UnregisterCallback(int Reference);
	}

	/// <summary>
	/// Network endpoint callback.
	/// </summary>
	/// <param name="Input">Request data.</param>
	/// <param name="IOffset">Input data offset in the input array.</param>
	/// <param name="ICount">Input data length.</param>
	/// <param name="OOffset">Offset of the output data in the (returned) array.</param>
	/// <param name="OCount">Output data length.</param>
	/// <returns>Response data.</returns>
	public delegate byte[] NEPCallback(byte[] Input, int IOffset, int ICount, out int OOffset, out int OCount);

	/// <summary>Control channel message type.</summary>
	enum MessageType
	{
		Handshake,
		OpenReference,
		CloseReference
	}
}
