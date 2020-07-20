using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Umbrella2.Algorithms.Filtering;
using Umbrella2.IO.FITS;
using Umbrella2.Visualizer.Winforms;

namespace Umbrella2.Pipeline.ViaNearby
{
	public partial class MainForm : Form
	{
		FrontendConfig Config;
		const string ConfigFile = "config.txt";
		Umbrella2.Pipeline.Standard.ClassicPipeline Pipeline;
		List<string>[] InputFiles;
		List<string>[] CatFiles;
		Dictionary<int, BadzoneFilter> Badzones;
		TrackletOutput TKO;

		public MainForm()
		{
			InitializeComponent();
		}

		private void textBox1_Validating(object sender, CancelEventArgs e)
		{
			if (textBox1.Text.Length != 4) goto skip;
			if (textBox1.Text[0] == 'e') textBox1.Text = "E" + textBox1.Text.Substring(1);
			if (textBox1.Text[0] != 'E') goto skip;
			label2.Text = "Night: " + textBox1.Text[1];
			label3.Text = "Field number: " + textBox1.Text.Substring(2);
			textBox1.BackColor = System.Drawing.Color.LightGreen;
		skip:
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
			try
			{
				if (Config.WatchDir & !LoadedLast) { fileSystemWatcher1.Path = Config.RootInputDir; fileSystemWatcher1.EnableRaisingEvents = true; LogLine("Core", "Watching directory for changes"); }
			}
			catch { LogLine("Core", "Could not watch root input directory."); }
			Pipeline = new Umbrella2.Pipeline.Standard.ClassicPipeline();
			textBox3.Text = Config.RootOutputDir;
			LogLine("Core", "Loading integrated plugins");
			foreach (System.Reflection.Assembly asm in Program.GetAssemblies())
				Plugins.LoadableTypes.RegisterNewTypes(asm.GetTypes());
			LogLine("Core", "Plugins loaded");
			LogLine("Core", "Initialized");

			if (Program.Args.Length != 0)
			{
				LogLine("Automation", "Arguments specified on the command line.");
				if (Program.Args[0] == "autofield")
				{
					LogLine("Automation", "Automatically running field " + Program.Args[1]);
					textBox1.Text = Program.Args[1];
					textBox1_Validating(null, null);
					textBox2_Validating(null, null);
					button1_Click(null, null);
				}
			}
		}

