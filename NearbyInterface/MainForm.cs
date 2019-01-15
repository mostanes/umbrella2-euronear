using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Umbrella2.Pipeline.ViaNearby
{
	public partial class MainForm : Form
	{
		public MainForm()
		{
			InitializeComponent();
		}

		private void textBox1_Validating(object sender, CancelEventArgs e)
		{
			if (textBox1.Text.Length != 4) return;
			if (textBox1.Text[0] != 'E') return;
			label2.Text = "Night: " + textBox1.Text[1];
			label3.Text = "Field number: " + textBox1.Text.Substring(2);
		}


		private void MainForm_Load(object sender, EventArgs e)
		{
			LogLine("Umbrella2 NEARBY Interface");
			LogLine("Core", "Loading");
		}

		private void changeSettingsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PipelineConfig pconfig = new PipelineConfig();
			pconfig.Show();
		}

		public void LogLine(string Message)
		{ richTextBox1.Text += Message + "\n"; }

		public void LogLine(string Component, string Message, string Instance = null)
		{ Instance = Instance ?? string.Empty; LogLine(Component + " :" + Instance + "> " + Message); }

		private void button1_Click(object sender, EventArgs e)
		{

		}
	}
}
