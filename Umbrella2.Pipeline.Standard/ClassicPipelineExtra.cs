using System;
using System.Collections.Generic;
using Umbrella2.Algorithms.Detection;
using Umbrella2.Algorithms.Filtering;
using Umbrella2.Algorithms.Images;
using Umbrella2.IO.FITS;
using Umbrella2.PropertyModel.CommonProperties;

namespace Umbrella2.Pipeline.Standard
{
	public partial class ClassicPipeline
	{
		private void DetectionDebugMap(string RunDir, int i, List<ImageDetection> LocalDetectionList, FitsImage DetectionSource)
		{
			FICHV Header = DetectionSource.CopyHeader().ChangeBitPix(16);
			MMapFitsFile DeOutput = MMapFitsFile.OpenWriteFile(RunDir + "DOutMap" + i.ToString() + ".fits", Header.Header);
			FitsImage DeOutIm = new FitsImage(DeOutput);
			var DeData = DeOutIm.LockData(new System.Drawing.Rectangle(0, 0, (int)DetectionSource.Width, (int)DetectionSource.Height), false, false);


			foreach (ImageDetection xi in LocalDetectionList)
			{
				int kat = 200 + LocalDetectionList.IndexOf(xi);
				if (xi.TryFetchProperty(out ObjectPoints pts))
				{
					foreach (var pt in pts.PixelPoints)
						try { DeData.Data[(int)pt.Y, (int)pt.X] = kat; }
						catch { break; }
				}
			}
			DeOutIm.ExitLock(DeData);
			Logger("Exported detections map");
		}

		private void ComputeDetectorData(FitsImage Central, ImageStatistics CentralStats, StarData StarList,
			 out MaskByMedian.MaskProperties MaskProp, out DotDetector SlowDetector, out LongTrailDetector.LongTrailData LTD)
		{
			MaskProp = new MaskByMedian.MaskProperties()
			{
				LTM = MaskThreshold.Low,
				UTM = MaskThreshold.High,
				ExtraMaskRadius = ExtraMaskRadius,
				MaskRadiusMultiplier = MaskRadiusMultiplier,
				StarList = StarList
			};
			MaskByMedian.CreateMasker(Central, MaskProp, CentralStats);

			SlowDetector = new DotDetector()
			{
				HighThresholdMultiplier = DotDetectorThreshold.High,
				LowThresholdMultiplier = DotDetectorThreshold.Low,
				MinPix = DotMinPix,
				NonrepresentativeThreshold = NonrepresentativeThreshold
			};

			LTD = LongTrailDetector.GeneralAlgorithmSetup(
				PSFSize: PSFDiameter, RLHTThreshold: RLHTThreshold,
				SegmentSelectThreshold: SegmentThreshold.High, SegmentDropThreshold: SegmentThreshold.Low,
				MaxInterblobDistance: MaxInterblobDistance, SimpleLine: true);
			LTD.DropCrowdedRegion = true;
		}

		private class LTLimit : IImageDetectionFilter
		{
			public int MinPix;

			public bool Filter(ImageDetection Input)
			{
				return (Input.TryFetchProperty(out ObjectPoints op) && op.PixelPoints.Length > MinPix) || Input.FetchOrCreate<PairingProperties>().IsDotDetection;
			}
		}
	}
}
