using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PixelFlow.Core
{
    /// <summary>
    /// High-performance image data wrapper with direct pixel buffer access.
    /// Avoids per-pixel GetPixel/SetPixel overhead by using unsafe bitmap pointers.
    /// </summary>
    public sealed class ImageData : IDisposable
    {
        private readonly Bitmap _bitmap;
        private byte[]? _pixelBuffer;
        private bool _disposed;
        private bool _bufferDirty;

        public int Width { get; }
        public int Height { get; }
        public int Stride { get; private set; }

        public ImageData(int width, int height)
        {
            Width = width;
            Height = height;
            _bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            LoadBuffer();
        }

        public ImageData(Bitmap source)
        {
            Width = source.Width;
            Height = source.Height;
            _bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(_bitmap);
            g.DrawImage(source, 0, 0);
            LoadBuffer();
        }

        public static ImageData FromFile(string path)
        {
            using var bmp = new Bitmap(path);
            return new ImageData(bmp);
        }

        private void LoadBuffer()
        {
            var rect = new Rectangle(0, 0, Width, Height);
            var bmpData = _bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Stride = bmpData.Stride;
            _pixelBuffer = new byte[Math.Abs(Stride) * Height];
            Marshal.Copy(bmpData.Scan0, _pixelBuffer, 0, _pixelBuffer.Length);
            _bitmap.UnlockBits(bmpData);
        }

        public void CommitBuffer()
        {
            if (_pixelBuffer == null || !_bufferDirty) return;
            var rect = new Rectangle(0, 0, Width, Height);
            var bmpData = _bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(_pixelBuffer, 0, bmpData.Scan0, _pixelBuffer.Length);
            _bitmap.UnlockBits(bmpData);
            _bufferDirty = false;
        }

        /// <summary>Gets a pixel. B=0, G=1, R=2, A=3 channel order (BGRA).</summary>
        public (byte B, byte G, byte R, byte A) GetPixel(int x, int y)
        {
            int offset = y * Stride + x * 4;
            return (_pixelBuffer![offset], _pixelBuffer[offset + 1],
                    _pixelBuffer[offset + 2], _pixelBuffer[offset + 3]);
        }

        /// <summary>Sets a pixel. Marks buffer as dirty for later commit.</summary>
        public void SetPixel(int x, int y, byte r, byte g, byte b, byte a = 255)
        {
            int offset = y * Stride + x * 4;
            _pixelBuffer![offset] = b;
            _pixelBuffer[offset + 1] = g;
            _pixelBuffer[offset + 2] = r;
            _pixelBuffer[offset + 3] = a;
            _bufferDirty = true;
        }

        public byte[] GetRawBuffer() => _pixelBuffer!;
        public int GetOffset(int x, int y) => y * Stride + x * 4;

        public Bitmap ToBitmap()
        {
            CommitBuffer();
            return new Bitmap(_bitmap);
        }

        public void SaveToFile(string path)
        {
            CommitBuffer();
            _bitmap.Save(path);
        }

        public ImageData Clone()
        {
            CommitBuffer();
            return new ImageData(_bitmap);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _bitmap.Dispose();
                _disposed = true;
            }
        }
    }
}
