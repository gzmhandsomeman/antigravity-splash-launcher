using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AntigravityLauncher
{
    public static class IconAssetLoader
    {
        private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        private static readonly IntPtr RT_ICON = new IntPtr(3);
        private static readonly IntPtr RT_GROUP_ICON = new IntPtr(14);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        private delegate bool EnumResNameProc(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpszType, EnumResNameProc lpEnumFunc, IntPtr lParam);

        public static BitmapSource LoadOriginalIcon(string fallbackExePath)
        {
            BitmapSource bmp = LoadFromEmbeddedResource();
            if (bmp != null) return bmp;

            bmp = LoadFromLocalFile();
            if (bmp != null) return bmp;

            bmp = LoadFromExecutableIconResource(fallbackExePath);
            if (bmp != null) return bmp;

            return CreateFallbackPrismIcon();
        }

        private static BitmapSource CreateFallbackPrismIcon()
        {
            int size = 512;
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                LinearGradientBrush bgBrush = new LinearGradientBrush();
                bgBrush.StartPoint = new Point(0.1, 0.0);
                bgBrush.EndPoint = new Point(0.9, 1.0);
                bgBrush.GradientStops.Add(new GradientStop(Colors.White, 0.0));
                bgBrush.GradientStops.Add(new GradientStop(Color.FromRgb(241, 245, 249), 1.0));
                bgBrush.Freeze();
                dc.DrawRoundedRectangle(bgBrush, null, new Rect(48, 48, 416, 416), 96, 96);

                Geometry ribbon = Geometry.Parse("M 256,120 C 292,120 356,288 378,364 C 384,384 360,392 344,376 C 306,336 286,284 256,284 C 226,284 206,336 168,376 C 152,392 128,384 134,364 C 156,288 220,120 256,120 Z");
                LinearGradientBrush spec = new LinearGradientBrush();
                spec.StartPoint = new Point(0.5, 0.0);
                spec.EndPoint = new Point(0.5, 1.0);
                spec.GradientStops.Add(new GradientStop(Color.FromRgb(251, 188, 5), 0.0));
                spec.GradientStops.Add(new GradientStop(Color.FromRgb(234, 67, 53), 0.25));
                spec.GradientStops.Add(new GradientStop(Color.FromRgb(52, 168, 83), 0.45));
                spec.GradientStops.Add(new GradientStop(Color.FromRgb(66, 133, 244), 0.85));
                spec.Freeze();
                dc.DrawGeometry(spec, null, ribbon);
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        private static BitmapSource LoadFromEmbeddedResource()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream("antigravity-icon.png"))
                {
                    if (stream == null) return null;
                    return DecodePngStream(stream);
                }
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource LoadFromLocalFile()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(baseDir, "antigravity-icon.png");
                if (!File.Exists(path)) return null;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return DecodePngStream(fs);
                }
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource LoadFromExecutableIconResource(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return null;
            }

            IntPtr hMod = LoadLibraryEx(exePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
            if (hMod == IntPtr.Zero) return null;

            try
            {
                IntPtr foundGroup = IntPtr.Zero;
                EnumResourceNames(hMod, RT_GROUP_ICON, delegate(IntPtr m, IntPtr t, IntPtr name, IntPtr lp)
                {
                    foundGroup = name;
                    return false;
                }, IntPtr.Zero);

                if (foundGroup == IntPtr.Zero) return null;

                IntPtr hGroupRes = FindResource(hMod, foundGroup, RT_GROUP_ICON);
                if (hGroupRes == IntPtr.Zero) return null;

                IntPtr hGroupLoad = LoadResource(hMod, hGroupRes);
                IntPtr pGroup = LockResource(hGroupLoad);
                uint groupSize = SizeofResource(hMod, hGroupRes);
                if (pGroup == IntPtr.Zero || groupSize < 6) return null;

                byte[] groupBuf = new byte[groupSize];
                Marshal.Copy(pGroup, groupBuf, 0, (int)groupSize);

                int count = BitConverter.ToUInt16(groupBuf, 4);
                int bestWidth = -1;
                ushort bestId = 0;

                for (int i = 0; i < count; i++)
                {
                    int off = 6 + i * 14;
                    if (off + 14 > groupBuf.Length) break;
                    int w = groupBuf[off] == 0 ? 256 : groupBuf[off];
                    ushort id = BitConverter.ToUInt16(groupBuf, off + 12);
                    if (w > bestWidth)
                    {
                        bestWidth = w;
                        bestId = id;
                    }
                }

                if (bestId == 0) return null;

                IntPtr hIconRes = FindResource(hMod, new IntPtr(bestId), RT_ICON);
                if (hIconRes == IntPtr.Zero) return null;

                IntPtr hIconLoad = LoadResource(hMod, hIconRes);
                IntPtr pIcon = LockResource(hIconLoad);
                uint iconSize = SizeofResource(hMod, hIconRes);
                if (pIcon == IntPtr.Zero || iconSize < 8) return null;

                byte[] iconBytes = new byte[iconSize];
                Marshal.Copy(pIcon, iconBytes, 0, (int)iconSize);

                bool isPng = iconBytes[0] == 0x89 && iconBytes[1] == 0x50 && iconBytes[2] == 0x4E && iconBytes[3] == 0x47;
                if (isPng)
                {
                    using (MemoryStream ms = new MemoryStream(iconBytes))
                    {
                        return DecodePngStream(ms);
                    }
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                FreeLibrary(hMod);
            }

            return null;
        }

        private static BitmapSource DecodePngStream(Stream stream)
        {
            PngBitmapDecoder decoder = new PngBitmapDecoder(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames == null || decoder.Frames.Count == 0) return null;
            BitmapSource frame = decoder.Frames[0];
            if (frame.Format != PixelFormats.Bgra32 && frame.Format != PixelFormats.Pbgra32)
            {
                FormatConvertedBitmap converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                return converted;
            }
            frame.Freeze();
            return frame;
        }

        public static BitmapSource CreateTintedGhost(BitmapSource src, Color tint)
        {
            if (src == null) return null;

            BitmapSource bgra = src;
            if (bgra.Format != PixelFormats.Bgra32)
            {
                FormatConvertedBitmap conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
                conv.Freeze();
                bgra = conv;
            }

            int width = bgra.PixelWidth;
            int height = bgra.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            bgra.CopyPixels(pixels, stride, 0);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte a = pixels[i + 3];
                if (a == 0)
                {
                    pixels[i] = 0;
                    pixels[i + 1] = 0;
                    pixels[i + 2] = 0;
                }
                else
                {
                    pixels[i] = tint.B;
                    pixels[i + 1] = tint.G;
                    pixels[i + 2] = tint.R;
                }
            }

            WriteableBitmap wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            wb.Freeze();
            return wb;
        }

        public static BitmapSource CreateFloorReflection(BitmapSource src)
        {
            if (src == null) return null;

            BitmapSource bgra = src;
            if (bgra.Format != PixelFormats.Bgra32)
            {
                FormatConvertedBitmap conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
                conv.Freeze();
                bgra = conv;
            }

            int width = bgra.PixelWidth;
            int height = bgra.PixelHeight;
            int stride = width * 4;
            byte[] srcPixels = new byte[height * stride];
            bgra.CopyPixels(srcPixels, stride, 0);

            int refHeight = Math.Max(1, (int)(height * 0.40));
            int startSrcY = Math.Min(height - 1, (int)(height * 0.91));
            byte[] dstPixels = new byte[refHeight * stride];

            for (int y = 0; y < refHeight; y++)
            {
                int srcY = startSrcY - y;
                if (srcY < 0) break;
                double t = (double)y / refHeight;
                double fade = Math.Pow(1.0 - t, 1.85);
                int srcRow = srcY * stride;
                int dstRow = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int si = srcRow + x * 4;
                    int di = dstRow + x * 4;
                    byte a = (byte)Math.Round(srcPixels[si + 3] * fade);
                    dstPixels[di] = srcPixels[si];
                    dstPixels[di + 1] = srcPixels[si + 1];
                    dstPixels[di + 2] = srcPixels[si + 2];
                    dstPixels[di + 3] = a;
                }
            }

            WriteableBitmap wb = new WriteableBitmap(width, refHeight, 96, 96, PixelFormats.Bgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, width, refHeight), dstPixels, stride, 0);
            wb.Freeze();
            return wb;
        }
    }
}
