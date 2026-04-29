using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using PixelFlow.Core;
using PixelFlow.Core.Filters;

namespace PixelFlow
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║       PixelFlow — Parallel Image Processing Tool     ║");
            Console.WriteLine("║                    Team PixelFlow                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.WriteLine();

            ImageData sourceImage = GenerateTestImage(512, 512);
            Console.WriteLine("📷 Source image: Generated synthetic 512x512 gradient image");
            Console.WriteLine($"   Pixels: {512 * 512:N0} | Threads: {Environment.ProcessorCount}");
            Console.WriteLine();

            var filters = new List<IFilter>
            {
                new GrayscaleFilter(),
                new SepiaFilter(),
                new BrightnessFilter(40),
                new ContrastFilter(1.8),
                new InvertFilter(),
                new SharpenFilter(),
                new GaussianBlurFilter(3),
                new EdgeDetectionFilter(80)
            };

            string outDir = "output";
            Directory.CreateDirectory(outDir);

            Console.WriteLine("🎨 Applying all 8 filters...");
            Console.WriteLine(new string('─', 60));

            foreach (var filter in filters)
            {
                var sw = Stopwatch.StartNew();
                using var result = filter.ApplyParallel(sourceImage);
                sw.Stop();
                Console.WriteLine($"  ✓ {filter.Name,-35} → {sw.ElapsedMilliseconds,4}ms");
            }

            Console.WriteLine();

            Console.WriteLine("⛓  Pipeline: Grayscale → Sharpen → Brightness(+30)");
            var pipeline = new FilterPipeline { Name = "EnhancedMono" }
                .Add(new GrayscaleFilter())
                .Add(new SharpenFilter())
                .Add(new BrightnessFilter(30));

            var pipelineSw = Stopwatch.StartNew();
            using var pipelineResult = pipeline.RunParallel(sourceImage);
            pipelineSw.Stop();
            Console.WriteLine($"  ✓ Pipeline complete in {pipelineSw.ElapsedMilliseconds}ms");
            Console.WriteLine();

            Console.WriteLine("📊 Histogram Analysis (parallel)...");
            var analyzer = new HistogramAnalyzer();
            analyzer.AnalyzeParallel(sourceImage);
            var rStats = analyzer.ComputeStats(analyzer.RedChannel);
            var gStats = analyzer.ComputeStats(analyzer.GreenChannel);
            var bStats = analyzer.ComputeStats(analyzer.BlueChannel);
            var lStats = analyzer.ComputeStats(analyzer.Luminance);
            Console.WriteLine($"  Red   — Mean: {rStats.Mean:F1}, StdDev: {rStats.StdDev:F1}, Range: [{rStats.Min}–{rStats.Max}]");
            Console.WriteLine($"  Green — Mean: {gStats.Mean:F1}, StdDev: {gStats.StdDev:F1}, Range: [{gStats.Min}–{gStats.Max}]");
            Console.WriteLine($"  Blue  — Mean: {bStats.Mean:F1}, StdDev: {bStats.StdDev:F1}, Range: [{bStats.Min}–{bStats.Max}]");
            Console.WriteLine($"  Lum   — Mean: {lStats.Mean:F1}, StdDev: {lStats.StdDev:F1}, Peak bin: {lStats.PeakBin}");
            Console.WriteLine();

            Console.WriteLine("⚡ Performance Benchmark: Sequential vs Parallel");
            Console.WriteLine(new string('─', 60));
            var bench = new PerformanceBenchmark(warmupRuns: 1, measuredRuns: 3);
            var results = bench.RunAll(filters, sourceImage);
            foreach (var r in results)
                Console.WriteLine($"  {r.Summary}");

            Console.WriteLine();
            sourceImage.Dispose();
            Console.WriteLine("✅ Done!");
        }

        private static ImageData GenerateTestImage(int width, int height)
        {
            var img = new ImageData(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    byte r = (byte)(x * 255 / width);
                    byte g = (byte)(y * 255 / height);
                    byte b = (byte)((x + y) * 255 / (width + height));
                    img.SetPixel(x, y, r, g, b);
                }
            img.CommitBuffer();
            return img;
        }
    }
}
