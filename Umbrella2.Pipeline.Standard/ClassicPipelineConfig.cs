using System;
using System.ComponentModel;
using System.Globalization;
using Umbrella2.Algorithms.Filtering;
using Umbrella2.Pipeline.ExtraIO;

namespace Umbrella2.Pipeline.Standard
{
	[TypeConverter(typeof(ThresholdConverter))]
	public class Threshold
	{
		[Description("Lower threshold")]
		public double Low { get; set; }
		[Description("Higher threshold")]
		public double High { get; set; }

		public override string ToString() => High + "; " + Low;
	}

	public class ThresholdConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) => sourceType == typeof(Threshold);
		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value is string s)
			{
				var parts = s.Split(',');
				if (parts.Length != 2) throw new ArgumentException("Not a Threshold values", nameof(value));
				return new Threshold() { High = double.Parse(parts[0]), Low = double.Parse(parts[1]) };
			}
			else throw new ArgumentException("Input not a string", nameof(value));
		}
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) => destinationType == typeof(string);
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType != typeof(string)) throw new ArgumentException("Output not a string", nameof(destinationType));
			if (value is Threshold t) return t.High + "," + t.Low;
			else throw new ArgumentException("Input not a Threshold", nameof(value));
		}
	}

	public struct PipelineArguments
	{
		public string RunDir;
		public IO.FITS.FitsImage[] Inputs;
		public string Badpixel;
		public System.Collections.Generic.IEnumerable<string>[] CatalogData;
		public bool Clipped;
		public BadzoneFilter CCDBadzone;
		public string FieldName;
		public int CCDNumber;
	}

	[Flags]
	public enum EnabledOperations : long
	{
		Normalization = 1,
		Masking = 2,
		SecondMedian = 4,
		BlobDetector = 8,
		LongTrailDetector = 16,
		OutputDetectionMap = 32,
		OutputRemovedDetections = 64,
		SourceExtractor = 128
	}

	public partial class ClassicPipeline
	{
		[Description("Threshold for masking stars. Value in standard deviations.")]
		[TypeConverter(typeof(ExpandableObjectConverter))]
		[Category("Star masking")]
		[DisplayName("Star masking threshold")]
		public Threshold MaskThreshold { get; set; } = new Threshold() { High = 3.5, Low = 2 };

		[Description("Amount to add to the radius to be masked.")]
		[Category("Star masking")]
		[DisplayName("Extra mask radius")]
		public double ExtraMaskRadius { get; set; } = 1;

		[Description("Amount by which to scale the star radius on masking.")]
		[Category("Star masking")]
		[DisplayName("Mask radius multiplier")]
		public double MaskRadiusMultiplier { get; set; } = 1.1;

		[Description("Threshold of detection for the blob detection algorithm. Value in standard deviations.")]
		[Category("Blob detection")]
		[DisplayName("Blob detector threshold")]
		[TypeConverter(typeof(ExpandableObjectConverter))]
		public Threshold DotDetectorThreshold { get; set; } = new Threshold() { High = 5, Low = 2.5 };

		[Description("Minimum number of pixels to count a detection.")]
		[Category("Blob detection")]
		[DisplayName("Blob min pixels")]
		public int DotMinPix { get; set; } = 15;

		[Description("Largest deviation from the mean a pixel can have before it is excluded from computing the local mean. Value in standard deviations.")]
		[Category("Blob detection")]
		[DisplayName("Non-representative threshold")]
		public double NonrepresentativeThreshold { get; set; } = 50;

		[Description("BITPIX of the generated intermediate files.")]
		[Category("General pipeline properties")]
		[DisplayName("Standard BITPIX")]
		public int StandardBITPIX { get; set; } = -32;

		[Description("Maximum number of detections allowed for each algorithm. If a detection algorithm reports more, they are ignored.")]
		[Category("General pipeline properties")]
		[DisplayName("Maximum algorithm detections")]
		public int MaxDetections { get; set; } = 1000;

		[Description("Radius of the Restricted Mean Shot noise removal filter. Value in pixels.")]
		[Category("Core")]
		[DisplayName("Shot noise radius")]
		public int PoissonRadius { get; set; } = 3;

		[Description("Use CoreFilter instead of Restricted Mean to also remove badpixels.")]
		[Category("Core")]
		[DisplayName("Use CoreFilter")]
		public bool UseCoreFilter { get; set; } = false;

		[Description("Skips CCD 2 from the pipeline.")]
		[Category("Core")]
		[DisplayName("Skip CCD 2")]
		public bool SkipCCD2 { get; set; } = false;

		[Description("Radius of the second median kernel. Value in pixels.")]
		[Category("Deep smoothing")]
		[DisplayName("Second median radius")]
		public int SecMedRadius { get; set; } = 5;

		[Description("Whether the SWAP has set flux scaling correctly.")]
		[Category("Input")]
		[DisplayName("Correct SWARP")]
		public bool CorrectSWARP { get; set; } = true;

		[Description("Distance between two consecutive points in the normalization mesh lattice. Value in pixels.")]
		[Category("Normalization")]
		[DisplayName("Normalization Mesh size")]
		public int NormalizationMeshSize { get; set; } = 40;

		[Description("Size of a blob (in pixels) that should be scanned to recover the PSF of an impulse.")]
		[Category("Core")]
		[DisplayName("PSF Diameter")]
		public int PSFDiameter { get; set; } = 5;

		[Description("Threshold of RLHT votes to process a line further. Value in standard deviations.")]
		[Category("Long trails")]
		[DisplayName("RLHT Threshold")]
		public double RLHTThreshold { get; set; } = 10;

		[Description("Threshold, in standard deviations, for selecting a segment as a trail.")]
		[Category("Long trails")]
		[DisplayName("Segment selection threshold")]
		[TypeConverter(typeof(ExpandableObjectConverter))]
		public Threshold SegmentThreshold { get; set; } = new Threshold() { High = 5, Low = 2.5 };

		[Description("Maximum distance between 2 blobs, in pixels. Value must be an integer.")]
		[Category("Long trails")]
		[DisplayName("Maximum interblob distance")]
		public int MaxInterblobDistance { get; set; } = 40;

		[Description("Minimum number of pixels detector for a trail to be valid.")]
		[Category("Long trails")]
		[DisplayName("Minimum trail pixels")]
		public int TrailMinPix { get; set; } = 100;

		[Description("Maximum thickness of a blob/trail. Value in pixels.")]
		[Category("Filtering")]
		[DisplayName("Max line thickness")]
		public double MaxLineThickness { get; set; } = 15;

		[Description("Maximum distance (in pixels) between 2 blobs/segments part of the same object.")]
		[Category("Filtering")]
		[DisplayName("Pairwise matching max distance")]
		public double MaxPairmatchDistance { get; set; } = 40;

		[Description("Number of overlapping pixels before 2 detections are directly considered part of the same object.")]
		[Category("Filtering")]
		[DisplayName("Pairwise matching mix pixels")]
		public int MixMatch { get; set; } = 10;

		[Description("Number of stellar radii at which the object is considered to cross a star.")]
		[Category("Filtering")]
		[DisplayName("Star-crossing radius multiplier")]
		public double StarCrossRadiusM { get; set; } = 1.55;

		[Description("Minimum flux of a star before it is used to mark star-crossed detections.")]
		[Category("Filtering")]
		[DisplayName("Star-crossing minimum star flux")]
		public double StarCrossMinFlux { get; set; } = 10000;

		[Description("Distance between the barycenter of 2 detections before they are considered the same.\n" +
			"Usually used for external detections without pixel information. Value in arcseconds.")]
		[Category("Pairing")]
		[DisplayName("Same-object separation")]
		public double SameArcSep { get; set; } = 0.1;

		[Description("Maximum sum of residuals when fitting a line through the detections. Value in arcseconds.")]
		[Category("Pairing")]
		[DisplayName("Max residual sum")]
		public double MaxResidual { get; set; } = 2;

		[Description("Extra distance to add to the search radius when looking for detections. Value in arcseconds.")]
		[Category("Pairing")]
		[DisplayName("Small extra search radius")]
		public double SmallExtraSearchRadius { get; set; } = 1.0;

		[Description("Extra distance to add to the search radius if no detections are found within the small radius. Value in arcseconds.")]
		[Category("Pairing")]
		[DisplayName("Big extra search radius")]
		public double BigExtraSearchRadius { get; set; } = 3.5;

		[Description("Selects which operations are run on the input images.")]
		[Category("Core")]
		[DisplayName("Enabled operations")]
		public EnabledOperations Operations { get; set; } = (EnabledOperations)(11 + 64 + 128);

		[Description("Detection threshold on original images. Value in standard deviations.")]
		[Category("Original image recovery")]
		[DisplayName("Detection threshold")]
		public double OriginalThreshold { get; set; } = 1.75;

		[Description("Maximum radius to include in detection.")]
		[Category("Original image recovery")]
		[DisplayName("Recovery radius")]
		public double RecoveryRadius { get; set; } = 10;

		[Description("MPC Observatory code to write in MPC reports.")]
		[Category("Reporting")]
		[DisplayName("Observatory code")]
		public string ObservatoryCode { get; set; } = "950";

		[Description("Which magnitude band to report in MPC reports.")]
		[Category("Reporting")]
		[DisplayName("Magnitude Band")]
		public MPCOpticalReportFormat.MagnitudeBand MagBand { get; set; } = MPCOpticalReportFormat.MagnitudeBand.R;

		[Description("Search radius on SkyBoT name service. Value in arcseconds.")]
		[Category("Reporting")]
		[DisplayName("SkyBoT Radius")]
		public double SkyBoTDistance { get; set; } = 5;
	}
}
