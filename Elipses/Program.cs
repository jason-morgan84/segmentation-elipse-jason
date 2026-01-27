using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq.Expressions;
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

            int[,] GaussianBlurred = new int[ImageHeight, ImageWidth];
            GaussianBlurred = Program.GaussianBlur(ImageArray, ImageHeight, ImageWidth, 3, "Mirror");

            /*
            int[,] EdgeDetected = new int[InputBitmap.Height, InputBitmap.Width];
            EdgeDetected = Program.SobelEdge(ImageArray);

            */
            Bitmap OutputBitmap = new Bitmap(ImageHeight, ImageWidth, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            
            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    OutputBitmap.SetPixel(x, y, Color.FromArgb(255, GaussianBlurred[y, x], GaussianBlurred[y, x], GaussianBlurred[y, x]));
                }
            }
            OutputBitmap.Save("blurry.tif");
            
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

        static int[,] GaussianBlur(int[,] SourceArray, int Height, int Width, int sigma, string EdgeHandling)
        {
            int[,] OutputArray = new int[SourceArray.GetLength(0), SourceArray.GetLength(1)];

            //define 1D kernel given sigma
            double[] Kernel = new double[sigma * 6 + 1];
            for (int i = 0; i < Kernel.Length; i++)
            {
                double KernelPosition = i - sigma * 3;
                Kernel[i] = (1 / Math.Sqrt(2 * Math.PI * sigma * sigma)) * Math.Exp(-((KernelPosition * KernelPosition) / (2 * sigma * sigma)));
            }

            //Apply kernel to image
            //First row by row, multiplying value of each pixel
            for (int y = 1; y < Height - 1; y++)
            {
                for (int x = 1; x < Width - 1; x++)
                {
                    int NewValue = 0;
                    for (int i = 0; i < Kernel.Length; i++)
                    {
                        //can both directions be done in the same for loop? should be possible
                        int Position = x + i - sigma * 3;
                        /*
                        try
                        {
                            NewValue += (int)(SourceArray[y, Position] * Kernel[i]);
                        }
                        catch (Exception e)
                        {
                            NewValue += 0;
                        }
                        */
                        
                        if (Position >= 0 && Position < Width )
                        {
                            NewValue += (int)(SourceArray[y, Position] * Kernel[i]);
                            
                        }
                        else
                        {
                            NewValue += 0;
                        }
                        /*
                        if (Position < 0)
                        {
                            if (EdgeHandling=="Black")
                            {
                                NewValue += 0;
                            }
                            else if (EdgeHandling=="Mirror")
                            {
                                Position = Math.Abs(Position) + 1;
                                NewValue += (int)(SourceArray[y, Position] * Kernel[i]);
                            }
                        }
                        else if (Position> Width - 1)
                        {
                            if (EdgeHandling=="Black")
                            {
                                NewValue += 0;
                            }
                            else if (EdgeHandling=="Mirror")
                            {
                                Position = (SourceArray.GetLength(1) - 1) - (Position - (Width-1) - 1);
                                NewValue += (int)(SourceArray[y, Position] * Kernel[i]);
                            }
                        }
                        else
                        {
                            NewValue += (int)(SourceArray[y, Position] * Kernel[i]);
                        }
                        */
                        
                    }

                    OutputArray[y, x] = NewValue;
                }
                
            }
            //Then column by column
            //consider neccessity of normalisation

            return OutputArray;
        }
    }
}
