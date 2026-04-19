using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using PixelFlow.Core;
using PixelFlow.Core.Filters;

namespace PixelFlow
{
    /// <summary>
    /// Console demo application for Team PixelFlow.
    /// Demonstrates: individual filters, pipelines, batch processing,
    /// histogram analysis, and sequential vs parallel benchmarking.
    ///
    /// Usage: dotnet run -- [image.png]
    /// If no image is provided, a synthetic test image is generated.
    /// </summary>
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║       PixelFlow — Parallel Image Processing Tool     ║");
            Console.WriteLine("║                    Team PixelFlow                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // ── 1. Load or generate test image ───────────────────────────────
            ImageData sourceImage;
            string sourceDesc;

            if (args.Length > 0 && File.Exists(args[0]))
            {
                sourceImage = ImageData.FromFile(args[0]);
                sourceDesc = $"Loaded: {args[0]} ({sourceImage.Width}x{sourceImage.Height})";
            }
            else
            {
                sourceImage = GenerateTestImage(512, 512);
                sourceDesc = "Generated synthetic 512x512 gradient image";
            }

            Console.WriteLine($"📷 Source image: {sourceDesc}");
            Console.WriteLine($"   Pixels: {sourceImage.Width * sourceImage.Height:N0} | Threads: {Environment.ProcessorCount}");
            Console.WriteLine();

            // ── 2. Register all 8 filters ────────────────────────────────────
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

            // ── 3. Apply each filter and save output ──────────────────────────
            string outDir = "output";
            Directory.CreateDirectory(outDir);

            Console.WriteLine("🎨 Applying all 8 filters...");
            Console.WriteLine(new string('─', 60));

            foreach (var filter in filters)
            {
                var sw = Stopwatch.StartNew();
                using var result = filter.ApplyParallel(sourceImage);
                sw.Stop();
                string fileName = Path.Combine(outDir, $"{filter.Name.Split('(')[0]}.png");
                result.SaveToFile(fileName);
                Console.WriteLine($"  ✓ {filter.Name,-35} → {sw.ElapsedMilliseconds,4}ms → {fileName}");
            }

            Console.WriteLine();

            // ── 4. Pipeline demo: Grayscale → Sharpen → Brightness ────────────
            Console.WriteLine("⛓  Pipeline: Grayscale → Sharpen → Brightness(+30)");
            var pipeline = new FilterPipeline { Name = "EnhancedMono" }
                .Add(new GrayscaleFilter())
                .Add(new SharpenFilter())
                .Add(new BrightnessFilter(30));

            var pipelineSw = Stopwatch.StartNew();
            using var pipelineResult = pipeline.RunParallel(sourceImage);
            pipelineSw.Stop();
            pipelineResult.SaveToFile(Path.Combine(outDir, "pipeline_EnhancedMono.png"));
            Console.WriteLine($"  ✓ Pipeline complete in {pipelineSw.ElapsedMilliseconds}ms → output/pipeline_EnhancedMono.png");
            Console.WriteLine();

            // ── 5. Histogram analysis ─────────────────────────────────────────
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

            // ── 6. Sequential vs Parallel benchmark ───────────────────────────
            Console.WriteLine("⚡ Performance Benchmark: Sequential vs Parallel");
            Console.WriteLine(new string('─', 60));
            var bench = new PerformanceBenchmark(warmupRuns: 1, measuredRuns: 3);
            var results = bench.RunAll(filters, sourceImage);
            foreach (var r in results)
                Console.WriteLine($"  {r.Summary}");

            Console.WriteLine();

            // ── 7. Batch processing demo ──────────────────────────────────────
            Console.WriteLine("📦 Batch Processing Demo (4 copies of source)...");
            var tempBatch = Path.Combine(outDir, "batch_input");
            var batchOut  = Path.Combine(outDir, "batch_output");
            Directory.CreateDirectory(tempBatch);

            // Write 4 copies as batch input
            var batchFiles = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                string f = Path.Combine(tempBatch, $"img_{i:D2}.png");
                sourceImage.SaveToFile(f);
                batchFiles.Add(f);
            }

            var batchPipeline = new FilterPipeline { Name = "BatchSepia" }
                .Add(new SepiaFilter())
                .Add(new ContrastFilter(1.3));

            var processor = new BatchProcessor();
            int progressCount = 0;
            processor.ProgressChanged += (_, e) =>
            {
                Console.WriteLine($"  [{e.Completed}/{e.Total}] {e.CurrentFile} — {e.Percentage:F0}%{(e.Error != null ? " ERROR: " + e.Error : "")}");
                progressCount++;
            };

            await processor.ProcessAsync(batchFiles, batchOut, batchPipeline, maxConcurrency: 4);
            Console.WriteLine($"  ✓ Batch complete — {batchFiles.Count} images processed → {batchOut}/");
            Console.WriteLine();

            // ── 8. Done ───────────────────────────────────────────────────────
            sourceImage.Dispose();
            Console.WriteLine("✅ All outputs saved to ./output/");
            Console.WriteLine("   Run tests: dotnet test PixelFlow.Tests.csproj");
            Console.WriteLine("   Run benchmarks: dotnet run -c Release --project PixelFlow.Benchmarks.csproj");
        }

        /// <summary>
        /// Generates a 512x512 synthetic gradient image with RGB variation
        /// so filters produce visually meaningful results without needing an input file.
        /// </summary>
        private static ImageData GenerateTestImage(int width, int height)
        {
            var img = new ImageData(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte r = (byte)(x * 255 / width);
                    byte g = (byte)(y * 255 / height);
                    byte b = (byte)((x + y) * 255 / (width + height));
                    img.SetPixel(x, y, r, g, b);
                }
            }
            img.CommitBuffer();
            return img;
        }
    }
}
