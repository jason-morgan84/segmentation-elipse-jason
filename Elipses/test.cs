using System;
using System.Collections.Generic;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
/*
namespace Elipses
{
    class Program
    {
        static void Main(string[] args)
        {
            using (Image<Rgba32> input = Image.Load<Rgba32>("tinytest.tif").Frames.CloneFrame(3))
                for (int y = 0; y<input.Height; y++)
                {
                    for(int x = 0; x < input.Width;x++)
                    {
                        Console.Write("{0} ", input[x, y].R);
                    }
                    Console.Write("\n");
                }
               
        }
    }
}
*/