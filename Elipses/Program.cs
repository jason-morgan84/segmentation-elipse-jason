using System;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Security.Cryptography;

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
            
            //Things to do
            ///////////Adapt to multi slice image
            ///////////Guassian blur
            ///////////Watershedding
            ///////////Segmentation
            ///////////GUI
            ///////////Separate each segment for analysis


            int SegmentChannel = 4;
            int channels = 4;
            int slices;

            Image InputImage = Image.FromFile(@"C:\\SingleSliceTest.tif");
            int pageCount = InputImage.GetFrameCount(System.Drawing.Imaging.FrameDimension.Page);

            slices = pageCount / channels;
            //add variables for image height/width

            InputImage.SelectActiveFrame(System.Drawing.Imaging.FrameDimension.Page,SegmentChannel-1);

            Bitmap InputBitmap = new Bitmap(InputImage);

            int[,] ImageArray = new int[InputBitmap.Height, InputBitmap.Width];

            for (int y = 0; y < InputBitmap.Height; y++)
            {
                for (int x=0; x < InputBitmap.Width; x++)
                {
                    ImageArray[y,x] = InputBitmap.GetPixel(x, y).R;
                }
            }
            Console.WriteLine("Channels: "+ channels);
            Console.WriteLine("Slices: " + slices);

            int[,] GaussianBlurred = new int[InputBitmap.Height, InputBitmap.Width];
            GaussianBlurred = Program.GaussianBlur(ImageArray, 3, "Mirror");

            /*
            int[,] EdgeDetected = new int[InputBitmap.Height, InputBitmap.Width];
            EdgeDetected = Program.SobelEdge(ImageArray);


            Bitmap OutputBitmap = new Bitmap(InputBitmap.Width, InputBitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            for (int y = 0; y < InputBitmap.Height; y++)
            {
                for (int x = 0; x < InputBitmap.Width; x++)
                {
                    OutputBitmap.SetPixel(x, y, Color.FromArgb(255, EdgeDetected[y, x], EdgeDetected[y, x], EdgeDetected[y, x]));
                }
            }
            OutputBitmap.Save("edgey.tif");
            */
        }

        static int[,] SobelEdge(int[,] SourceArray)
        {
            //for each pixel (excluding top row, bottom row, left column and right column)
            //convolve with both sobel operators
            //square root sum of squares to get new value
            int sx, sy;
            int[,] OutputArray = new int[SourceArray.GetLength(0),SourceArray.GetLength(1)];
            for (int y = 1; y<SourceArray.GetLength(0)-1;y++)
            {
                for(int x = 1; x<SourceArray.GetLength(1)-1;x++)
                {
                    sx = (1 * SourceArray[y - 1, x - 1] + 2 * SourceArray[y - 1, x] + 1 * SourceArray[y - 1, x + 1] - 1 * SourceArray[y + 1, x - 1] - 2 * SourceArray[y + 1, x] - 1 * SourceArray[y + 1, x + 1]);
                    sy = (1 * SourceArray[x - 1, y - 1] + 2 * SourceArray[x - 1, y] + 1 * SourceArray[x - 1, y + 1] - 1 * SourceArray[x + 1, y - 1] - 2 * SourceArray[x + 1, y] - 1 * SourceArray[x + 1, y + 1]);
                    OutputArray[y,x] = Convert.ToInt32(Math.Sqrt(sx * sx + sy * sy));
                    if (OutputArray[y, x] > 255) OutputArray[y, x] = 255;
                }
            }
            return OutputArray;
        }

        static int[,] GaussianBlur(int[,] SourceArray, int sigma, string EdgeHandling)
        {
            int[,] OutputArray = new int[SourceArray.GetLength(0), SourceArray.GetLength(1)];

            //define 1D kernel given sigma
            double[] Kernel = new double[sigma * 6 + 1];
            for (int i = 0; i < Kernel.Length; i++)
            {
                double KernelPosition = i - sigma * 3;
                Kernel[i] = (1 / Math.Sqrt(2 * Math.PI * sigma * sigma)) * Math.Exp(-((KernelPosition * KernelPosition) / (2 * sigma * sigma)));
            }
            Array.ForEach(Kernel, Console.WriteLine);
            //Apply kernel to image
            //First row by row, multiplying value of each pixel
            for (int y = 1; y < SourceArray.GetLength(0) - 1; y++)
            {
                for (int x = 1; x < SourceArray.GetLength(1) - 1; x++)
                {
                    int NewValue = 0;
                    for (int i = 0; i < Kernel.Length; i++)
                    {
                        //consider replacing relative position with actual position

                        //edge handling currently only works when going below 0
                        //need to change to going above max array size
                        int RelativePosition = i - sigma * 3;
                        Console.WriteLine(x + RelativePosition);
                        if (x + RelativePosition < 0)
                        {
                            if (EdgeHandling=="Black")
                            {
                                NewValue += 0;
                            }
                            else if (EdgeHandling=="Mirror")
                            {
                                //RelativePosition = Math.Abs(RelativePosition) - 1;
                                NewValue += (int)(SourceArray[y, Math.Abs(x + RelativePosition) + 1] * Kernel[i]);
                            }
                        }
                        else
                        {
                            NewValue += (int)(SourceArray[y, x + RelativePosition] * Kernel[i]);
                        }
                        
                    }
                }
            }
                    //Then column by column
                    //conisder different methods of edge handling
                    //consider neccessity of normalisation
                    return OutputArray;
        }
    }
}