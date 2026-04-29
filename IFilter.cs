using System;
using System.Threading;
using System.Threading.Tasks;

namespace PixelFlow.Core
{
    public interface IFilter
    {
        string Name { get; }
        string Description { get; }
        ImageData Apply(ImageData input);
        ImageData ApplyParallel(ImageData input, CancellationToken ct = default);
    }

    public abstract class FilterBase : IFilter
    {
        public abstract string Name { get; }
        public abstract string Description { get; }

        protected abstract void ProcessPixel(ImageData input, ImageData output, int x, int y);

        public virtual ImageData Apply(ImageData input)
        {
            var output = input.Clone();
            for (int y = 0; y < input.Height; y++)
                for (int x = 0; x < input.Width; x++)
                    ProcessPixel(input, output, x, y);
            output.CommitBuffer();
            return output;
        }

        public virtual ImageData ApplyParallel(ImageData input, CancellationToken ct = default)
        {
            var output = input.Clone();
            Parallel.For(0, input.Height,
                new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
                y =>
                {
                    for (int x = 0; x < input.Width; x++)
                        ProcessPixel(input, output, x, y);
                });
            output.CommitBuffer();
            return output;
        }

        protected static byte Clamp(double value) => (byte)Math.Max(0, Math.Min(255, (int)value));
        protected static byte Clamp(int value) => (byte)Math.Max(0, Math.Min(255, value));
        protected static byte Luminance(byte r, byte g, byte b)
            => (byte)(0.299 * r + 0.587 * g + 0.114 * b);
    }
}
