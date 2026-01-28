using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq.Expressions;
using System.Security.Cryptography;
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

            //Things to do
            ///////////Adapt to multi slice image
            ///////////Elevation Map
            ///////////Watershedding
            ///////////Segmentation
            ///////////GUI
            ///////////Separate each segment for analysis


            int SegmentChannel = 4;
            int channels = 4;
            int slices;

            Image InputImage = Image.FromFile(@"SingleSliceTest.tif");
            int pageCount = InputImage.GetFrameCount(System.Drawing.Imaging.FrameDimension.Page);

            slices = pageCount / channels;
            int ImageHeight=InputImage.Height;
            int ImageWidth=InputImage.Width;
            //add variables for image height/width

            InputImage.SelectActiveFrame(System.Drawing.Imaging.FrameDimension.Page,SegmentChannel-1);

            Bitmap InputBitmap = new Bitmap(InputImage);

            int[,] ImageArray = new int[ImageHeight, ImageWidth];

            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x=0; x < ImageWidth; x++)
                {
                    ImageArray[y,x] = InputBitmap.GetPixel(x, y).R;
                }
            }
            Console.WriteLine("Channels: "+ channels);
            Console.WriteLine("Slices: " + slices);
            Console.WriteLine("Height: " + ImageHeight);
            Console.WriteLine("Width: " + ImageWidth);

            int[,] GaussianBlurred = new int[ImageHeight, ImageWidth];
            GaussianBlurred = Program.GaussianBlur(ImageArray, ImageHeight, ImageWidth, 3);

            
            int[,] EdgeDetected = new int[ImageHeight, ImageWidth];
            int[,] Gradients = new int[ImageHeight, ImageWidth];
            Program.SobelEdge(GaussianBlurred,out EdgeDetected,out Gradients);

            
            Bitmap OutputBitmap = new Bitmap(ImageWidth, ImageHeight , System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            
            for (int y = 0; y < ImageHeight - 1; y++)
            {
                for (int x = 0; x < ImageWidth - 1; x++)
                {
                    OutputBitmap.SetPixel(x, y, Color.FromArgb(255, Gradients[y, x]*64, Gradients[y, x]*64, Gradients[y, x]*64));
                }
            }
            OutputBitmap.Save("Edgy.tif");
            
        }

        static void SobelEdge(int[,] SourceArray, out int[,] GradientIntensity, out int[,] EdgeDirection)
        {
            //returns array using sobel edge detection and intensity gradient for further steps of Canny edge detection
            int height = SourceArray.GetLength(0);
            int width = SourceArray.GetLength(1);

            double sx, sy;
            double[,] WorkingIntensity = new double[height, width];
            GradientIntensity = new int[height, width];
            double MaxIntensity = 0;

            double G = 0;
            EdgeDirection = new int[height, width];


            for (int y = 1; y< height - 1;y++)
            {
                for(int x = 1; x<width-1;x++)
                {
                    //for each pixel (excluding top row, bottom row, left column and right column)
                    //convolve with both sobel operators
                    //square root sum of squares to get new value
                    sx = (1 * SourceArray[y - 1, x - 1] + 2 * SourceArray[y - 1, x] + 1 * SourceArray[y - 1, x + 1] - 1 * SourceArray[y + 1, x - 1] - 2 * SourceArray[y + 1, x] - 1 * SourceArray[y + 1, x + 1]);
                    sy = (1 * SourceArray[x - 1, y - 1] + 2 * SourceArray[x - 1, y] + 1 * SourceArray[x - 1, y + 1] - 1 * SourceArray[x + 1, y - 1] - 2 * SourceArray[x + 1, y] - 1 * SourceArray[x + 1, y + 1]);
                    WorkingIntensity[y,x] = Math.Sqrt(sx * sx + sy * sy);
                    if (WorkingIntensity[y, x] > MaxIntensity) MaxIntensity = WorkingIntensity[y,x];

                    G = Math.Atan2(sy , sx)*(180/Math.PI);

                    if ((G > 0 - 22.5 && G <= 0 + 22.5) || (G > -180 - 22.5 && G <= -180 + 22.5) || (G > 180 - 22.5 && G <= 180 + 22.5)) { G = 0; } //Round to G = 0 for horizontal
                    else if ((G > 45 - 22.5 && G <= 45 + 22.5) || (G > -135 - 22.5 && G <= -135 + 22.5)) { G = 1; } // Round to G = 1 to diagonal increasing as x increases
                    else if ((G > 90 - 22.5 && G <= 90 + 22.5) || G > -90 - 22.5 && G <= -90 + 22.5) { G = 2; } //Round to G = 2 for vertical
                    else if ((G > 135 - 22.5 && G <= 135 + 22.5) || (G > -45 - 22.5 && G <= -45 + 22.5)) { G = 3; } // Round to G = 3 for diagonal decreasing as x increases

                    if (G!=0 & G!= 1 & G!= 2 & G!=3) Console.WriteLine(G);
                    EdgeDirection[y, x] = (int)G;
                }
            }
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    WorkingIntensity[y, x] = (WorkingIntensity[y, x] / MaxIntensity) * 255;
                    GradientIntensity[y, x] = (int)WorkingIntensity[y, x];
                }
            }
        }

        static int[,] GaussianBlur(int[,] SourceArray, int Height, int Width, int sigma)
        {
            int[,] FirstPass = new int[Height, Width];
            int[,] OutputArray = new int[Height, Width];

            //define 1D kernel given sigma
            double[] Kernel = new double[sigma * 6 + 1];
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
                    Double XNewValue = 0;
                    for (int i = 0; i < Kernel.Length; i++)
                    {
                        int XPosition = x + i - sigma * 3;
                        //Edge handling - mirror at edges
                        if (XPosition < 0) { XPosition = Math.Abs(XPosition) + 1; }
                        else if (XPosition >= Width) { XPosition = 2 * Width - XPosition - 1; }

                        XNewValue += SourceArray[y, XPosition] * Kernel[i];
                    }
                    FirstPass[y, x] = (int)XNewValue;
                }
            }

            for (int y = 1; y < Height - 1; y++)
            {
                for (int x = 1; x < Width - 1; x++)
                {
                    double YNewValue = 0;
                    for (int i = 0; i < Kernel.Length; i++)
                    {
                        int YPosition = y + i - sigma * 3;
                        if (YPosition < 0) { YPosition = Math.Abs(YPosition) + 1; }
                        else if (YPosition >= Height) { YPosition = 2 * Height - YPosition - 1; }

                        YNewValue += FirstPass[YPosition, x] * Kernel[i];


                    }
                    OutputArray[y, x] = (int)YNewValue;
                }                
            }
            //consider neccessity of normalisation
            return OutputArray;
        }

        static int[,] NonMaximumSuppression(int[,] SourceArray)
        {
            int[,] OutputArray = new int[SourceArray.GetLength(0), SourceArray.GetLength(1)];
            return OutputArray;
        }
    }
}
