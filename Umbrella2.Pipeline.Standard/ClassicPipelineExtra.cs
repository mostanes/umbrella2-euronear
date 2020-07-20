using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Umbrella2.Algorithms.Detection;
using Umbrella2.Algorithms.Filtering;
using Umbrella2.Algorithms.Images;
using Umbrella2.IO;
using Umbrella2.IO.FITS;
using Umbrella2.IO.FITS.KnownKeywords;
using Umbrella2.Pipeline.EIOAlgorithms;
using Umbrella2.PropertyModel.CommonProperties;
using static Umbrella2.Pipeline.EIOAlgorithms.VizieRCalibration;

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

		private Dictionary<Image, double> CalibrateZP(FitsImage[] Images)
		{
			Dictionary<Image, double> ZP = new Dictionary<Image, double>();

			CalibrationArgs ca = new CalibrationArgs()
			{
				ClippingPoint = 60000 * Images[0].GetProperty<SWarpScaling>().FlxScale,
				MaxFlux = 300000,
				MaxVizierMag = 22,
				MinFlux = 500,
				NonRepThreshold = 1,
				PositionError = 1,
				StarHighThreshold = 30,
				StarLowThreshold = 5
			};

			Parallel.ForEach(Images, (img) => FindZP(img, ZP, ca));

			return ZP;
		}

		private bool PrecacheSkyBot(FitsImage[] Images)
		{
			Logger("Preloading SkyBoT information");
			foreach (FitsImage img in Images)
				img.GetProperty<SkyBotImageData>().RetrieveObjects(ObservatoryCode);
			return true;
		}

		private void PairSkyBot(List<Tracklet> Tracklets, double ArcLengthSec, string ReportFieldName, int CCDNumber, FitsImage[] ImageSet, Step.StepPipeline Pipeline)
		{
			foreach (Image img in ImageSet)
			{
				SkyBotImageData skid = img.GetProperty<SkyBotImageData>();
				//skid.RetrieveObjects(ObservatoryCode);

				foreach (Tracklet tk in Tracklets)
					skid.TryPair(tk, ArcLengthSec);

				var unp = skid.GetUnpaired();
				if (unp.Count != 0)
				{
					Logger("SkyBoT: Unpaired objects left: ");
					foreach (var o in unp)
					{
						string SkData = "SkyBoT: " + ExtraIO.EquatorialPointStringFormatter.FormatToString(o, ExtraIO.EquatorialPointStringFormatter.Format.MPC);
						PixelPoint pp = img.Transform.GetPixelPoint(o);
						SkData += ";" + pp.ToString();
						Logger(SkData);
						var Reasons = Pipeline.QueryWhyNot(o, 5);
						string LRs = "Reasons: ";
						foreach (string x in Reasons) LRs += x + ";";
						if (Reasons.Count == 0) Logger("Not found in removal log, so not detected.");
						else Logger(LRs);
					}

				}
			}

			for (int i = 0; i < Tracklets.Count; i++)
			{
				Tracklet tk = Tracklets[i];
				if (!tk.TryFetchProperty(out ObjectIdentity objid)) objid = new ObjectIdentity();
				objid.ComputeNamescoreWithDefault(tk, null, ReportFieldName, CCDNumber, i);
				tk.SetResetProperty(objid);
			}
		}

		private List<Tracklet> RecoverTracklets(List<Tracklet> Input, FitsImage[] Images, string Filename, Dictionary<Image, double> ZP)
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
				{
					Results.Add(Rcv);
					log.AppendLine("Found\n");
					foreach (ImageDetection imd in Rcv.Detections)
						if (imd.TryFetchProperty(out ObjectPhotometry oph))
							oph.Magnitude = ZP[imd.ParentImage] - 2.5 * Math.Log10(oph.Flux);
				}
				else log.AppendLine("Not found\n");
			}
			System.IO.File.WriteAllText(Filename, log.ToString());
			return Results;
		}

		private void FindZP(Image img, Dictionary<Image, double> ZP, CalibrationArgs ca)
		{
			double Mag = 28.9 + 2.5 * Math.Log10(img.GetProperty<SWarpScaling>().FlxScale);
			double dMag = 0;
			try
			{
				dMag = CalibrateImage(img, ca);
				if (double.IsNaN(dMag)) Logger("FP error while calibrating flux");
				else Mag = dMag;
			}
			catch (Exception ex) { Logger("Could not calibrate flux. Error: " + ex.Message); }

			Logger("Zeropoint: " + Mag);
			lock (ZP)
				ZP.Add(img, Mag);
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
