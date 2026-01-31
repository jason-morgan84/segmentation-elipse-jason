using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Xml;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ellipses
{

    class Program
    {
        static void Main(string[] args)
        {


            //Things that are done:
            ///////////Load test image
            ///////////Dapi channel into array
            ///////////Sobel edge detection on array
            ///////////Guassian blur
            ///////////Canny edge detection
            ///////////Binarising
            ///////////

            //Things to do
            ///////////Fill holes
            ///////////Segmentation (and minimum object size)
            ///////////Separate each segment for analysis
            ///////////Adapt to multi slice image
            ///////////Elevation Map?
            ///////////Watershedding?
            ///////////GUI



            int SegmentChannel = 4;
            int channels = 4;

            Bitmap InputBitmap = new Bitmap(@"singleslicetest.tif",true);

            int pageCount = InputBitmap.GetFrameCount(System.Drawing.Imaging.FrameDimension.Page);
            int slices = pageCount / channels;
            int ImageHeight = InputBitmap.Height;
            int ImageWidth = InputBitmap.Width;

            InputBitmap.SelectActiveFrame(System.Drawing.Imaging.FrameDimension.Page,SegmentChannel-1);
            var pal = InputBitmap.Palette;
            for (int i = 0; i < pal.Entries.Length; i++)
                pal.Entries[i] = Color.FromArgb(i, i, i);

            InputBitmap.Palette = pal;

            int[,] ImageArray = new int[ImageHeight, ImageWidth];

            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    ImageArray[y, x] = InputBitmap.GetPixel(x, y).R;
                }
            }
            Console.WriteLine("Channels: " + channels);
            Console.WriteLine("Slices: " + slices);
            Console.WriteLine("Height: " + ImageHeight);
            Console.WriteLine("Width: " + ImageWidth);


            int[,] GaussianBlurred = Filter.GaussianBlur(ImageArray, ImageHeight, ImageWidth, 1);
            int[] Histogram = Threshold.Histogram(GaussianBlurred);
            int Triangle = Threshold.TriangleThreshold(Histogram);
            Console.WriteLine("Triangle: " + Triangle);
            int[,] Thresholded = Threshold.Binarise(GaussianBlurred, ImageHeight, ImageWidth, Triangle);
            
            Output.OutputImage(GaussianBlurred, ImageHeight, ImageWidth, "Blurred.tif", 1);
            Output.OutputImage(Thresholded, ImageHeight, ImageWidth, "Thresholded.tif", 255);

        }
    }

    class Threshold
    {
        public static int[,] DoubleThreshold(int[,] SourceArray, int Height, int Width, double low, double high)
        {
            int[,] OutputArray = new int[Height, Width];
            double MaxInputValue = 0;

            foreach (double item in SourceArray) if (item > MaxInputValue) MaxInputValue = item;

            double HighThreshld = MaxInputValue * high;
            double LowThreshold = MaxInputValue * low;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (SourceArray[y, x] >= HighThreshld) OutputArray[y, x] = 2;
                    else if (SourceArray[y, x] < LowThreshold) OutputArray[y, x] = 0;
                    else OutputArray[y, x] = 1;
                }
            }
            return OutputArray;
        }

        public static int[,] Binarise(int[,] SourceArray, int Height, int Width, double Threshold)
        {
            int[,] OutputArray = new int[Height, Width];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (SourceArray[y, x] >= Threshold) OutputArray[y, x] = 1; 
                }
            }
            return OutputArray;
        }

        public static int[] Histogram(int[,] SourceArray)
        {
            //goes through each pixel in the image, increments the respective value in a 256-long array to form histogram data
            int[] OutputArray = new int[256];
            foreach (int item in SourceArray)
            {
                OutputArray[item]++;
            }
            return OutputArray;
        }

        public static int TriangleThreshold(int[] InputHistogram)
        {
            //impletmentation of triangle thresholding
            //Zack GW, Rogers WE, Latt SA. Automatic measurement of sister chromatid exchange frequency. J Histochem Cytochem. 1977 Jul;25(7):741-53. doi: 10.1177/25.7.70454. PMID: 70454.
            //NOTE - THIS ONLY WORKS WHEN PEAK IS ON THE LEFT OF THE HISTOGRAM
            //NOTE2 - this returns a slightly lower threshold than triangle in FIJI but the same as CV2 - I don't think FIJI normalises the data
            int PeakHeight = InputHistogram.Max();
            int PeakIndex = InputHistogram.IndexOf(PeakHeight);
            int MaxIndex = 0;

            for (int i = 255; i >= 0; i--)
            {
                if (InputHistogram[i] != 0)
                {
                    MaxIndex = i;
                    break;
                }
            }

            double NormalisedBinWidth = 1 / (double)(MaxIndex - PeakIndex);
            double MaxVerticalDistance = 0;
            int ThresholdBin = 0;
            for (int i = PeakIndex; i < (MaxIndex - PeakIndex); i++)
            {
                //This calculates the vertical distance between the diagonal between the peak histogram height (normalised to 1) and the histogram width (normalised to 1)
                //Techincally, the triangle algorithm needs the perpendicular distance between the line and the top of the histogram bar being analysed, but this is proportional
                //to the vertical distance (sin45 * verticaldistance)

                double VerticalDistance = 1 - (double)(NormalisedBinWidth * (i - PeakIndex)) - (double)InputHistogram[i] / PeakHeight;
             
                if (VerticalDistance > MaxVerticalDistance)
                {
                    MaxVerticalDistance = VerticalDistance;
                    ThresholdBin = i;
                }
            }
            return ThresholdBin;
        }
    }
    
    class EdgeDetection
    {
        public static void SobelEdge(int[,] SourceArray, int height, int width, out int[,] GradientIntensity, out int[,] EdgeDirection)
        {
            //returns array using sobel edge detection and intensity gradient for further steps of Canny edge detection
            double sx, sy;
            double[,] WorkingGradientIntensity = new double[height,width];
            GradientIntensity = new int[height, width];
            double MaxIntensity = 0;

            double G = 0;
            EdgeDirection = new int[height, width];


            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    //for each pixel (excluding top row, bottom row, left column and right column)
                    //convolve with both sobel operators
                    //square root sum of squares to get new value
                    sx = (-1 * SourceArray[y - 1, x - 1] + 1 * SourceArray[y - 1, x + 1]
                        - 2 * SourceArray[y, x - 1] + 2 * SourceArray[y, x + 1]
                        - 1 * SourceArray[y + 1, x - 1] + 1 * SourceArray[y + 1, x + 1]);
                    sy = (-1 * SourceArray[y - 1, x - 1] - 2 * SourceArray[y - 1, x] - 1 * SourceArray[y - 1, x + 1]
                        + 1 * SourceArray[y + 1, x - 1] + 2 * SourceArray[y + 1, x] + 1 * SourceArray[y + 1, x + 1]);
                    WorkingGradientIntensity[y, x] = Math.Sqrt(sx * sx + sy * sy);

                    //get max intensity for normalisation
                    if (WorkingGradientIntensity[y, x] > MaxIntensity) MaxIntensity = WorkingGradientIntensity[y, x];

                    //calculate gradient angle in degrees
                    G = Math.Atan2(sy, sx) * (180 / Math.PI);
                    EdgeDirection[y, x] = (int)Math.Round(G);

                    //Console.WriteLine("Y: {0}; X: {1}; Value: {2}; sx: {3}; sy: {4}; Int: {5}; G1: {6}; G2: {7}", y, x, SourceArray[y,x], sx, sy, Math.Sqrt(sx * sx + sy * sy), Math.Atan2(sy, sx) * (180 / Math.PI), G);
                }
            }

            //normalise gradient intensities
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GradientIntensity[y, x] = (int)Math.Round((WorkingGradientIntensity[y, x] / MaxIntensity) * 255);
                }
            }
        }

        public static int[,] NonMaximumSuppression(int[,] GradientIntensity, int[,] EdgeAngle, int Height, int Width)
        {
            //Carries out non-maximum suppresion using an input array of gradient intensity and edge angles taken from sobel edge detection
            //for each pixel, compares the two cells on either side, perpendicular to the edge angle - if current cell is the maximum, it becomes 1, otherwise it becomes 0.
            int[,] OutputArray = new int[Height, Width];
            for (int y = 1; y < Height - 1; y++)
            {
                for (int x = 1; x < Width - 1; x++)
                {
                    double G = EdgeAngle[y, x];

                    if ((G > 0 - 22.5 && G <= 0 + 22.5) || (G > -180 - 22.5 && G <= -180 + 22.5) || (G > 180 - 22.5 && G <= 180 + 22.5))
                    {
                        //for angles around 0 (horizontal), check east and west cells to see if current cell is the maximum
                        if (GradientIntensity[y, x] >= GradientIntensity[y, x - 1] && GradientIntensity[y, x] >= GradientIntensity[y, x + 1]) OutputArray[y, x] = GradientIntensity[y, x];
                    }
                    else if ((G > 45 - 22.5 && G <= 45 + 22.5) || (G > -135 - 22.5 && G <= -135 + 22.5))
                    {
                        //for angles around 45 degrees (y decreasing as x increases) check north-west and south east
                        if (GradientIntensity[y, x] >= GradientIntensity[y - 1, x - 1] && GradientIntensity[y, x] >= GradientIntensity[y + 1, x + 1]) OutputArray[y, x] = GradientIntensity[y, x];
                    }
                    else if ((G > 90 - 22.5 && G <= 90 + 22.5) || G > -90 - 22.5 && G <= -90 + 22.5)
                    {
                        //for angles around 90 degrees (vertical) check north and south cells
                        if (GradientIntensity[y, x] >= GradientIntensity[y - 1, x] && GradientIntensity[y, x] >= GradientIntensity[y + 1, x]) OutputArray[y, x] = GradientIntensity[y, x];
                    }
                    else if ((G > 135 - 22.5 && G <= 135 + 22.5) || (G > -45 - 22.5 && G <= -45 + 22.5))
                    {
                        //for angles around 135 degrees (y increases as x increases) check north east and south west
                        if (GradientIntensity[y, x] >= GradientIntensity[y - 1, x + 1] && GradientIntensity[y, x] >= GradientIntensity[y + 1, x - 1]) OutputArray[y, x] = GradientIntensity[y, x];
                    }

                }
            }
            return OutputArray;
        }

        public static int[,] Hysteresis(int[,] SourceArray, int Height, int Width)
        {
            int[,] OutputArray = new int[Height, Width];
            for (int y = 1; y < Height - 1; y++)
            {
                for (int x = 1; x < Width - 1; x++)
                {
                    if (SourceArray[y, x] == 2) OutputArray[y, x] = 1;
                    else if (SourceArray[y, x] == 1)
                    {
                        bool Adjacent = false;
                        int[] Nearby = { SourceArray[y - 1, x - 1], SourceArray[y - 1, x], SourceArray[y - 1, x + 1], SourceArray[y, x - 1], SourceArray[y, x + 1], SourceArray[y + 1, x - 1], SourceArray[y + 1, x], SourceArray[y + 1, x + 1] };
                        foreach (int Cell in Nearby) if (Cell == 2) Adjacent = true;
                        if (Adjacent == true) OutputArray[y, x] = 1;
                    }

                }
            }

            return OutputArray;
        }
    }
    class Filter
    {
        public static int[,] GaussianBlur(int[,] SourceArray, int Height, int Width, double sigma)
        {
            double[,] FirstPass = new double[Height, Width];
            int[,] OutputArray = new int[Height, Width];

            //define 1D kernel given sigma
            double[] Kernel = new double[(int)(sigma * 6 + 1)];
            for (int i = 0; i < Kernel.Length; i++)
            {
                double KernelPosition = i - sigma * 3;
                Kernel[i] = (1 / Math.Sqrt(2 * Math.PI * sigma * sigma)) * Math.Exp(-((KernelPosition * KernelPosition) / (2 * sigma * sigma)));
            }
            //Normalize kernel
            double KernelSum = Kernel.Sum();
            for (int i = 0; i < Kernel.Length; i++)
            {
                Kernel[i] /= KernelSum;
            }
            //Apply kernel to image in two passes
            for (int y = 1; y < Height - 1; y++)
            {
                for (int x = 1; x < Width - 1; x++)
                {
                    double XNewValue = 0;
                    for (int i = 0; i < Kernel.Length; i++)
                    {
                        int XPosition = x + i - (int)(sigma * 3);
                        //Edge handling - mirror at edges
                        if (XPosition < 0) { XPosition = Math.Abs(XPosition) + 1; }
                        else if (XPosition >= Width) { XPosition = 2 * Width - XPosition - 1; }

                        XNewValue += SourceArray[y, XPosition] * Kernel[i];
                    }
                    FirstPass[y, x] = XNewValue;
                }
            }
            for (int y = 1; y < Height - 1; y++)
            {
                for (int x = 1; x < Width - 1; x++)
                {
                    double YNewValue = 0;
                    for (int i = 0; i < Kernel.Length; i++)
                    {
                        int YPosition = y + i - (int)(sigma * 3);
                        if (YPosition < 0) { YPosition = Math.Abs(YPosition) + 1; }
                        else if (YPosition >= Height) { YPosition = 2 * Height - YPosition - 1; }

                        YNewValue += FirstPass[YPosition, x] * Kernel[i];
                    }
                    OutputArray[y, x] = (int)Math.Round(YNewValue);
                }
            }
            return OutputArray;
        }
    }
    class Output
    {
        public static void OutputImage(int[,] SourceArray, int Height, int Width, string filename, int modifier)
        {
            Bitmap OutputBitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    OutputBitmap.SetPixel(x, y, Color.FromArgb(255, SourceArray[y, x] * modifier, SourceArray[y, x] * modifier, SourceArray[y, x] * modifier));
                }
            }
            OutputBitmap.Save(filename);
        }
    }
}
