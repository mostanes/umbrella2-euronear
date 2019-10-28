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
using Umbrella2.IO;
using Umbrella2.IO.FITS;
using Umbrella2.IO.FITS.KnownKeywords;
using Umbrella2.PropertyModel.CommonProperties;

namespace Umbrella2.Pipeline.ViaNearby
{
	public partial class StandardPipeline
	{
		[Flags]
		public enum EnabledOperations : long
		{
			Normalization = 1,
			Masking = 2,
			SecondMedian = 4,
			BlobDetector = 8,
			LongTrailDetector = 16,
			OutputDetectionMap = 32
		}

		public List<Tracklet> AnalyzeCCD(string RunDir, string[] FilePaths, string Badpixel, Action<string> Logger)
		{
			/* Deal with incorrect SWARP flux scaling */
			SWarpScaling.ApplyTransform = CorrectSWARP;

			if (!Directory.Exists(RunDir)) Directory.CreateDirectory(RunDir);

			/* Read input images and preprocess for poisson noise */
			int ImageCount = FilePaths.Length;
			FitsImage[] Originals = new FitsImage[ImageCount];
			FitsImage[] FirstProcess = new FitsImage[ImageCount];
			ObservationTime[] Times = new ObservationTime[ImageCount];
			FitsImage Central;
			double[,] PoissonWeights = PoissonKernel(PoissonRadius);
			double[] PFW = new double[PoissonWeights.Length];
			Buffer.BlockCopy(PoissonWeights, 0, PFW, 0, PFW.Length * sizeof(double));
			Logger("Begining to run the pipeline");

			bool HasBadpix = Badpixel != null;

			BitArray[] map = null;
			if (HasBadpix)
			{
				Logger("Checking badpixel file");
				MMapFitsFile fif_bad = MMapFitsFile.OpenReadFile(Badpixel);
				FitsImage BadpixMap = new FitsImage(fif_bad);
				map = BadpixelFilter.CreateFilter(BadpixMap);
			}


			for (int i = 0; i < ImageCount; i++)
			{
				MMapFitsFile File = MMapFitsFile.OpenReadFile(FilePaths[i]);
				Originals[i] = new FitsImage(File);
				MMapFitsFile PFFile;
				string PoissonFN = Path.Combine(RunDir, Path.GetFileNameWithoutExtension(FilePaths[i]) + "_poisson.fits");
				FitsImage Poisson;
				if (!System.IO.File.Exists(PoissonFN))
				{
					FICHV Header = Originals[i].CopyHeader().ChangeBitPix(StandardBITPIX);
					PFFile = MMapFitsFile.OpenWriteFile(PoissonFN, Header.Header);
					Poisson = new FitsImage(PFFile);
					if (!UseCoreFilter)
						RestrictedMean.RestrictedMeanFilter.Run(PFW, Originals[i], Poisson, RestrictedMean.Parameters(PoissonRadius));
					else if (HasBadpix)
						CoreFilter.Filter.Run(new CoreFilter.CoreFilterParameters(PFW, map), Originals[i], Poisson, CoreFilter.Parameters(PoissonRadius));
					else throw new ArgumentException("Must specify Badpixel files if trying to run with CoreFilter");
					Logger("Generated poisson image " + i);
				}
				else { PFFile = MMapFitsFile.OpenReadFile(PoissonFN); Poisson = new FitsImage(PFFile); }
				Poisson.GetProperty<ImageSource>().AddToSet(Originals[i], "Poisson Filtered");
				Times[i] = (Originals[i].GetProperty<ObservationTime>());
				if (Operations.HasFlag(EnabledOperations.Normalization))
				{
					FitsImage Normalized = EnsureImage(RunDir, "Normalized_", i, Poisson, StandardBITPIX, (x) => { Point4Distance p4d = new Point4Distance(Poisson, x, NormalizationMeshSize); });
					Normalized.GetProperty<ImageSource>().AddToSet(Originals[i], "Normalized");
					FirstProcess[i] = Normalized;
					Logger("Generated normalized image " + i);
				}
				else FirstProcess[i] = Poisson;
			}

			/* Create the central median */
			string CentralPath = Path.Combine(RunDir, "Central.fits");
			bool CentralExists = File.Exists(CentralPath);
			MMapFitsFile CFile;
			if (!CentralExists)
			{
				FICHV Header = Originals[0].CopyHeader().ChangeBitPix(StandardBITPIX);
				CFile = MMapFitsFile.OpenWriteFile(CentralPath, Header.Header);
			}
			else CFile = MMapFitsFile.OpenReadFile(CentralPath);
			Central = new FitsImage(CFile);
			if (!CentralExists)
				HardMedians.MultiImageMedian.Run(null, FirstProcess, Central, HardMedians.MultiImageMedianParameters);

			Logger("Computed the multi-image median");

			/* Prepare the mask, slow object detector, trail detector, weights for second median filtering, etc. */
			ImageStatistics CentralStats = new ImageStatistics(Central);
			MaskByMedian.MaskProperties MaskProp = new MaskByMedian.MaskProperties()
			{
				LTM = MaskThreshold.Low,
				UTM = MaskThreshold.High,
				ExtraMaskRadius = ExtraMaskRadius,
				MaskRadiusMultiplier = MaskRadiusMultiplier
			};
			StarData StarList = new StarData();
			MaskProp.StarList = StarList;
			MaskByMedian.CreateMasker(Central, MaskProp, CentralStats);

			DotDetector SlowDetector = new DotDetector()
			{
				HighThresholdMultiplier = DotDetectorThreshold.High,
				LowThresholdMultiplier = DotDetectorThreshold.Low,
				MinPix = DotMinPix,
				NonrepresentativeThreshold = NonrepresentativeThreshold
			};

			List<ImageDetection> FullDetectionsList = new List<ImageDetection>();

			double[,] MedianWeights2 = GenerateSecondMedian();
			double[] FMW2 = new double[MedianWeights2.Length];
			Buffer.BlockCopy(MedianWeights2, 0, FMW2, 0, FMW2.Length * sizeof(double));

			LongTrailDetector.LongTrailData LTD = LongTrailDetector.GeneralAlgorithmSetup(
				PSFSize: PSFDiameter, RLHTThreshold: RLHTThreshold,
				SegmentSelectThreshold: SegmentThreshold.High, SegmentDropThreshold: SegmentThreshold.Low,
				MaxInterblobDistance: MaxInterblobDistance, SimpleLine: true);
			LTD.DropCrowdedRegion = true;

			var Serializer = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
			Dictionary<FitsImage, FitsImage> Viewmap = new Dictionary<FitsImage, FitsImage>();

			Logger("Ready for final image processing and detection");

			for (int i = 0; i < ImageCount; i++)
			{
				List<ImageDetection> LocalDetectionList = new List<ImageDetection>();

				FitsImage DetectionSource = FirstProcess[i];
				if (Operations.HasFlag(EnabledOperations.Masking))
				{
					FitsImage MaskedImage = EnsureImage(RunDir, "Masked_", i, FirstProcess[i], StandardBITPIX, (x) => MaskByMedian.MaskImage(FirstProcess[i], x, MaskProp),
						new List<ImageProperties>() { Originals[i].GetProperty<ObservationTime>() });
					DetectionSource = MaskedImage;
					MaskedImage.GetProperty<ImageSource>().AddToSet(FirstProcess[i], "Masked Difference");
					Logger("Masked image " + i);
				}

				if (Operations.HasFlag(EnabledOperations.SecondMedian))
				{
					FitsImage SecondMedImage = EnsureImage(RunDir, "SecMed_", i, DetectionSource, StandardBITPIX,
						(x) => HardMedians.WeightedMedian.Run(FMW2, DetectionSource, x, HardMedians.WeightedMedianParameters(SecMedRadius)),
						new List<ImageProperties>() { Originals[i].GetProperty<ObservationTime>() });

					SecondMedImage.GetProperty<ImageSource>().AddToSet(DetectionSource, "Second Median");
					Logger("Computed second median for image " + i);
				}

				ImageStatistics SecMedStat = new ImageStatistics(DetectionSource);
				foreach (MetadataRecord er in Originals[i].GetProperty<ObservationTime>().GetRecords()) DetectionSource.Header.Add(er.Name, er);

				LocalDetectionList = new List<ImageDetection>();

				if (Operations.HasFlag(EnabledOperations.LongTrailDetector))
				{
					LongTrailDetector.PrepareAlgorithmForImage(DetectionSource, SecMedStat, ref LTD);
					LongTrailDetector.Algorithm.Run(LTD, DetectionSource, LongTrailDetector.Parameters);
					LocalDetectionList.AddRange(LTD.Results);
					Logger("Found " + LTD.Results.Count + " detections with the long trail detector");
				}

				if (Operations.HasFlag(EnabledOperations.BlobDetector))
				{
					var SlowList = SlowDetector.Detect(DetectionSource);
					LocalDetectionList.AddRange(SlowList);
					Logger("Found " + SlowList.Count + " detections with the blob detector");
				}

				if (Operations.HasFlag(EnabledOperations.OutputDetectionMap))
				{
					FICHV Header = DetectionSource.CopyHeader().ChangeBitPix(16);
					MMapFitsFile DeOutput = MMapFitsFile.OpenWriteFile(RunDir + "DOutSeg" + i.ToString() + ".fits", Header.Header);
					FitsImage DeOutIm = new FitsImage(DeOutput);
					var DeData = DeOutIm.LockData(new System.Drawing.Rectangle(0, 0, (int) DetectionSource.Width, (int) DetectionSource.Height), false, false);


					foreach (ImageDetection xi in LocalDetectionList)
					{
						int kat = 200 + LocalDetectionList.IndexOf(xi);
						foreach (var pt in xi.FetchProperty<ObjectPoints>().PixelPoints)
							try { DeData.Data[(int) pt.Y, (int) pt.X] = kat; }
							catch { break; }
					}
					DeOutIm.ExitLock(DeData);
					Logger("Exported detections map");
				}

				FullDetectionsList.AddRange(LocalDetectionList.Where((x) => x.FetchProperty<ObjectPoints>().PixelPoints.Length > 100 | x.FetchOrCreate<PairingProperties>().IsDotDetection));
			}
			Logger("Filtering and pairing detections...");
			//PoolMDMerger DetectionPool = new PoolMDMerger(Times.Select((x) => x.Time).ToArray());
			LinePoolSimple lps = new LinePoolSimple();

			LinearityThresholdFilter LTF = new LinearityThresholdFilter() { MaxLineThickness = MaxLineThickness };
			List<ImageDetection> FilteredDetections = ImageDetectionFilterTools.Filter(FullDetectionsList, LTF);
			StarList.MarkStarCrossed(FilteredDetections, 2, 250000);
			double PSFArc = Math.PI / 180 / 3600;
			PrePair.MatchDetections(FilteredDetections, MaxPairmatchDistance, MixMatch, PSFArc);

			Logger("Left with " + FilteredDetections.Count + " detections");
			lps.LoadDetections(FilteredDetections);
			//DetectionPool.LoadDetections(FilteredDetections);
			lps.GeneratePool();
			//DetectionPool.GeneratePool();
			//var Pairings = DetectionPool.Search();
			var Pairings = lps.FindTracklets();

			Logger("Found " + Pairings.Count + " raw tracklets");

			//var TKList = Pairings.Select((ImageDetection[][] x) => x.Where((y) => y.Length > 0).Select(StandardTrackletFactory.MergeStandardDetections).ToArray())
			//	.Select((x) => StandardTrackletFactory.CreateTracklet(x)).ToList();
			var TKList = Pairings;
			var TK2List = TrackletFilters.Filter(TKList, new LinearityTest());

			var TK3L = TK2List.Where(SelectByReg).ToList();

			Logger("Done. " + TK3L.Count + " candidate objects found.");

			return TK3L;
		}

		
	}
}
