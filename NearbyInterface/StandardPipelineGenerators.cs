using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Umbrella2.IO;
using Umbrella2.IO.FITS;

namespace Umbrella2.Pipeline.ViaNearby
{
	public partial class StandardPipeline
	{
		static bool SelectByReg(Tracklet Input)
		{
			double Th = 0.2 / Input.Velocity.ArcSecMin;
			if (1 - Math.Abs(Input.VelReg.R_TR) > Th) return false;
			if (1 - Math.Abs(Input.VelReg.R_RD) > Th) return false;
			if (1 - Math.Abs(Input.VelReg.R_TD) > Th) return false;
			return true;
		}

		static FitsImage EnsureImage(string RunDir, string Name, int Number, FitsImage Model, int BitPix, Action<FitsImage> Algorithm, List<ImageProperties> ExtraProperties = null)
		{
			string ImagePath = Path.Combine(RunDir, Name + Number.ToString() + ".fits");
			if (File.Exists(ImagePath)) return new FitsImage(MMapFitsFile.OpenReadFile(ImagePath));
			FICHV values = Model.CopyHeader().ChangeBitPix(BitPix);
			MMapFitsFile file = MMapFitsFile.OpenWriteFile(ImagePath, values.Header);
			FitsImage Image = new FitsImage(file);
			Algorithm(Image);
			return Image;
		}

		static double[,] PoissonKernel(int Lat)
		{
			int MedX = 2 * Lat + 1, MedY = 2 * Lat + 1;
			int Center = Lat + 1;
			double[,] Dex = new double[MedX, MedY];
			double Sum = 0;
			int i, j;
			for (i = 0; i < MedY; i++) for (j = 0; j < MedX; j++)
				{
					double R = Math.Sqrt((i - Center) * (i - Center) + (j - Center) * (j - Center));
					Sum += Dex[i, j] = 1 / (1 + R * Math.Log(Lat + R));
				}
			for (i = 0; i < MedY; i++) for (j = 0; j < MedX; j++) Dex[i, j] /= Sum;
			return Dex;
		}

		static double[,] GenerateSecondMedian()
		{
			const int Lat = 5;
			const int MedX = 2 * Lat + 1, MedY = 2 * Lat + 1;
			double[,] Dex = new double[MedX, MedY];
			double Sum = 0;
			int i, j;
			for (i = 0; i < MedY; i++) for (j = 0; j < MedX; j++) Dex[i, j] = 1;
			for (i = 3; i < 8; i++) for (j = 3; j < 8; j++) Dex[i, j] = 4;
			for (i = 0; i < MedY; i++) for (j = 0; j < MedX; j++) Sum += Dex[i, j];
			for (i = 0; i < MedY; i++) for (j = 0; j < MedX; j++) Dex[i, j] /= Sum;
			return Dex;
		}
	}
}
