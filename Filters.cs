using System;
using System.Threading;
using System.Threading.Tasks;

namespace PixelFlow.Core.Filters
{
    // ─────────────────────────────────────────────────────────────────────────
    // FILTER 1: Grayscale
    // Converts RGB to luminance using ITU-R BT.601 coefficients.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class GrayscaleFilter : FilterBase
    {
        public override string Name => "Grayscale";
        public override string Description => "Converts image to grayscale using luminance weighting (ITU-R BT.601).";

        protected override void ProcessPixel(ImageData input, ImageData output, int x, int y)
        {
            var (b, g, r, a) = input.GetPixel(x, y);
            byte gray = Luminance(r, g, b);
            output.SetPixel(x, y, gray, gray, gray, a);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILTER 2: Sepia
    // Applies classic warm-tone sepia matrix transform.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class SepiaFilter : FilterBase
    {
        public override string Name => "Sepia";
        public override string Description => "Warm vintage tone using the standard sepia color matrix.";

        protected override void ProcessPixel(ImageData input, ImageData output, int x, int y)
        {
            var (b, g, r, a) = input.GetPixel(x, y);
            double nr = r * 0.393 + g * 0.769 + b * 0.189;
            double ng = r * 0.349 + g * 0.686 + b * 0.168;
            double nb = r * 0.272 + g * 0.534 + b * 0.131;
            output.SetPixel(x, y, Clamp(nr), Clamp(ng), Clamp(nb), a);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILTER 3: Brightness
    // Adds a constant offset to all channels.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class BrightnessFilter : FilterBase
    {
        private readonly int _offset;

        /// <param name="offset">Positive = brighter, negative = darker. Range -255 to 255.</param>
        public BrightnessFilter(int offset = 50) => _offset = offset;

        public override string Name => $"Brightness({_offset:+#;-#;0})";
        public override string Description => $"Adjusts brightness by offset {_offset}.";

        protected override void ProcessPixel(ImageData input, ImageData output, int x, int y)
        {
            var (b, g, r, a) = input.GetPixel(x, y);
            output.SetPixel(x, y, Clamp(r + _offset), Clamp(g + _offset), Clamp(b + _offset), a);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILTER 4: Contrast
    // Scales pixel distance from the midpoint (128).
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class ContrastFilter : FilterBase
    {
        private readonly double _factor;

        /// <param name="factor">1.0 = no change, >1 = more contrast, <1 = less.</param>
        public ContrastFilter(double factor = 1.5) => _factor = factor;

        public override string Name => $"Contrast({_factor:F2}x)";
        public override string Description => $"Contrast scale factor {_factor}.";

        protected override void ProcessPixel(ImageData input, ImageData output, int x, int y)
        {
            var (b, g, r, a) = input.GetPixel(x, y);
            output.SetPixel(x, y,
                Clamp((r - 128) * _factor + 128),
                Clamp((g - 128) * _factor + 128),
                Clamp((b - 128) * _factor + 128), a);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILTER 5: Invert
    // Subtracts each channel from 255.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class InvertFilter : FilterBase
    {
        public override string Name => "Invert";
        public override string Description => "Inverts all color channels (negative image).";

        protected override void ProcessPixel(ImageData input, ImageData output, int x, int y)
        {
            var (b, g, r, a) = input.GetPixel(x, y);
            output.SetPixel(x, y, (byte)(255 - r), (byte)(255 - g), (byte)(255 - b), a);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILTER 6: Sharpen
    // 3x3 sharpening kernel: center=5, cardinal neighbors=-1.
    // Kernel filters need full override — they read neighboring pixels.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class SharpenFilter : FilterBase
    {
        public override string Name => "Sharpen";
        public override string Description => "Enhances edges using a 3×3 sharpening convolution kernel.";

        private static readonly int[,] Kernel =
        {
            { 0, -1,  0 },
            { -1,  5, -1 },
            { 0, -1,  0 }
        };

        // Override entirely — kernel filters cannot use ProcessPixel (needs neighbor reads)
        public override ImageData Apply(ImageData input)
            => ApplyKernel(input, sequential: true);

        public override ImageData ApplyParallel(ImageData input, CancellationToken ct = default)
            => ApplyKernel(input, sequential: false, ct: ct);

        private ImageData ApplyKernel(ImageData input, bool sequential, CancellationToken ct = default)
        {
            var output = input.Clone();

            void ProcessRow(int y)
            {
                for (int x = 0; x < input.Width; x++)
                {
                    double r = 0, g = 0, b = 0;
                    for (int ky = -1; ky <= 1; ky++)
                    {
                        int ny = Math.Clamp(y + ky, 0, input.Height - 1);
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int nx = Math.Clamp(x + kx, 0, input.Width - 1);
                            var (pb, pg, pr, _) = input.GetPixel(nx, ny);
                            int k = Kernel[ky + 1, kx + 1];
                            r += pr * k; g += pg * k; b += pb * k;
                        }
                    }
                    var (_, _, _, a) = input.GetPixel(x, y);
                    output.SetPixel(x, y, Clamp(r), Clamp(g), Clamp(b), a);
                }
            }

            if (sequential)
                for (int y = 0; y < input.Height; y++) ProcessRow(y);
            else
                Parallel.For(0, input.Height,
                    new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
                    y => ProcessRow(y));

            output.CommitBuffer();
            return output;
        }

        protected override void ProcessPixel(ImageData input, ImageData output, int x, int y)
            => throw new NotSupportedException("SharpenFilter uses ApplyKernel directly.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILTER 7: Gaussian Blur
    // Separable 2-pass Gaussian with configurable radius.
    // Two-pass (horizontal then vertical) is O(n*r) not O(n*r²).
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class GaussianBlurFilter : FilterBase
    {
        private readonly int _radius;
        private readonly double[] _kernel;

        public GaussianBlurFilter(int radius = 3)
        {
            _radius = radius;
            _kernel = BuildKernel(radius);
        }

        public override string Name => $"GaussianBlur(r={_radius})";
        public override string Description => $"Separable Gaussian blur with radius {_radius}.";

        private static double[] BuildKernel(int radius)
        {
            double sigma = radius / 3.0;
            int size = radius * 2 + 1;
            var k = new double[size];
            double sum = 0;
            for (int i = 0; i < size; i++)
            {
                double x = i - radius;
                k[i] = Math.Exp(-(x * x) / (2 * sigma * sigma));
                sum += k[i];
            }
            for (int i = 0; i < size; i++) k[i] /= sum;
            return k;
        }

        public override ImageData Apply(ImageData input)
            => ApplyTwoPass(input, sequential: true);

        public override ImageData ApplyParallel(ImageData input, CancellationToken ct = default)
            => ApplyTwoPass(input, sequential: false, ct: ct);

        private ImageData ApplyTwoPass(ImageData input, bool sequential, CancellationToken ct = default)
        {
            // Pass 1: horizontal blur → temp
            var temp = input.Clone();
            var opts = new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount };

            void HorizRow(int y)
            {
                for (int x = 0; x < input.Width; x++)
                {
                    double r = 0, g = 0, b = 0;
                    for (int k = -_radius; k <= _radius; k++)
                    {
                        int nx = Math.Clamp(x + k, 0, input.Width - 1);
                        var (pb, pg, pr, _) = input.GetPixel(nx, y);
                        double w = _kernel[k + _radius];
                        r += pr * w; g += pg * w; b += pb * w;
                    }
                    var (_, _, _, a) = input.GetPixel(x, y);
                    temp.SetPixel(x, y, Clamp(r), Clamp(g), Clamp(b), a);
                }
            }

            if (sequential)
                for (int y = 0; y < input.Height; y++) HorizRow(y);
            else
                Parallel.For(0, input.Height, opts, y => HorizRow(y));

            temp.CommitBuffer();

            // Pass 2: vertical blur → output
            var output = temp.Clone();

            void VertRow(int y)
            {
                for (int x = 0; x < input.Width; x++)
                {
                    double r = 0, g = 0, b = 0;
                    for (int k = -_radius; k <= _radius; k++)
                    {
                        int ny = Math.Clamp(y + k, 0, input.Height - 1);
                        var (pb, pg, pr, _) = temp.GetPixel(x, ny);
                        double w = _kernel[k + _radius];
                        r += pr * w; g += pg * w; b += pb * w;
                    }
                    var (_, _, _, a) = temp.GetPixel(x, y);
                    output.SetPixel(x, y, Clamp(r), Clamp(g), Clamp(b), a);
                }
            }

            if (sequential)
                for (int y = 0; y < input.Height; y++) VertRow(y);
            else
                Parallel.For(0, input.Height, opts, y => VertRow(y));

            output.CommitBuffer();
            temp.Dispose();
            return output;
        }

        protected override void ProcessPixel(ImageData input, ImageData output, int x, int y)
            => throw new NotSupportedException("GaussianBlurFilter uses two-pass apply.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILTER 8: Edge Detection (Sobel)
    // Computes gradient magnitude using Sobel Gx and Gy kernels.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class EdgeDetectionFilter : FilterBase
    {
        private readonly double _threshold;

        public EdgeDetectionFilter(double threshold = 128.0) => _threshold = threshold;

        public override string Name => "EdgeDetection(Sobel)";
        public override string Description => "Detects edges using Sobel gradient operator.";

        public override ImageData Apply(ImageData input) => ApplySobel(input, sequential: true);
        public override ImageData ApplyParallel(ImageData input, CancellationToken ct = default)
            => ApplySobel(input, sequential: false, ct: ct);

        private ImageData ApplySobel(ImageData input, bool sequential, CancellationToken ct = default)
        {
            // Convert to grayscale first
            var gray = new GrayscaleFilter().Apply(input);
            var output = gray.Clone();
            var opts = new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount };

            void ProcessRow(int y)
            {
                for (int x = 0; x < input.Width; x++)
                {
                    double gx = 0, gy = 0;
                    int[,] kx = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
                    int[,] ky = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = Math.Clamp(y + dy, 0, input.Height - 1);
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = Math.Clamp(x + dx, 0, input.Width - 1);
                            var (b, _, _, _) = gray.GetPixel(nx, ny); // gray: r=g=b
                            gx += b * kx[dy + 1, dx + 1];
                            gy += b * ky[dy + 1, dx + 1];
                        }
                    }
                    double mag = Math.Sqrt(gx * gx + gy * gy);
                    byte edge = mag > _threshold ? (byte)255 : (byte)0;
                    output.SetPixel(x, y, edge, edge, edge);
                }
            }

            if (sequential)
                for (int y = 0; y < input.Height; y++) ProcessRow(y);
            else
                Parallel.For(0, input.Height, opts, y => ProcessRow(y));

            output.CommitBuffer();
            gray.Dispose();
            return output;
        }

        protected override void ProcessPixel(ImageData input, ImageData output, int x, int y)
            => throw new NotSupportedException("EdgeDetectionFilter uses ApplySobel.");
    }
}
