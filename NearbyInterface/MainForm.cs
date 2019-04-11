using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Umbrella2.Visualizers.Winforms;

namespace Umbrella2.Pipeline.ViaNearby
{
	public partial class MainForm : Form
	{
		FrontendConfig Config;
		const string ConfigFile = "config.txt";
		StandardPipeline Pipeline;
		List<string>[] InputFiles;

		public MainForm()
		{
			InitializeComponent();
		}

		private void textBox1_Validating(object sender, CancelEventArgs e)
		{
			if (textBox1.Text.Length != 4) return;
			if (textBox1.Text[0] == 'e') textBox1.Text = "E" + textBox1.Text.Substring(1);
			if (textBox1.Text[0] != 'E') return;
			label2.Text = "Night: " + textBox1.Text[1];
			label3.Text = "Field number: " + textBox1.Text.Substring(2);
			textBox1.BackColor = System.Drawing.Color.LightGreen;
			textBox2.Text = Path.Combine(Config.RootInputDir, textBox1.Text);
			textBox3.Text = Path.Combine(Config.RootOutputDir, textBox1.Text);
		}


		private void MainForm_Load(object sender, EventArgs e)
		{
			LogLine("Umbrella2 NEARBY Interface");
			LogLine("Core", "Loading configuration");

			try
			{
				var ConfSet = Configurator.ReadConfigFile(ConfigFile);
				Config = Configurator.ReadConfig(ConfSet);
			}
			catch (Exception ex)
			{
				LogLine("Core", "Failed to load configuration file. Error follows:\n" + ex.ToString());
				Config = new FrontendConfig { LoadLast = false, RootInputDir = string.Empty, RootOutputDir = Path.GetTempPath(), WatchDir = false };
				LogLine("Core", "Using default configuration");
			}

			LogLine("Core", "Loaded configuration file");

			bool LoadedLast = false;
			if (Config.LoadLast)
			{
				if (TryLoadLast()) { LogLine("Core", "Found field not yet run"); LoadedLast = true; }
				else LogLine("Core", "All fields ran");
			}
			if (Config.WatchDir & !LoadedLast) { fileSystemWatcher1.Path = Config.RootInputDir; fileSystemWatcher1.EnableRaisingEvents = true; LogLine("Core", "Watching directory for changes"); }
			Pipeline = new StandardPipeline();
			textBox3.Text = Config.RootOutputDir;
			LogLine("Core", "Loading integrated plugins");
			foreach (System.Reflection.Assembly asm in Program.GetAssemblies())
				Plugins.LoadableTypes.RegisterNewTypes(asm.GetTypes());
			LogLine("Core", "Plugins loaded");
			LogLine("Core", "Initialized");
		}

		bool TryLoadLast()
		{
			if (!Directory.Exists(Config.RootInputDir)) LogLine("Autoload", "Input directory does not exist");
			if (!Directory.Exists(Config.RootOutputDir)) LogLine("Autoload", "Output directory does not exist");
			string[] InputDirs = Directory.GetDirectories(Config.RootInputDir);
			string[] OutputDirs = Directory.GetDirectories(Config.RootOutputDir);
			Array.Sort(InputDirs);
			Array.Sort(OutputDirs);
			int ocnt = 0;
			for (int i = 0; i < InputDirs.Length; i++)
			{
				string Idir = Path.GetFileName(InputDirs[i]);
				int Result = -1;
				if (Idir.Length == 4 && Idir[0] == 'E')
				{
					for (; ocnt < OutputDirs.Length; ocnt++)
					{
						string Odir = Path.GetFileName(OutputDirs[ocnt]);
						Result = string.Compare(Idir, Odir);
						if (Result == -1)
						{
							textBox1.Text = Idir;
							textBox2.Text = InputDirs[i];
							textBox3.Text = OutputDirs[i];
							return true;
						}
						if (Result == 0) break;
					}
				}
				if (Result == 0) continue;
				textBox1.Text = Idir;
				return true;
			}

			return false;
		}

		private void changeSettingsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PipelineConfig pconfig = new PipelineConfig(Config, Pipeline);
			pconfig.ShowDialog();
		}

		public void LogLine(string Message)
		{
			richTextBox1.SuspendLayout();
			if (richTextBox1.SelectionStart <= 0) richTextBox1.SelectionStart = richTextBox1.Text.Length;

			if (richTextBox1.SelectionStart >= richTextBox1.Text.Length - 1)
				richTextBox1.AppendText(Message + "\n");
			else
			{
				int cpos = richTextBox1.SelectionStart;
				int run = richTextBox1.SelectionLength;
				richTextBox1.AppendText(Message + "\n");
				richTextBox1.SelectionStart = cpos;
				richTextBox1.SelectionLength = run;
			}
			richTextBox1.ScrollToCaret();
			richTextBox1.ResumeLayout();
		}

