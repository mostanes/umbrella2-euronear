using System;
using System.Collections;
using Umbrella2.Algorithms.Images;
using Umbrella2.IO.FITS;

namespace Umbrella2.Pipeline.Standard
{
	static class PipelineHelperFunctions
	{
		internal static double[,] PoissonKernel(int Lat)
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

		internal static double[] LinearizeArray(double[,] Array2D)
		{
			double[] Lin = new double[Array2D.Length];
			Buffer.BlockCopy(Array2D, 0, Lin, 0, Lin.Length * sizeof(double));
			return Lin;
		}

		internal static double[] LinearizedPoissonKernel(int Lat) => LinearizeArray(PoissonKernel(Lat));

		internal static double[] LinearizedMedianKernel() => LinearizeArray(GenerateSecondMedian());

		internal static double[,] GenerateSecondMedian(int Lat = 5, int CenterLat = 2 /* TODO: move in config */)
		{
			int MedX = 2 * Lat + 1, MedY = 2 * Lat + 1;
			double[,] Dex = new double[MedX, MedY];
			double Sum = 0;
			int i, j;
			for (i = 0; i < MedY; i++) for (j = 0; j < MedX; j++) Dex[i, j] = 1;
			for (i = Lat - CenterLat; i < Lat + CenterLat + 1; i++) for (j = Lat - CenterLat; j < Lat + CenterLat + 1; j++) Dex[i, j] = 4;
			for (i = 0; i < MedY; i++) for (j = 0; j < MedX; j++) Sum += Dex[i, j];
			for (i = 0; i < MedY; i++) for (j = 0; j < MedX; j++) Dex[i, j] /= Sum;
			return Dex;
		}

		internal static BitArray[] ExtractBadpixel(string Badpixel, Action<string> Logger)
		{
			BitArray[] map = null;
			if (Badpixel != null)
			{
				Logger("Checking badpixel file");
				MMapFitsFile fif_bad = MMapFitsFile.OpenReadFile(Badpixel);
				FitsImage BadpixMap = new FitsImage(fif_bad);
				map = BadpixelFilter.CreateFilter(BadpixMap);
			}

			return map;
		}
	}
}
