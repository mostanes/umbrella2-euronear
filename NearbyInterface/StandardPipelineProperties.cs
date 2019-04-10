using System.ComponentModel;

namespace Umbrella2.Pipeline.ViaNearby
{
	public partial class StandardPipeline
	{
		[Description("Threshold for masking stars.")]
		[TypeConverter(typeof(ExpandableObjectConverter))]
		[Category("Star masking")]
		[DisplayName("Star masking threshold")]
		public Threshold MaskThreshold { get; set; } = new Threshold() { High = 3.5, Low = 2 };

		[Description("Amount to add to the radius to be masked.")]
		[Category("Star masking")]
		[DisplayName("Extra mask radius")]
		public double ExtraMaskRadius { get; set; } = 2;

		[Description("Amount by which to scale the star radius on masking.")]
		[Category("Star masking")]
		[DisplayName("Mask radius multiplier")]
		public double MaskRadiusMultiplier { get; set; } = 1.15;

		[Description("Threshold of detection for the blob detection algorithm")]
		[Category("Blob detection")]
		[DisplayName("Blob detector threshold")]
		[TypeConverter(typeof(ExpandableObjectConverter))]
		public Threshold DotDetectorThreshold { get; set; } = new Threshold() { High = 5, Low = 2.5 };

		[Description("Minimum number of pixels to count a detection")]
		[Category("Blob detection")]
		[DisplayName("Blob min pixels")]
		public int DotMinPix { get; set; } = 15;

		[Description("Largest deviation from the mean a pixel can have before it is excluded from computing the local mean.")]
		[Category("Blob detection")]
		[DisplayName("Non-representative threshold")]
		public double NonrepresentativeThreshold { get; set; } = 50;

		[Description("BITPIX of the generated intermediate files.")]
		[Category("General pipeline properties")]
		[DisplayName("Standard BITPIX")]
		public int StandardBITPIX { get; set; } = -32;

		[Description("Radius of the Restricted Mean Shot noise removal filter. Value in pixels.")]
		[Category("Core")]
		[DisplayName("Shot noise radius")]
		public int PoissonRadius { get; set; } = 3;

		[Description("Use CoreFilter instead of Restricted Mean to also remove badpixels.")]
		[Category("Core")]
		[DisplayName("Use CoreFilter")]
		public bool UseCoreFilter { get; set; } = false;

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

		[Description("Maximum thickness of a blob/trail")]
		[Category("Filtering")]
		[DisplayName("Max line thickness")]
		public double MaxLineThickness { get; set; } = 15;

		[Description("Maximum distance (in pixels) between 2 blobs/segments part of the same object")]
		[Category("Filtering")]
		[DisplayName("Pairwise matching max distance")]
		public double MaxPairmatchDistance { get; set; } = 40;

		[Description("Number of overlapping pixels before 2 detections are directly considered part of the same object")]
		[Category("Filtering")]
		[DisplayName("Pairwise matching mix pixels")]
		public int MixMatch { get; set; } = 10;

		[Description("Selects which operations are run on the input images.")]
		[Category("Core")]
		[DisplayName("Enabled operations")]
		[Editor(typeof(General.Utils.FlagEnumUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
		public EnabledOperations Operations { get; set; } = (EnabledOperations) 11;
	}
}