		public void LogLine(string Component, string Message, string Instance = null)
		{ Instance = Instance ?? string.Empty; LogLine(Component + " :" + Instance + "> " + Message); }

		delegate void LogLineInvoker(string Component, string Message, string Instance = null);

		public void InvokeLogLine(string Component, string Message, string Instance = null)
		{ this.Invoke((LogLineInvoker) LogLine, Component, Message, Instance); }

		private void button1_Click(object sender, EventArgs e)
		{
			System.Threading.Tasks.Task tk = new System.Threading.Tasks.Task(RunPipeline);
			tk.Start();
		}

		private void fileSystemWatcher1_Created(object sender, System.IO.FileSystemEventArgs e)
		{
			if (e.ChangeType != System.IO.WatcherChangeTypes.Created && e.ChangeType != System.IO.WatcherChangeTypes.Renamed) return;
			if (e.Name.Length != 4 || e.Name[0] != 'E') return;
			LogLine("Autoload", "New field available. Loading.");
			textBox1.Text = e.Name;
			textBox2.Text = e.FullPath;
			textBox3.Text = Config.RootOutputDir + e.Name;
		}

		void RunPipeline()
		{
			int i;
			string[] BadpixSet = null;
			if(Pipeline.UseCoreFilter) BadpixSet = Directory.GetFiles(Config.Badpixel);
			for (i = 0; i < InputFiles.Length; i++)
			{
				int CCDNum = i + 1;
				string CCDStr = "CCD" + CCDNum.ToString();
				string CBP = BadpixSet == null ? null : BadpixSet.Where((x) => x.Contains(CCDStr)).First();
				List<Tracklet> Result;
				try
				{
					Result = Pipeline.AnalyzeCCD(Path.Combine(textBox3.Text, CCDStr), InputFiles[i].ToArray(), CBP, (x) => InvokeLogLine("Pipeline", x, CCDStr));
				}
				catch (Exception ex)
				{
					InvokeLogLine("Pipeline Error", "Unhandled exception of type " + ex.GetType() + " occurred.");
					InvokeLogLine("Pipeline Error", "Error message: " + ex.Message);
					InvokeLogLine("Pipeline Error", "Stack trace: " + ex.StackTrace);
					return;
				}
				this.Invoke((ResultShower) ShowResults, Result, i);
			}
		}

		delegate void ResultShower(List<Tracklet> Tracklets, int i);

		static void ShowResults(List<Tracklet> Tracklets, int i)
		{
			TrackletOutput TKO = new TrackletOutput("Tracklet viewer for CCD" + i.ToString());
			TKO.Tracklets = Tracklets;
			TKO.Show();
		}

		private void textBox2_Validating(object sender, CancelEventArgs e)
		{
			if (!Directory.Exists(textBox2.Text))
			{ textBox2.BackColor = System.Drawing.Color.Red; button1.Enabled = false; return; }
			string TPath = textBox2.Text;
			if (!TryValidateInputPath(TPath))
			{
				TPath += Path.DirectorySeparatorChar + "resampleRow";
				if(!Directory.Exists(TPath)) { textBox2.BackColor = System.Drawing.Color.Yellow; button1.Enabled = false; return; }
				if (!TryValidateInputPath(TPath)) { textBox2.BackColor = System.Drawing.Color.Yellow; button1.Enabled = false; return; }
			}

			textBox2.BackColor = System.Drawing.Color.LightGreen;
			button1.Enabled = true;
		}

		bool TryValidateInputPath(string TPath)
		{
			string[] FITS = Directory.EnumerateFiles(TPath).Where(IsFitsExtension).ToArray();
			if (FITS.Length == 0) return false;
			List<List<string>> CCDf = new List<List<string>>();
			foreach(string s in FITS)
			{
				string FN = Path.GetFileNameWithoutExtension(s);
				int idx = FN.IndexOf("CCD");
				if (idx == -1) return false;
				char CCDNum = FN[idx + 3];
				int CCDnum = CCDNum - '1';
				while (CCDf.Count < CCDnum + 1) CCDf.Add(new List<string>());
				CCDf[CCDnum].Add(s);
			}
			InputFiles = CCDf.ToArray();
			return true;
		}

		static bool IsFitsExtension(string File)
		{
			string Extension = Path.GetExtension(File);
			if (Extension == ".fit" || Extension == ".fits") return true;
			return false;
		}

		private void inputToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(textBox2.Text)) folderBrowserDialog1.SelectedPath = textBox2.Text;
			folderBrowserDialog1.ShowDialog();
			textBox2.Text = folderBrowserDialog1.SelectedPath;
		}

		private void outputToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(textBox3.Text)) folderBrowserDialog1.SelectedPath = textBox3.Text;
			folderBrowserDialog1.ShowDialog();
			textBox3.Text = folderBrowserDialog1.SelectedPath;
		}
	}
}
