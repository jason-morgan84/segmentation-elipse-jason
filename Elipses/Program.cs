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


            /******************PRIORITY - Compare output so far to other versions online*********************/

            //Things that are done:
            ///////////Load test image
            ///////////Dapi channel into array
            ///////////Sobel edge detection on array
            ///////////Guassian blur

            //Things to do
            ///////////Double thresholding
            ///////////Hysteresis
            ///////////Adapt to multi slice image
            ///////////Elevation Map
            ///////////Watershedding
            ///////////Segmentation
            ///////////GUI
            ///////////Separate each segment for analysis


            int SegmentChannel = 4;
            int channels = 4;
            int slices;

            //Image InputImage = Image.FromFile(@"rectangle.tif");
            Image InputImage = Image.FromFile(@"SingleSliceTest.tif");
            int pageCount = InputImage.GetFrameCount(System.Drawing.Imaging.FrameDimension.Page);

            slices = pageCount / channels;
            int ImageHeight=InputImage.Height;
            int ImageWidth=InputImage.Width;

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
            Console.WriteLine("Gaussian blur complete");
            int[,] EdgeDetected = new int[ImageHeight, ImageWidth];
            double[,] Gradients = new double[ImageHeight, ImageWidth];
            int[,] NMS = new int[ImageHeight, ImageWidth];
            //note: Gaussian blur skipped for testing
            Program.SobelEdge(GaussianBlurred, ImageHeight, ImageWidth, out EdgeDetected,out Gradients);
            Console.WriteLine("Edge detection complete");
            NMS = Program.NonMaximumSuppression(EdgeDetected, Gradients, ImageHeight, ImageWidth);
            Console.WriteLine("Non-maximum suppression complete");
                        
            Bitmap OutputBitmap = new Bitmap(ImageWidth, ImageHeight , System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            
            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    OutputBitmap.SetPixel(x, y, Color.FromArgb(255, EdgeDetected[y, x], EdgeDetected[y, x], EdgeDetected[y, x]));
                }
            }
            OutputBitmap.Save("Edgy.tif");

            Bitmap OutputBitmap2 = new Bitmap(ImageWidth, ImageHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    OutputBitmap2.SetPixel(x, y, Color.FromArgb(255, NMS[y, x], NMS[y, x], NMS[y, x]));
                }
            }
            OutputBitmap2.Save("thinned.tif");
            
        }

        static void SobelEdge(int[,] SourceArray, int height, int width, out int[,] GradientIntensity, out double[,] EdgeDirection)
        {
            //returns array using sobel edge detection and intensity gradient for further steps of Canny edge detection
            double sx, sy;
            double[,] WorkingIntensity = new double[height, width];
            GradientIntensity = new int[height, width];
            double MaxIntensity = 0;

            double G = 0;
            EdgeDirection = new double[height, width];


            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    //for each pixel (excluding top row, bottom row, left column and right column)
                    //convolve with both sobel operators
                    //square root sum of squares to get new value
                    sx = (-1 * SourceArray[y - 1, x - 1] + 1 * SourceArray[y - 1, x + 1]
                        - 2 * SourceArray[y, x - 1] + 2 * SourceArray[y, x+1]
                        - 1 * SourceArray[y + 1, x - 1] + 1 * SourceArray[y + 1, x + 1]);
                    sy = (-1 * SourceArray[y - 1, x - 1] -2 * SourceArray[y - 1, x] -1 * SourceArray[y - 1, x + 1] 
                        + 1 * SourceArray[y + 1, x - 1] + 2 * SourceArray[y + 1, x] + 1 * SourceArray[y + 1, x + 1]);
                    WorkingIntensity[y,x] = Math.Sqrt(sx * sx + sy * sy);

                    //get max intensity for normalisation
                    if (WorkingIntensity[y, x] > MaxIntensity) MaxIntensity = WorkingIntensity[y,x];

                    //calculate gradient angle in degrees
                    G = Math.Atan2(sy , sx)*(180/Math.PI);
                    EdgeDirection[y, x] = G;
                  
                    //Console.WriteLine("Y: {0}; X: {1}; Value: {2}; sx: {3}; sy: {4}; Int: {5}; G1: {6}; G2: {7}", y, x, SourceArray[y,x], sx, sy, Math.Sqrt(sx * sx + sy * sy), Math.Atan2(sy, sx) * (180 / Math.PI), G);
                }
            }
            
            //normalise gradient intensities
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
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
            return OutputArray;
        }

        static int[,] NonMaximumSuppression(int[,] SourceArray, double[,] EdgeAngle, int Height, int Width)
        {
            int[,] OutputArray = new int[Height, Width];
            for (int y = 1; y < Height - 1; y++)
            {
                for (int x = 1; x < Width - 1; x++)
                {
                    double G = EdgeAngle[y,x];
                    //Round angles to 0, 45, 90 or 135
                    //Console.Write("\n");
                   // Console.Write("y: {0}, x: {1}, G: {2}, Value: {3}", y, x, G, SourceArray[y,x]);
                    if ((G > 0 - 22.5 && G <= 0 + 22.5) || (G > -180 - 22.5 && G <= -180 + 22.5) || (G > 180 - 22.5 && G <= 180 + 22.5)) 
                    {
                        //for angles around 0 (horizontal), check east and west cells to see if current cell is the maximum (or and greater than equals to catch
                        //if cells are identical
                        if ((SourceArray[y, x] > SourceArray[y, x - 1] && SourceArray[y, x] >= SourceArray[y, x + 1]) ||
                            (SourceArray[y, x] >= SourceArray[y, x - 1] && SourceArray[y, x] > SourceArray[y, x + 1]))
                        {
                            OutputArray[y, x] = SourceArray[y, x];
                        }
                    } 
                    else if ((G > 45 - 22.5 && G <= 45 + 22.5) || (G > -135 - 22.5 && G <= -135 + 22.5))
                    {
                        //for angles around 45 degrees (y decreasing as x increases) check north-west and south east
                        if ((SourceArray[y, x] > SourceArray[y - 1, x - 1] && SourceArray[y, x] >= SourceArray[y + 1, x + 1]) ||
                            (SourceArray[y, x] >= SourceArray[y - 1, x - 1] && SourceArray[y, x] > SourceArray[y + 1, x + 1]))
                        {
                            OutputArray[y, x] = SourceArray[y, x];
                        }
                    } 
                    else if ((G > 90 - 22.5 && G <= 90 + 22.5) || G > -90 - 22.5 && G <= -90 + 22.5) 
                    {
                        //for angles around 90 degrees (vertical) check north and south cells
                        if ((SourceArray[y, x] > SourceArray[y - 1, x] && SourceArray[y, x] >= SourceArray[y + 1, x]) ||
                            (SourceArray[y, x] >= SourceArray[y - 1, x] && SourceArray[y, x] > SourceArray[y + 1, x]))
                        {
                            OutputArray[y, x] = SourceArray[y, x];
                        }
                    } 
                    else if ((G > 135 - 22.5 && G <= 135 + 22.5) || (G > -45 - 22.5 && G <= -45 + 22.5)) 
                    {
                        //for angles around 135 degrees (y increases as x increases) check north east and south west
                        if ((SourceArray[y, x] > SourceArray[y - 1, x + 1] && SourceArray[y, x] >= SourceArray[y + 1, x - 1]) ||
                            (SourceArray[y, x] >= SourceArray[y - 1, x + 1] && SourceArray[y, x] > SourceArray[y + 1, x - 1]))
                            {
                            OutputArray[y, x] = SourceArray[y, x];
                            }
                    } 

                }
            }
            return OutputArray;
        }
    }
}
