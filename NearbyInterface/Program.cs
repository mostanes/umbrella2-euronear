using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Umbrella2.Pipeline.ViaNearby
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
		}

		public static List<System.Reflection.Assembly> GetAssemblies()
		{
			List<System.Reflection.Assembly> asm = new List<System.Reflection.Assembly>(AppDomain.CurrentDomain.GetAssemblies());
			int i;
			for (i = 0; i < asm.Count; i++)
			{
				asm.AddRange(asm[i].GetReferencedAssemblies().Where((x) => !asm.Any((System.Reflection.Assembly y) => y.GetName().Name == x.Name)).Select((x) => System.Reflection.Assembly.Load(x)));
			}
			return asm;
		}
	}
}
