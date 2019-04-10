using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Umbrella2.Pipeline.ViaNearby
{
	public partial class PipelineConfig : Form
	{
		FrontendConfig Config;
		StandardPipeline Pipeline;

		public PipelineConfig(FrontendConfig Configuration, StandardPipeline Pipeline)
		{
			InitializeComponent();
			Config = Configuration;
			this.Pipeline = Pipeline;
		}

		private void PipelineConfig_Load(object sender, EventArgs e)
		{
			textBox1.Text = Config.RootInputDir;
			textBox2.Text = Config.RootOutputDir;
			checkBox1.Checked = Config.LoadLast;
			checkBox2.Checked = Config.WatchDir;
			textBox3.Text = Config.Badpixel;

			propertyGrid1.SelectedObject = Pipeline;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(textBox1.Text)) folderBrowserDialog1.SelectedPath = textBox1.Text;
			folderBrowserDialog1.ShowDialog();
			textBox1.Text = folderBrowserDialog1.SelectedPath;
		}

		private void button2_Click(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(textBox2.Text)) folderBrowserDialog1.SelectedPath = textBox2.Text;
			folderBrowserDialog1.ShowDialog();
			textBox2.Text = folderBrowserDialog1.SelectedPath;
		}

		private void checkBox1_CheckedChanged(object sender, EventArgs e) => Config.LoadLast = checkBox1.Checked;

		private void checkBox2_CheckedChanged(object sender, EventArgs e) => Config.WatchDir = checkBox2.Checked;

		private void loadConfigurationFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			openFileDialog1.InitialDirectory = Environment.CurrentDirectory;
			openFileDialog1.ShowDialog();
			try
			{
				var ConfigSet = Configurator.ReadConfigFile(openFileDialog1.FileName);
				Configurator.ReadConfig(ConfigSet, Config);
			}
			catch (FormatException ex) { MessageBox.Show("Invalid configuration file", "ViaNearby configurator"); }
			PipelineConfig_Load(null, null);
		}

		private void saveConfigurationFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Dictionary<string, string> ConfigSet = new Dictionary<string, string>();
			Configurator.WriteConfig(ConfigSet, Config);
			saveFileDialog1.InitialDirectory = Environment.CurrentDirectory;
			saveFileDialog1.FileName = "config.txt";
			saveFileDialog1.ShowDialog();
			Configurator.WriteConfigFile(ConfigSet, saveFileDialog1.FileName);
		}

		private void textBox1_Validated(object sender, EventArgs e) => Config.RootInputDir = textBox1.Text;

		private void textBox2_Validated(object sender, EventArgs e) => Config.RootOutputDir = textBox2.Text;

		private void button3_Click(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(textBox3.Text)) folderBrowserDialog1.SelectedPath = textBox3.Text;
			folderBrowserDialog1.ShowDialog();
			textBox3.Text = folderBrowserDialog1.SelectedPath;
		}

		private void textBox3_Validated(object sender, EventArgs e) => Config.Badpixel = textBox3.Text;
	}
}
