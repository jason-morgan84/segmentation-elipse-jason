using System;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;

namespace Ellipses
{

    class Program
    {

        static void Main(string[] args)
        {
            //Things to do
            //Load test image
            //image format: multi channel tiff
            //iniially: single stack tiff, keep in mind this will be built up to something more complx
            //Get DAPI channel into array
            //sobel edge detection + watershedding

            int SegmentChannel = 4;
            int channels = 4;
            int slices;

            Image InputImage = Image.FromFile(@"C:\\SingleSliceTest.tif");
            int pageCount = InputImage.GetFrameCount(System.Drawing.Imaging.FrameDimension.Page);

            slices = pageCount / channels;

            InputImage.SelectActiveFrame(System.Drawing.Imaging.FrameDimension.Page,SegmentChannel-1);

            //change a to something that makes more sense
            Bitmap a = new Bitmap(InputImage);

            int[,] ImageArray = new int[a.Height, a.Width];

            for (int y = 0; y < a.Height; y++)
            {
                for (int x=0; x < a.Width; x++)
                {
                    ImageArray[y,x] = a.GetPixel(x, y).R;
                }
            }
            Console.WriteLine(a.GetPixel(240, 210));
            Console.WriteLine("Channels: "+ channels);
            Console.WriteLine("Slices: " + slices);

            int[,] EdgeDetected = new int[a.Height, a.Width];

            EdgeDetected = Program.SobelEdge(ImageArray);


            Bitmap OutputBitmap = new Bitmap(a.Width, a.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++)
                {
                    OutputBitmap.SetPixel(x, y, Color.FromArgb(255, EdgeDetected[y, x], EdgeDetected[y, x], EdgeDetected[y, x]));
                }
            }
            OutputBitmap.Save("edgey.tif");
            //next: watershedding, gaussian blur, GUI
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
    }
}