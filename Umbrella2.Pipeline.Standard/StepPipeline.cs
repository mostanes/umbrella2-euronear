using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Umbrella2.Algorithms.Filtering;
using Umbrella2.Algorithms.Images;
using Umbrella2.IO;
using Umbrella2.IO.FITS;
using Umbrella2.PropertyModel.CommonProperties;
using static Umbrella2.Algorithms.Images.SchedCore;

namespace Umbrella2.Pipeline.Step
{
	public class StepPipeline
	{
		int StandardBitpix;
		string RunDir;
		FICHV[] Headers;

		List<EquatorialPoint> AllDetections;
		Dictionary<EquatorialPoint, string> RemovalPoints;

		public Action<bool, string, int> LogHookImage;
		public Action<string, int> LogHookDetection;

		void SetModel(int Number, FitsImage Model, List<ImageProperties> ExtraProperties)
		{
			Headers[Number] = Model.CopyHeader().ChangeBitPix(StandardBitpix);

			foreach (ImageProperties imp in ExtraProperties)
				foreach (MetadataRecord mr in imp.GetRecords())
					Headers[Number].Header.Add(mr.Name, mr);
		}

		public StepPipeline(int StandardBitPix, string RunDir, int NoImages)
		{
			StandardBitpix = StandardBitPix;
			this.RunDir = RunDir;
			Headers = new FICHV[NoImages];
			AllDetections = new List<EquatorialPoint>();
			RemovalPoints = new Dictionary<EquatorialPoint, string>();
		}

		public bool EnsureImage(string Name, int Number, out FitsImage Image)
		{
			string ImagePath = Path.Combine(RunDir, Name + Number.ToString() + ".fits");
			if (File.Exists(ImagePath)) { Image = new FitsImage(MMapFitsFile.OpenReadFile(ImagePath)); return true; }
			FICHV values = Headers[Number];
			MMapFitsFile file = MMapFitsFile.OpenWriteFile(ImagePath, values.Header);
			Image = new FitsImage(file);
			return false;
		}

		public bool EnsureCentralImage(string Name, out FitsImage Image)
		{
			string ImagePath = Path.Combine(RunDir, Name + ".fits");
			if (File.Exists(ImagePath)) { Image = new FitsImage(MMapFitsFile.OpenReadFile(ImagePath)); return true; }
			FICHV values = Headers[0];
			MMapFitsFile file = MMapFitsFile.OpenWriteFile(ImagePath, values.Header);
			Image = new FitsImage(file);
			return false;
		}

		public void RunPipeline<T>(SimpleMap<T> Map, string Name, int Number, ref FitsImage Pipeline, T Argument, AlgorithmRunParameters Parameters)
		{
			bool Value = EnsureImage(Name, Number, out FitsImage Result);
			if (!Value)
				Map.Run(Argument, Pipeline, Result, Parameters);

			Result.GetProperty<ImageSource>().AddToSet(Pipeline, Name);
			Pipeline = Result;
			LogHookImage(Value, Name, Number);
		}

		public void RunPipeline<T>(PositionDependentMap<T> Map, string Name, int Number, ref FitsImage Pipeline, T Argument, AlgorithmRunParameters Parameters)
		{
			bool Value = EnsureImage(Name, Number, out FitsImage Result);
			if (!Value)
				Map.Run(Argument, Pipeline, Result, Parameters);

			Result.GetProperty<ImageSource>().AddToSet(Pipeline, Name);
			Pipeline = Result;
			LogHookImage(Value, Name, Number);
		}

		public List<ImageDetection> RunDetector(Func<FitsImage,List<ImageDetection>> Detector, FitsImage Image, string DetectorName)
		{
			var Detections = Detector(Image);
			AllDetections.AddRange(Detections.Select((x) => x.Barycenter.EP));
			LogHookDetection(DetectorName, Detections.Count);
			return Detections;
		}

		public List<ImageDetection> RunFilters(List<ImageDetection> Detections, string RemovalPoint, params IImageDetectionFilter[] Filters)
		{
			List<ImageDetection> New = new List<ImageDetection>();
			System.Threading.Tasks.Parallel.ForEach(Detections, (ImageDetection obj) =>
			 {
				 bool Keep = true;
				 foreach(IImageDetectionFilter filter in Filters)
				 {
					 if (!filter.Filter(obj))
					 {
						 Keep = false;
						 string RemName = "Filter name: " + filter.GetType().Name + "\nPoint: " + RemovalPoint;
						 RemovalPoints.Add(obj.Barycenter.EP, RemName);
						 break;
					 }
				 }
				 if (Keep) lock (New) New.Add(obj);
			 });
			return New;
		}

		public List<Tracklet> RunFilters(List<Tracklet> Tracklets, string RemovalPoint, params ITrackletFilter[] Filters)
		{
			List<Tracklet> New = new List<Tracklet>();
			System.Threading.Tasks.Parallel.ForEach(Tracklets, (Tracklet t) =>
			{
				bool Keep = true;
				foreach (ITrackletFilter filter in Filters)
				{
					if (!filter.Filter(t))
					{
						Keep = false;
						foreach (ImageDetection obj in t.Detections)
						{
							string RemName = "Filter name: " + filter.GetType().Name + "\nPoint: " + RemovalPoint;
							RemovalPoints.Add(obj.Barycenter.EP, RemName);
						}
						break;
					}
				}
				if (Keep) lock (New) New.Add(t);
			});
			return New;
		}
	}
}
