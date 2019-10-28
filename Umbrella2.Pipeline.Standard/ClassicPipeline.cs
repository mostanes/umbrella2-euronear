using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
		Action<string> Logger;

		void Log(bool Generated, string Name, int Number)
		{
			if (Generated) Logger("Generated " + Name + " image " + Number);
			else Logger("Found " + Name + " image " + Number);
		}

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

			Step.StepPipeline sp = new Step.StepPipeline(StandardBITPIX, RunDir, Args.Inputs.Length);
			sp.LogHookImage = Log;

			bool HasBadpix = Args.Badpixel != null;

			Logger("Begining to run the pipeline");

			BitArray[] map = PipelineHelperFunctions.ExtractBadpixel(Args.Badpixel, Logger);

			for (int i = 0; i < ImageCount; i++)
			{
				FitsImage Pipeline = Args.Inputs[i];

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
			StarData StarList = new StarData();
			ComputeDetectorData(Central, CentralStats, StarList, out MaskByMedian.MaskProperties MaskProp, out DotDetector SlowDetector,
				out LongTrailDetector.LongTrailData LTD);

			DetectionReducer dr = new DetectionReducer();
			if (Operations.HasFlag(EnabledOperations.SourceExtractor))
			{
				try
				{
					var Dets = ExtraIO.SourceExtractor.ParseSEFile(File.ReadLines(Args.CentralCatalog), Central);
					dr.LoadDetections(Dets);
				}
				catch (Exception ex) { throw new ArgumentException("Could not read detections from SE catalog.", ex); }
				dr.GeneratePool();
			}

			Logger("Set up detectors");

			List<ImageDetection> FullDetectionsList = new List<ImageDetection>();
			double[] FMW2 = PipelineHelperFunctions.LinearizedMedianKernel();
			LTLimit ltl = new LTLimit() { MinPix = TrailMinPix };

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
				   }, DetectionSource, "Trail");
					LocalDetectionList.AddRange(Dets);
				}

				if (Operations.HasFlag(EnabledOperations.BlobDetector))
				{
					var Dets = sp.RunDetector(SlowDetector.Detect, DetectionSource, "Blob");
					LocalDetectionList.AddRange(Dets);
				}

				if (Operations.HasFlag(EnabledOperations.SourceExtractor))
				{
					var dts = sp.RunDetector((arg) =>
					{
						List<ImageDetection> Dets = ExtraIO.SourceExtractor.ParseSEFile(File.ReadLines(Args.Catalogs[i]), Args.Inputs[i]);
						var ND = dr.Reduce(Dets);
						return ND;
					}, DetectionSource, "SE");
					LocalDetectionList.AddRange(dts);
				}

				if (Operations.HasFlag(EnabledOperations.OutputDetectionMap))
					DetectionDebugMap(RunDir, i, LocalDetectionList, DetectionSource);
					
				var NLDL = sp.RunFilters(LocalDetectionList, "LocalToGlobal", ltl);
				FullDetectionsList.AddRange(NLDL);
			}
			Logger("Filtering and pairing detections...");

			LinearityThresholdFilter LTF = new LinearityThresholdFilter() { MaxLineThickness = MaxLineThickness };
			List<ImageDetection> FilteredDetections = sp.RunFilters(FullDetectionsList, "MainFilter", LTF);
			StarList.MarkStarCrossed(FilteredDetections, StarCrossRadiusM, StarCrossMinFlux);
			PrePair.MatchDetections(FilteredDetections, MaxPairmatchDistance, MixMatch, SameArcSep);

			Logger("Left with " + FilteredDetections.Count + " detections");
			LinePoolSimple lps = new LinePoolSimple() { MaxLinErrorArcSec = MaxResidual, SearchExtra = ExtraSearchRadius };
			lps.LoadDetections(FilteredDetections);

			lps.GeneratePool();
			var Pairings = lps.FindTracklets();

			Logger("Found " + Pairings.Count + " raw tracklets");

			LinearityTest lintest = new LinearityTest();
			var TK2List = sp.RunFilters(Pairings, "Tracklet Filtering", lintest);

			Logger("Done. " + TK2List.Count + " candidate objects found.");

			return TK2List;
		}
	}
}
