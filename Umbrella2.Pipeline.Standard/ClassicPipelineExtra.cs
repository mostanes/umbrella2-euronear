using System;
using System.Collections.Generic;
using Umbrella2.Algorithms.Detection;
using Umbrella2.Algorithms.Filtering;
using Umbrella2.Algorithms.Images;
using Umbrella2.IO;
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

		private List<Tracklet> RecoverTracklets(List<Tracklet> Input, FitsImage[] Images, string Filename)
		{
			ApproxRecover ar = new ApproxRecover()
				{ CrossMatchRemove = 3 * Images.Length / 4, ThresholdMultiplier = OriginalThreshold, RecoverRadius = RecoveryRadius };
			List<Tracklet> Results = new List<Tracklet>();
			System.Text.StringBuilder log = new System.Text.StringBuilder();
			foreach (Tracklet tk in Input)
			{
				foreach (ImageDetection imd in tk.Detections)
					log.AppendLine(ExtraIO.EquatorialPointStringFormatter.FormatToString(imd.Barycenter.EP, ExtraIO.EquatorialPointStringFormatter.Format.MPC));
				if (ar.RecoverTracklet(tk.VelReg, Images, out Tracklet Rcv))
				{ Results.Add(Rcv); log.AppendLine("Found\n"); }
				else log.AppendLine("Not found\n");
			}
			System.IO.File.WriteAllText(Filename, log.ToString());
			return Results;
		}

		private class LTLimit : IImageDetectionFilter
		{
			public int MinPix;

			public bool Filter(ImageDetection Input)
			{
				if (Input.TryFetchProperty(out ObjectPoints op))
					return (op.PixelPoints.Length > MinPix || Input.FetchOrCreate<PairingProperties>().IsDotDetection);
				else return true;

			}
		}

		private class StaticFilter : ITrackletFilter
		{
			public double MinPixDistance = 3;
			public double MinEqVel = 0.1;

			public bool Filter(Tracklet Input)
			{
				double EqVelR = MinEqVel * Math.PI / 38880000;
				if (Input.Velocity.SphericalVelocity < EqVelR)
					return false;

				double SqS = 0;

				foreach(ImageDetection imd1 in Input.Detections)
					foreach(ImageDetection imd2 in Input.Detections)
						if(imd1!=imd2)
						{
							double dX = imd1.Barycenter.PP.X - imd2.Barycenter.PP.X;
							double dY = imd1.Barycenter.PP.Y - imd2.Barycenter.PP.Y;
							SqS += dX * dX + dY * dY;
						}

				if (SqS < MinPixDistance * MinPixDistance * Input.Detections.Length * (Input.Detections.Length - 1))
					return false;

				return true;
			}
		}

		private class RipFilter : IImageDetectionFilter
		{
			public double SigmaTop = 10;
			public double ImgMean;
			public double ImgSigma;

			public bool Filter(ImageDetection Input)
			{
				double Th = ImgMean + ImgSigma * SigmaTop;
				if(Input.TryFetchProperty(out ObjectPoints op))
				{
					double[] pv = new double[op.PixelValues.Length];
					Buffer.BlockCopy(op.PixelValues, 0, pv, 0, pv.Length * sizeof(double));
					Array.Sort(pv);
					if (pv[pv.Length / 10] > Th)
						return false;
				}
				return true;
			}
		}

		private class TotalError : ITrackletFilter
		{
			public double MaxError = 2.5;

			public bool Filter(Tracklet Input)
			{
				double MxER = MaxError * Math.PI / 180 / 3600;

				double SS = Input.VelReg.S_TD + Input.VelReg.S_TR;
				if (SS > MxER * MxER) return false;
				return true;
			}
		}
	}
}
