using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PixelFlow.Core
{
    public sealed class ImageData : IDisposable
    {
        private readonly Image<Rgba32> _image;
        private byte[] _pixelBuffer;
        private bool _disposed;

        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }

        public ImageData(int width, int height)
        {
            Width = width;
            Height = height;
            Stride = width * 4;
            _image = new Image<Rgba32>(width, height);
            _pixelBuffer = new byte[Stride * Height];
        }

        private ImageData(Image<Rgba32> source)
        {
            Width = source.Width;
            Height = source.Height;
            Stride = Width * 4;
            _image = source.Clone();
            _pixelBuffer = new byte[Stride * Height];
            LoadBuffer();
        }

        public static ImageData FromFile(string path)
        {
            var img = Image.Load<Rgba32>(path);
            return new ImageData(img);
        }

        private void LoadBuffer()
        {
            for (int y = 0; y < Height; y++)
            {
                var row = _image.Frames[0].PixelBuffer.DangerousGetRowSpan(y);
                for (int x = 0; x < Width; x++)
                {
                    int offset = y * Stride + x * 4;
                    _pixelBuffer[offset]     = row[x].B;
                    _pixelBuffer[offset + 1] = row[x].G;
                    _pixelBuffer[offset + 2] = row[x].R;
                    _pixelBuffer[offset + 3] = row[x].A;
                }
            }
        }

        public void CommitBuffer()
        {
            for (int y = 0; y < Height; y++)
            {
                var row = _image.Frames[0].PixelBuffer.DangerousGetRowSpan(y);
                for (int x = 0; x < Width; x++)
                {
                    int offset = y * Stride + x * 4;
                    row[x] = new Rgba32(
                        _pixelBuffer[offset + 2],
                        _pixelBuffer[offset + 1],
                        _pixelBuffer[offset],
                        _pixelBuffer[offset + 3]);
                }
            }
        }

        public (byte B, byte G, byte R, byte A) GetPixel(int x, int y)
        {
            int offset = y * Stride + x * 4;
            return (_pixelBuffer[offset], _pixelBuffer[offset + 1],
                    _pixelBuffer[offset + 2], _pixelBuffer[offset + 3]);
        }

        public void SetPixel(int x, int y, byte r, byte g, byte b, byte a = 255)
        {
            int offset = y * Stride + x * 4;
            _pixelBuffer[offset]     = b;
            _pixelBuffer[offset + 1] = g;
            _pixelBuffer[offset + 2] = r;
            _pixelBuffer[offset + 3] = a;
        }

        public byte[] GetRawBuffer() => _pixelBuffer;
        public int GetOffset(int x, int y) => y * Stride + x * 4;

        public void SaveToFile(string path)
        {
            CommitBuffer();
            _image.Save(path);
        }

        public ImageData Clone()
        {
            CommitBuffer();
            return new ImageData(_image);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _image.Dispose();
                _disposed = true;
            }
        }
    }
}