		bool TryLoadLast()
		{
			bool Err = false;
			if (!Directory.Exists(Config.RootInputDir)) { LogLine("Autoload", "Input directory does not exist"); Err = true; }
			if (!Directory.Exists(Config.RootOutputDir)) { LogLine("Autoload", "Output directory does not exist"); Err = true; }
			if (Err) return false;

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

		void TryGetBadzone()
		{
			if (!File.Exists("badzone.txt")) return;
			Badzones = new Dictionary<int,BadzoneFilter>();
			int C_CCD = 0;
			List<List<PixelPoint>> c_pix = null;
			foreach (string Line in File.ReadLines("badzone.txt"))
			{
				if (Line[0] == '#') continue;
				if (Line[0] == 'C')
				{
					if (c_pix != null)
						Badzones.Add(C_CCD, new BadzoneFilter(c_pix));
					c_pix = new List<List<PixelPoint>>();
					C_CCD = int.Parse(Line.Substring(1));
				}
				else
				{
					List<PixelPoint> lpp = new List<PixelPoint>();
					string[] ppl = Line.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (string p in ppl)
					{
						string[] h = p.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
						lpp.Add(new PixelPoint() { X = double.Parse(h[0]), Y = double.Parse(h[1]) });
					}
					c_pix.Add(lpp);
				}
			}
			if (c_pix != null)
				Badzones.Add(C_CCD, new BadzoneFilter(c_pix));
		}

		void RunPipeline()
		{
			string FieldName = textBox1.Text;
			int i;
			string[] BadpixSet = null;
			if(Pipeline.UseCoreFilter) BadpixSet = Directory.GetFiles(Config.Badpixel);
			TryGetBadzone();
			for (i = 0; i < InputFiles.Length; i++)
			{
				int CCDNum = i + 1;
				if (Pipeline.SkipCCD2 & CCDNum == 2) continue;

				string CCDStr = "CCD" + CCDNum.ToString();
				string CBP = BadpixSet == null ? null : BadpixSet.Where((x) => x.Contains(CCDStr)).First();
				List<Tracklet> Result;
				FitsImage[] fims;
				try
				{
					MMapFitsFile[] mmfs = InputFiles[i].Select((x) => MMapFitsFile.OpenReadFile(x)).ToArray();
					fims = mmfs.Select((x) => new FitsImage(x)).ToArray();
					BadzoneFilter bzf = null;
					if (Badzones != null && Badzones.ContainsKey(CCDNum))
						bzf = Badzones[CCDNum];
					Standard.PipelineArguments pa = new Standard.PipelineArguments()
					{
						Badpixel = CBP,
						RunDir = Path.Combine(textBox3.Text, CCDStr),
						Inputs = fims,
						CatalogData = CatFiles?[i]?.Select(File.ReadLines)?.ToArray(),
						Clipped = false,
						CCDBadzone = bzf,
						FieldName = textBox1.Text,
						CCDNumber = CCDNum
					};

					Pipeline.Logger = (x) => InvokeLogLine("Pipeline", x, CCDStr);

					Result = Pipeline.AnalyzeCCD(pa);
				}
				catch (Exception ex)
				{
					InvokeLogLine("Pipeline Error", "Unhandled exception of type " + ex.GetType() + " occurred.");
					InvokeLogLine("Pipeline Error", "Error message: " + ex.Message);
					InvokeLogLine("Pipeline Error", "Stack trace: " + ex.StackTrace);
					return;
				}
				this.Invoke((ResultShower)ShowResults, Result, i, fims, FieldName);
			}
		}

		delegate void ResultShower(List<Tracklet> Tracklets, int i, IList<FitsImage> Images, string FieldName);

		void ShowResults(List<Tracklet> Tracklets, int i, IList<FitsImage> Images, string FieldName)
		{
			int CCDNum = i + 1;
			if (TKO == null)
			{
				TKO = new TrackletOutput(FieldName);
				TKO.ReportName = Path.Combine(textBox2.Text, "mpc3report.txt");
				TKO.ObservatoryCode = "950";
				TKO.Band = ExtraIO.MPCOpticalReportFormat.MagnitudeBand.R;
				TKO.FieldName = FieldName;
				TKO.ReportFieldName = FieldName.Substring(0, 2) + FieldName.Substring(FieldName.Length - 2, 2);
				TKO.Show();
			}
			TKO.AddCCD(CCDNum, Tracklets, (IList<IO.Image>)Images);
		}

		private void textBox2_Validating(object sender, CancelEventArgs e)
		{
			if (!Directory.Exists(textBox2.Text))
			{ textBox2.BackColor = System.Drawing.Color.Red; button1.Enabled = false; return; }
			string TPath = textBox2.Text;
			if (!TryValidateInputPath(TPath))
			{
				if (TrySEValidateInputPath(TPath))
				{
					textBox2.BackColor = System.Drawing.Color.LightBlue;
					button1.Enabled = true;
				}
			}
			else
			{
				textBox2.BackColor = System.Drawing.Color.LightGreen;
				button1.Enabled = true;
			}
		}

		bool TryValidateInputPath(string FTPath)
		{
			string[] FITS = Directory.EnumerateFiles(FTPath).Where(IsFitsExtension).ToArray();
			if (FITS.Length == 0) return false;
			List<List<string>> CCDf = new List<List<string>>();
			foreach(string s in FITS)
			{
				string FN = Path.GetFileNameWithoutExtension(s);
				//int idx = FN.IndexOf("CCD");
				int idx = FN.IndexOf(".resamp");
				if (idx == -1) return false;
				//char CCDNum = FN[idx + 3];
				char CCDNum = FN[idx - 1];
				int CCDnum = CCDNum - '1';
				while (CCDf.Count < CCDnum + 1) CCDf.Add(new List<string>());
				CCDf[CCDnum].Add(s);
			}
			InputFiles = CCDf.ToArray();
			return true;
		}

		bool TrySEValidateInputPath(string TPath)
		{
			string FTPath = Path.Combine(TPath, "resamp");
			if (!Directory.Exists(FTPath)) { textBox2.BackColor = System.Drawing.Color.Yellow; button1.Enabled = false; return false; }
			if (!TryValidateInputPath(FTPath)) { textBox2.BackColor = System.Drawing.Color.Yellow; button1.Enabled = false; return false; }
			textBox2.BackColor = System.Drawing.Color.LightGreen;
			button1.Enabled = true;

			try
			{
				string SPath = Path.Combine(TPath, "sextractor_cat");
				List<string> Catpaths = Directory.EnumerateFiles(SPath).ToList();
				CatFiles = new List<string>[InputFiles.Length];
				for (int i = 0; i < InputFiles.Length; i++)
				{
					CatFiles[i] = new List<string>();
					foreach (string s in InputFiles[i])
					{
						string fn = Path.GetFileNameWithoutExtension(s);
						CatFiles[i].Add(Path.Combine(SPath, fn + ".cat"));
					}
				}
			}
			catch { return false; }
			return true;
		}

		static bool IsFitsExtension(string File)
		{
			string Extension = Path.GetExtension(File);
			if (Extension == ".fit" || Extension == ".fits" || Extension == ".fts") return true;
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
