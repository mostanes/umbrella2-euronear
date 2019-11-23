using System;
using System.Security.Cryptography;

namespace Webrella.ClientInterface.Networking
{
	/// <summary>
	/// Performs encryption of data passing through it. Uses AES.
	/// </summary>
	class EncryptWrapper : INetworkEndpoint
	{
		INetworkEndpoint Wrapped;
		Aes CryptoGen = Aes.Create();
		byte[] Key;
		byte[] IV;

		public EncryptWrapper(INetworkEndpoint Underlayer, byte[] Key, byte[] IV)
		{
			Wrapped = Underlayer;
			this.IV = IV;
			this.Key = Key;
			CryptoGen.Mode = CipherMode.CBC;
			CryptoGen.Padding = PaddingMode.PKCS7;
		}

		public void RegisterCallback(int Reference, NEPCallback Callback)
		{
			CallbackWrapper cbw = new CallbackWrapper() { Callback = Callback, Key = Key, IV = IV, CryptoGen = CryptoGen };
			Wrapped.RegisterCallback(Reference, cbw.Wrapper);
		}

		public byte[] SendReceive(int Reference, byte[] Data, int Count)
		{

			ICryptoTransform Enc = CryptoGen.CreateEncryptor(Key, IV);
			ICryptoTransform Dec = CryptoGen.CreateDecryptor(Key, IV);

			byte[] Req = Enc.TransformFinalBlock(Data, 0, Count);
			byte[] Rep = Wrapped.SendReceive(Reference, Req, Req.Length);
			return Dec.TransformFinalBlock(Rep, 0, Rep.Length);
		}

		public static byte[] RequestIV()
		{
			RNGCryptoServiceProvider rcsp = new RNGCryptoServiceProvider();
			byte[] IV = new byte[16];
			rcsp.GetBytes(IV);
			return IV;
		}

		public void UnregisterCallback(int Reference)
		{ Wrapped.UnregisterCallback(Reference); }

		class CallbackWrapper
		{
			internal NEPCallback Callback;
			internal Aes CryptoGen;
			internal byte[] Key;
			internal byte[] IV;

			internal byte[] Wrapper(byte[] Data, int IOffset, int ICount, out int OOffset, out int OCount)
			{
				ICryptoTransform Enc = CryptoGen.CreateEncryptor(Key, IV);
				ICryptoTransform Dec = CryptoGen.CreateDecryptor(Key, IV);
				byte[] Dt = Dec.TransformFinalBlock(Data, IOffset, ICount);
				byte[] Resp = Callback(Dt, 0, Dt.Length, out int Of, out int Oc);
				byte[] Ec = Enc.TransformFinalBlock(Resp, Of, Oc);
				OOffset = 0; OCount = Ec.Length;
				return Ec;
			}
		}
	}
}
