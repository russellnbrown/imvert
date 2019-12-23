using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//https://gist.github.com/ChuckSavage AppendImageExtension.cs with added bits

namespace imvert
{
    public static class IsImageExtension
    {
        static List<string> jpg;
        static List<string> bmp;
        static List<string> gif;
        static List<string> png;
        static List<string> imgExtensions = new List<string>() { ".jpg", ".png", ".bmp", ".gif" };

        public enum ImageType
        {
            JPG,
            BMP,
            GIF,
            PNG,
            NONE
        }

        const string JPG = "FF";
        const string BMP = "42";
        const string GIF = "47";
        const string PNG = "89";

        internal static bool IsImageFileExtension(FileInfo infile)
        {            
            string ext = infile.Extension.ToLower();

            if (imgExtensions.Contains(ext))
                return true;
            return false;
        }

 

        static IsImageExtension()
        {
            jpg = new List<string> { "FF", "D8" };
            bmp = new List<string> { "42", "4D" };
            gif = new List<string> { "47", "49", "46" };
            png = new List<string> { "89", "50", "4E", "47", "0D", "0A", "1A", "0A" };
        }

        public static bool IsImage(this string file, out ImageType type)
        {
            type = ImageType.NONE;
            if (string.IsNullOrWhiteSpace(file)) return false;
            if (!File.Exists(file)) return false;
            using (var stream = File.OpenRead(file))
                return stream.IsImage(out type);
        }

        public static bool IsImage(this Stream stream, out ImageType type)
        {
            type = ImageType.NONE;
            stream.Seek(0, SeekOrigin.Begin);
            string bit = stream.ReadByte().ToString("X2");
            switch (bit)
            {
                case JPG:
                    if (stream.IsImage(jpg))
                    {
                        type = ImageType.JPG;
                        return true;
                    }
                    break;
                case BMP:
                    if (stream.IsImage(bmp))
                    {
                        type = ImageType.BMP;
                        return true;
                    }
                    break;
                case GIF:
                    if (stream.IsImage(gif))
                    {
                        type = ImageType.GIF;
                        return true;
                    }
                    break;
                case PNG:
                    if (stream.IsImage(png))
                    {
                        type = ImageType.PNG;
                        return true;
                    }
                    break;
                default:
                    break;
            }
            return false;
        }

        public static bool IsImage(this Stream stream, List<string> comparer)
        {
            stream.Seek(0, SeekOrigin.Begin);
            foreach (string c in comparer)
            {
                string bit = stream.ReadByte().ToString("X2");
                if (0 != string.Compare(bit, c))
                    return false;
            }
            return true;
        }
    }
}
