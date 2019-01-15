using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace Umbrella2.Pipeline.ViaNearby
{
	public static class Configurator
	{
		public static Dictionary<string, string> ReadConfigFile(string FileName)
		{
			Dictionary<string, string> Config = new Dictionary<string, string>();
			bool skip;
			foreach (string line in File.ReadAllLines(FileName))
			{
				if (string.IsNullOrWhiteSpace(line)) continue;
				if (line[0] == '#' | line[0] == ';') continue;
				if (line[0] == '/') { skip = true; continue; }
				int idx = line.IndexOf('=');
				if (idx == -1) throw new FormatException("File does not conform to expected standard");
				string Key = line.Substring(0, idx);
				string Value = line.Substring(idx + 1);
				Config.Add(Key, Value);
			}
			return Config;
		}

		public static void ApplyConfiguration(StandardPipeline Pipeline, Dictionary<string, string> Config)
		{
			Pipeline.CorrectSWARP = Config.ToBool(nameof(StandardPipeline.CorrectSWARP));
			Pipeline.DotDetectorThreshold = Config.ToThreshold(nameof(StandardPipeline.DotDetectorThreshold));
			Pipeline.ExtraMaskRadius = Config.ToDouble(nameof(StandardPipeline.ExtraMaskRadius));
			Pipeline.MaskRadiusMultiplier = Config.ToDouble(nameof(StandardPipeline.MaskRadiusMultiplier));
			Pipeline.MaskThreshold = Config.ToThreshold(nameof(StandardPipeline.MaskThreshold));
			Pipeline.NormalizationMeshSize = Config.ToInt(nameof(StandardPipeline.NormalizationMeshSize));
			Pipeline.PoissonRadius = Config.ToInt(nameof(StandardPipeline.PoissonRadius));
			Pipeline.SecMedRadius = Config.ToInt(nameof(StandardPipeline.SecMedRadius));
			Pipeline.StandardBITPIX = Config.ToInt(nameof(StandardPipeline.StandardBITPIX));
		}

		static int ToInt(this Dictionary<string, string> Dict, string Value) => int.Parse(Dict[Value]);
		static double ToDouble(this Dictionary<string, string> Dict, string Value) => double.Parse(Dict[Value]);
		static bool ToBool(this Dictionary<string, string> Dict, string Value) => string.IsNullOrWhiteSpace(Dict[Value]) || Dict[Value].ToLower() == "true";
		static Threshold ToThreshold(this Dictionary<string, string> Dict, string Value)
		{
			string[] Values = Dict[Value].Split(',', ';');
			if (Values.Length != 2) throw new FormatException("Threshold field expected, however not enough values found");
			return new Threshold() { High = double.Parse(Values[0]), Low = double.Parse(Values[1]) };
		}
	}

	public struct Threshold
	{
		[Description("Lower threshold")]
		public double Low { get; set; }
		[Description("Higher threshold")]
		public double High { get; set; }

		public override string ToString() => High + "; " + Low;
	}
}
