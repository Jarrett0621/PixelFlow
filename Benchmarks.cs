using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PixelFlow.Core;
using PixelFlow.Core.Filters;

namespace PixelFlow.Benchmarks
{
    /// <summary>
    /// BenchmarkDotNet micro-benchmarks.
    /// Run with: dotnet run -c Release
    /// BenchmarkDotNet handles warmup, multiple iterations, and statistical analysis.
    /// </summary>
    [MemoryDiagnoser]          // Reports allocations (Gen0/1/2, bytes allocated)
    [ThreadingDiagnoser]       // Reports thread contention
    [SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
    public class FilterBenchmarks
    {
        private ImageData _image512 = null!;
        private ImageData _image1024 = null!;

        [GlobalSetup]
        public void Setup()
        {
            _image512  = CreateGradient(512, 512);
            _image1024 = CreateGradient(1024, 1024);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _image512.Dispose();
            _image1024.Dispose();
        }

        // ── Sequential filter benchmarks ──────────────────────────────────
        [Benchmark] public void Grayscale_512_Sequential()
            => new GrayscaleFilter().Apply(_image512).Dispose();

        [Benchmark] public void Grayscale_512_Parallel()
            => new GrayscaleFilter().ApplyParallel(_image512).Dispose();

        [Benchmark] public void GaussianBlur_512_Sequential()
            => new GaussianBlurFilter(3).Apply(_image512).Dispose();

        [Benchmark] public void GaussianBlur_512_Parallel()
            => new GaussianBlurFilter(3).ApplyParallel(_image512).Dispose();

        [Benchmark] public void EdgeDetection_512_Sequential()
            => new EdgeDetectionFilter().Apply(_image512).Dispose();

        [Benchmark] public void EdgeDetection_512_Parallel()
            => new EdgeDetectionFilter().ApplyParallel(_image512).Dispose();

        [Benchmark] public void Sharpen_512_Sequential()
            => new SharpenFilter().Apply(_image512).Dispose();

        [Benchmark] public void Sharpen_512_Parallel()
            => new SharpenFilter().ApplyParallel(_image512).Dispose();

        [Benchmark] public void FullPipeline_512_Sequential()
        {
            var p = new FilterPipeline()
                .Add(new GrayscaleFilter())
                .Add(new SharpenFilter())
                .Add(new BrightnessFilter(20));
            p.Run(_image512).Dispose();
        }

        [Benchmark] public void FullPipeline_512_Parallel()
        {
            var p = new FilterPipeline()
                .Add(new GrayscaleFilter())
                .Add(new SharpenFilter())
                .Add(new BrightnessFilter(20));
            p.RunParallel(_image512).Dispose();
        }

        [Benchmark] public void HistogramAnalyze_512_Sequential()
        {
            var h = new HistogramAnalyzer();
            h.Analyze(_image512);
        }

        [Benchmark] public void HistogramAnalyze_512_Parallel()
        {
            var h = new HistogramAnalyzer();
            h.AnalyzeParallel(_image512);
        }

        // ── 1024x1024 scale tests ─────────────────────────────────────────
        [Benchmark] public void GaussianBlur_1024_Sequential()
            => new GaussianBlurFilter(5).Apply(_image1024).Dispose();

        [Benchmark] public void GaussianBlur_1024_Parallel()
            => new GaussianBlurFilter(5).ApplyParallel(_image1024).Dispose();

        private static ImageData CreateGradient(int w, int h)
        {
            var img = new ImageData(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    byte r = (byte)(x * 255 / w);
                    byte g = (byte)(y * 255 / h);
                    byte b = (byte)((x + y) * 255 / (w + h));
                    img.SetPixel(x, y, r, g, b);
                }
            img.CommitBuffer();
            return img;
        }
    }

    public class BenchmarkProgram
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<FilterBenchmarks>();
        }
    }
}
