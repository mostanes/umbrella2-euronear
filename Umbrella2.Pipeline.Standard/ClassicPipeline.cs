using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Umbrella2.Algorithms.Detection;
using Umbrella2.Algorithms.Filtering;
using Umbrella2.Algorithms.Images;
using Umbrella2.Algorithms.Images.Normalization;
using Umbrella2.Algorithms.Pairing;
using Umbrella2.IO.FITS;
using Umbrella2.IO.FITS.KnownKeywords;
using Umbrella2.PropertyModel.CommonProperties;

namespace Umbrella2.Pipeline.Standard
{
	public partial class ClassicPipeline
	{
		public Action<string> Logger;

		void LogImage(bool Generated, string Name, int Number)
		{
			if (Generated) Logger("Generated " + Name + " image " + Number);
			else Logger("Found " + Name + " image " + Number);
		}

		void LogDet(string Detector, int DetNum) => Logger("Found " + DetNum + " detections using " + Detector + " detector");

		void LogMessage(string Source, string Message) => Logger("[" + Source + "]: " + Message);

		public List<Tracklet> AnalyzeCCD(PipelineArguments Args)
		{
			Logger("Setting up pipeline");
			/* Deal with incorrect SWARP flux scaling */
			SWarpScaling.ApplyTransform = CorrectSWARP;

			string RunDir = Args.RunDir;
			if (!Directory.Exists(RunDir)) Directory.CreateDirectory(RunDir);

			/* Read input images and preprocess for poisson noise */
			int ImageCount = Args.Inputs.Length;
			FitsImage[] FirstProcess = new FitsImage[ImageCount];
			double[] PFW = PipelineHelperFunctions.LinearizedPoissonKernel(PoissonRadius);

			Step.StepPipeline sp = new Step.StepPipeline(StandardBITPIX, RunDir, Args.Inputs.Length, MaxDetections);
			sp.LogHookImage = LogImage;
			sp.LogHookDetection = LogDet;

			bool HasBadpix = Args.Badpixel != null;

			Logger("Begining to run the pipeline");
			var zpTask = System.Threading.Tasks.Task<Dictionary<IO.Image, double>>.Factory.StartNew(() => CalibrateZP(Args.Inputs));
			var skTask = System.Threading.Tasks.Task<bool>.Factory.StartNew(() => PrecacheSkyBot(Args.Inputs));

			BitArray[] map = PipelineHelperFunctions.ExtractBadpixel(Args.Badpixel, Logger);

			for (int i = 0; i < ImageCount; i++)
			{
				FitsImage Pipeline = Args.Inputs[i];
				Pipeline.GetProperty<ImageSource>().AddToSet(Pipeline, "Original");
				sp.SetModel(i, Pipeline, new List<IO.ImageProperties>() { Pipeline.GetProperty<ObservationTime>() });

				if (!UseCoreFilter)
					sp.RunPipeline(RestrictedMean.RestrictedMeanFilter, "Poisson", i, ref Pipeline, PFW, RestrictedMean.Parameters(PoissonRadius));
				else if (HasBadpix)
					sp.RunPipeline(CoreFilter.Filter, "Poisson", i, ref Pipeline, new CoreFilter.CoreFilterParameters(PFW, map), CoreFilter.Parameters(PoissonRadius));
				else throw new ArgumentException("Must specify Badpixel files if trying to run with CoreFilter");

				if (Operations.HasFlag(EnabledOperations.Normalization))
				{
					if (!sp.EnsureImage("Normalized", i, out FitsImage Normalized))
					{
						Point4Distance p4d = new Point4Distance(Pipeline, Normalized, NormalizationMeshSize);
						Logger("Generated Normalized image " + i);
					}
					else Logger("Found Normalized image " + i);
					Normalized.GetProperty<ImageSource>().AddToSet(Args.Inputs[i], "Normalized");
					Pipeline = Normalized;
				}
				FirstProcess[i] = Pipeline;
			}

			/* Create the central median */
			string CentralPath = Path.Combine(RunDir, "Central.fits");
			if (!sp.EnsureCentralImage("Central", out FitsImage Central))
			{
				HardMedians.MultiImageMedian.Run(null, FirstProcess, Central, HardMedians.MultiImageMedianParameters);
				Logger("Generated Central image");
			}
			else Logger("Found Central image");

			Logger("Computed the multi-image median");

			/* Prepare the mask, slow object detector, trail detector, weights for second median filtering, etc. */
			ImageStatistics CentralStats = new ImageStatistics(Central);
			if (Args.Clipped) CentralStats = new ImageStatistics(Central, CentralStats.ZeroLevel, 2 * CentralStats.StDev);
			StarData StarList = new StarData();
			ComputeDetectorData(Central, CentralStats, StarList, out MaskByMedian.MaskProperties MaskProp, out DotDetector SlowDetector,
				out LongTrailDetector.LongTrailData LTD);
			if (Args.Clipped)
			{
				SlowDetector.HighThresholdMultiplier *= 2;
				SlowDetector.LowThresholdMultiplier *= 2;
			}

			DetectionReducer dr = new DetectionReducer() { PairingRadius = 0.7 };
			if (Operations.HasFlag(EnabledOperations.SourceExtractor))
			{
				try
				{
					dr.LoadStars(StarList.FixedStarList);
				}
				catch (Exception ex) { throw new ArgumentException("Could not read detections from SE catalog.", ex); }
				dr.GeneratePool();
			}


			Logger("Set up detectors");

			List<ImageDetection> FullDetectionsList = new List<ImageDetection>();
			double[] FMW2 = PipelineHelperFunctions.LinearizedMedianKernel();
			LTLimit ltl = new LTLimit() { MinPix = TrailMinPix };
			RipFilter rf = new RipFilter() { SigmaTop = 30 };

			Logger("Ready for final image processing and detection");

			for (int i = 0; i < ImageCount; i++)
			{
				List<ImageDetection> LocalDetectionList = new List<ImageDetection>();

				FitsImage DetectionSource = FirstProcess[i];

				if (Operations.HasFlag(EnabledOperations.Masking))
					sp.RunPipeline(MaskByMedian.Masker, "Masked", i, ref DetectionSource, MaskProp, MaskByMedian.Parameters);

				if (Operations.HasFlag(EnabledOperations.SecondMedian))
					sp.RunPipeline(HardMedians.WeightedMedian, "Second Median", i, ref DetectionSource, FMW2, HardMedians.WeightedMedianParameters(SecMedRadius));

				ImageStatistics SecMedStat = new ImageStatistics(DetectionSource);

				if (Operations.HasFlag(EnabledOperations.LongTrailDetector))
				{
					var Dets = sp.RunDetector((FitsImage img) =>
				   {
					   LongTrailDetector.PrepareAlgorithmForImage(img, SecMedStat, ref LTD);
					   LongTrailDetector.Algorithm.Run(LTD, DetectionSource, LongTrailDetector.Parameters);
					   return LTD.Results;
				   }, DetectionSource, "Trail", DetectionAlgorithm.Trail);
					LocalDetectionList.AddRange(Dets);
				}

				if (Operations.HasFlag(EnabledOperations.BlobDetector))
				{
					var Dets = sp.RunDetector(SlowDetector.Detect, DetectionSource, "Blob", DetectionAlgorithm.Blob);
					LocalDetectionList.AddRange(Dets);
				}

				if (Operations.HasFlag(EnabledOperations.SourceExtractor))
				{
					var dts = sp.RunDetector((arg) =>
					{
						List<ImageDetection> Dets = ExtraIO.SourceExtractor.ParseSEFile(Args.CatalogData[i], Args.Inputs[i]);
						Dets = Dets.Where((x) => x.FetchProperty<ObjectPhotometry>().Flux > 300).ToList();
						var ND = dr.Reduce(Dets);
						return ND;
					}, DetectionSource, "SE", DetectionAlgorithm.SourceExtractor);
					LocalDetectionList.AddRange(dts);
				}

				if (Operations.HasFlag(EnabledOperations.OutputDetectionMap))
					DetectionDebugMap(RunDir, i, LocalDetectionList, DetectionSource);

				rf.ImgMean = SecMedStat.ZeroLevel;
				rf.ImgSigma = SecMedStat.StDev;
				var NLDL = sp.RunFilters(LocalDetectionList, "LocalToGlobal", ltl, rf);
				Logger("Total " + NLDL.Count + " detections.");
				FullDetectionsList.AddRange(NLDL);
			}
			Logger("Filtering and pairing detections...");

			LinearityThresholdFilter LTF = new LinearityThresholdFilter() { MaxLineThickness = MaxLineThickness };
			List<ImageDetection> FilteredDetections = sp.RunFilters(FullDetectionsList, "MainFilter", LTF);
			StarList.MarkStarCrossed(FilteredDetections, StarCrossRadiusM, StarCrossMinFlux);
			if (Args.CCDBadzone != null)
				FilteredDetections = sp.RunFilters(FilteredDetections, "Badzone", Args.CCDBadzone);

			Logger("Before PrePair " + FilteredDetections.Count);
			PrePair.MatchDetections(FilteredDetections, MaxPairmatchDistance, MixMatch, SameArcSep);

			Logger("Left with " + FilteredDetections.Count + " detections");
			LinePoolSimple lps = new LinePoolSimple() { MaxLinErrorArcSec = MaxResidual, SearchExtra = ExtraSearchRadius };
			lps.LoadDetections(FilteredDetections);

			lps.GeneratePool();
			var Pairings = lps.FindTracklets();
			sp.NotePairings(FilteredDetections, Pairings);

			Logger("Found " + Pairings.Count + " raw tracklets");

			LinearityTest lintest = new LinearityTest();
			StaticFilter stf = new StaticFilter();
			TotalError te = new TotalError();
			var TK2List = sp.RunFilters(Pairings, "Tracklet Filtering", stf, te);

			Logger("After filtering: " + TK2List.Count + " candidate objects found");

			sp.LogDetections(Path.Combine(RunDir, "detlog.txt"));

			Dictionary<IO.Image, double> ZP = zpTask.Result;
			skTask.Wait();

			var Recovered = RecoverTracklets(TK2List, Args.Inputs, Path.Combine(RunDir, "reclog.txt"), ZP);
			TrackletsDeduplication.Deduplicate(Recovered, 1.0);

			Logger("Recovered " + Recovered.Count + " candidate objects");

			PairSkyBot(Recovered, SkyBoTDistance, Args.FieldName, Args.CCDNumber, Args.Inputs);

			return Recovered;
		}
	}
}
