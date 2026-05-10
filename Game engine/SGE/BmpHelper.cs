using System;
using System.IO;

namespace SGE
{
    public static class BmpHelper
    {
        public static void SavePlaceholderBmp(string path, string sceneName)
        {
            const int width = 320;
            const int height = 180;
            var pixels = new byte[width * height * 3];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var offset = (y * width + x) * 3;
                    var r = (byte)((x * 255) / (width - 1));
                    var g = (byte)((y * 255) / (height - 1));
                    var b = (byte)(128);
                    pixels[offset + 0] = b;
                    pixels[offset + 1] = g;
                    pixels[offset + 2] = r;
                }
            }
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);
            var rowSize = ((24 * width + 31) / 32) * 4;
            var pixelDataSize = rowSize * height;
            var fileSize = 14 + 40 + pixelDataSize;
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(fileSize);
            writer.Write((short)0);
            writer.Write((short)0);
            writer.Write(14 + 40);
            writer.Write(40);
            writer.Write(width);
            writer.Write(height);
            writer.Write((short)1);
            writer.Write((short)24);
            writer.Write(0);
            writer.Write(pixelDataSize);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            var padding = new byte[rowSize - width * 3];
            for (var y = height - 1; y >= 0; y--)
            {
                var rowStart = y * width * 3;
                writer.Write(pixels, rowStart, width * 3);
                writer.Write(padding);
            }
        }
    }
}
