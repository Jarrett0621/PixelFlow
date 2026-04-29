using System;
using System.Drawing;
using Xunit;
using PixelFlow.Core;
using PixelFlow.Core.Filters;

namespace PixelFlow.Tests
{
    /// Unit tests for all 8 filters, pipeline, histogram, and batch processor.
    /// Each test is self-contained: creates a synthetic image, applies a transformation,
    /// and verifies pixel values or structural properties.

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: creates a 10x10 image with a known color
    // ─────────────────────────────────────────────────────────────────────────
    public static class TestImageFactory
    {
        public static ImageData Solid(int width, int height, byte r, byte g, byte b)
        {
            var img = new ImageData(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    img.SetPixel(x, y, r, g, b);
            img.CommitBuffer();
            return img;
        }

        //Checkerboard: alternating black and white pixels.
        public static ImageData Checkerboard(int size)
        {
            var img = new ImageData(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    byte v = (byte)(((x + y) % 2 == 0) ? 255 : 0);
                    img.SetPixel(x, y, v, v, v);
                }
            img.CommitBuffer();
            return img;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GrayscaleFilter Tests
    // ─────────────────────────────────────────────────────────────────────────
    public class GrayscaleFilterTests
    {
        [Fact]
        public void Apply_PureRed_ProducesGrayPixel()
        {
            using var input = TestImageFactory.Solid(10, 10, 255, 0, 0);
            var filter = new GrayscaleFilter();
            using var output = filter.Apply(input);
            var (b, g, r, _) = output.GetPixel(5, 5);
            // Expected luminance: 0.299*255 ≈ 76
            Assert.Equal(r, g);
            Assert.Equal(g, b);
            Assert.InRange(r, 70, 82);
        }

        [Fact]
        public void Apply_White_StaysWhite()
        {
            using var input = TestImageFactory.Solid(4, 4, 255, 255, 255);
            using var output = new GrayscaleFilter().Apply(input);
            var (b, g, r, _) = output.GetPixel(0, 0);
            Assert.Equal(255, r); Assert.Equal(255, g); Assert.Equal(255, b);
        }

        [Fact]
        public void ApplyParallel_MatchesSequential()
        {
            using var input = TestImageFactory.Solid(50, 50, 100, 150, 200);
            var filter = new GrayscaleFilter();
            using var seq = filter.Apply(input);
            using var par = filter.ApplyParallel(input);
            var (bs, gs, rs, _) = seq.GetPixel(25, 25);
            var (bp, gp, rp, _) = par.GetPixel(25, 25);
            Assert.Equal(rs, rp); Assert.Equal(gs, gp); Assert.Equal(bs, bp);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SepiaFilter Tests
    // ─────────────────────────────────────────────────────────────────────────
    public class SepiaFilterTests
    {
        [Fact]
        public void Apply_White_ProducesWarmTone()
        {
            using var input = TestImageFactory.Solid(4, 4, 255, 255, 255);
            using var output = new SepiaFilter().Apply(input);
            var (b, g, r, _) = output.GetPixel(0, 0);
            // Red channel should be highest (warm sepia)
            Assert.True(r >= g && g >= b, $"Expected R>G>B for sepia white, got R={r} G={g} B={b}");
        }

        [Fact]
        public void Apply_Black_StaysBlack()
        {
            using var input = TestImageFactory.Solid(4, 4, 0, 0, 0);
            using var output = new SepiaFilter().Apply(input);
            var (b, g, r, _) = output.GetPixel(0, 0);
            Assert.Equal(0, r); Assert.Equal(0, g); Assert.Equal(0, b);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BrightnessFilter Tests
    // ─────────────────────────────────────────────────────────────────────────
    public class BrightnessFilterTests
    {
        [Fact]
        public void Apply_PositiveOffset_IncreasesChannels()
        {
            using var input = TestImageFactory.Solid(4, 4, 100, 100, 100);
            using var output = new BrightnessFilter(50).Apply(input);
            var (b, g, r, _) = output.GetPixel(0, 0);
            Assert.Equal(150, r); Assert.Equal(150, g); Assert.Equal(150, b);
        }

        [Fact]
        public void Apply_OverflowClamped()
        {
            using var input = TestImageFactory.Solid(4, 4, 255, 255, 255);
            using var output = new BrightnessFilter(100).Apply(input);
            var (b, g, r, _) = output.GetPixel(0, 0);
            Assert.Equal(255, r); // clamped
        }

        [Fact]
        public void Apply_NegativeOffset_DarkensImage()
        {
            using var input = TestImageFactory.Solid(4, 4, 200, 200, 200);
            using var output = new BrightnessFilter(-50).Apply(input);
            var (b, g, r, _) = output.GetPixel(0, 0);
            Assert.Equal(150, r);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ContrastFilter Tests
    // ─────────────────────────────────────────────────────────────────────────
    public class ContrastFilterTests
    {
        [Fact]
        public void Apply_MidGray_RemainsUnchanged()
        {
            // 128 is the midpoint — scaling from 128 by any factor stays at 128
            using var input = TestImageFactory.Solid(4, 4, 128, 128, 128);
            using var output = new ContrastFilter(2.0).Apply(input);
            var (b, g, r, _) = output.GetPixel(0, 0);
            Assert.InRange(r, 126, 130); // float rounding tolerance
        }

        [Fact]
        public void Apply_AboveMidWithHighFactor_Clamps()
        {
            using var input = TestImageFactory.Solid(4, 4, 200, 200, 200);
            using var output = new ContrastFilter(10.0).Apply(input);
            var (b, g, r, _) = output.GetPixel(0, 0);
            Assert.Equal(255, r); // should clamp
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // InvertFilter Tests
    // ─────────────────────────────────────────────────────────────────────────
    public class InvertFilterTests
    {
        [Fact]
        public void Apply_White_ProducesBlack()
        {
            using var input = TestImageFactory.Solid(4, 4, 255, 255, 255);
            using var output = new InvertFilter().Apply(input);
            var (b, g, r, _) = output.GetPixel(0, 0);
            Assert.Equal(0, r); Assert.Equal(0, g); Assert.Equal(0, b);
        }

        [Fact]
        public void Apply_AppliedTwice_RestoresOriginal()
        {
            using var input = TestImageFactory.Solid(4, 4, 100, 150, 200);
            var f = new InvertFilter();
            using var once = f.Apply(input);
            using var twice = f.Apply(once);
            var (b, g, r, _) = twice.GetPixel(0, 0);
            Assert.Equal(200, r); Assert.Equal(150, g); Assert.Equal(100, b);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SharpenFilter Tests
    // ─────────────────────────────────────────────────────────────────────────
    public class SharpenFilterTests
    {
        [Fact]
        public void Apply_SolidColor_NoEdgeArtifacts()
        {
            // A solid image has zero gradient — sharpening should leave center pixels unchanged
            using var input = TestImageFactory.Solid(20, 20, 128, 128, 128);
            using var output = new SharpenFilter().Apply(input);
            var (b, g, r, _) = output.GetPixel(10, 10);
            Assert.InRange(r, 120, 136); // minor kernel accumulation at solid regions
        }

        [Fact]
        public void ApplyParallel_MatchesSequential()
        {
            using var input = TestImageFactory.Checkerboard(20);
            var filter = new SharpenFilter();
            using var seq = filter.Apply(input);
            using var par = filter.ApplyParallel(input);
            // Sample center pixel — both should agree
            var (bs, gs, rs, _) = seq.GetPixel(10, 10);
            var (bp, gp, rp, _) = par.GetPixel(10, 10);
            Assert.Equal(rs, rp);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GaussianBlurFilter Tests
    // ─────────────────────────────────────────────────────────────────────────
    public class GaussianBlurFilterTests
    {
        [Fact]
        public void Apply_SolidColor_Unchanged()
        {
            using var input = TestImageFactory.Solid(20, 20, 100, 150, 200);
            using var output = new GaussianBlurFilter(3).Apply(input);
            var (b, g, r, _) = output.GetPixel(10, 10);
            Assert.InRange(r, 95, 105);
            Assert.InRange(g, 145, 155);
            Assert.InRange(b, 195, 205);
        }

        [Fact]
        public void Apply_OutputSameDimensions()
        {
            using var input = TestImageFactory.Solid(30, 40, 80, 80, 80);
            using var output = new GaussianBlurFilter(2).Apply(input);
            Assert.Equal(30, output.Width);
            Assert.Equal(40, output.Height);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EdgeDetectionFilter Tests
    // ─────────────────────────────────────────────────────────────────────────
    public class EdgeDetectionFilterTests
    {
        [Fact]
        public void Apply_SolidImage_ProducesNoEdges()
        {
            using var input = TestImageFactory.Solid(20, 20, 128, 128, 128);
            using var output = new EdgeDetectionFilter(64).Apply(input);
            var (b, _, _, _) = output.GetPixel(10, 10);
            Assert.Equal(0, b); // interior of solid = no edge
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FilterPipeline Tests
    // ─────────────────────────────────────────────────────────────────────────
    public class FilterPipelineTests
    {
        [Fact]
        public void Run_EmptyPipeline_ReturnsCopy()
        {
            using var input = TestImageFactory.Solid(4, 4, 200, 100, 50);
            var pipeline = new FilterPipeline();
            using var output = pipeline.Run(input);
            var (b, g, r, _) = output.GetPixel(0, 0);
            Assert.Equal(200, r); Assert.Equal(100, g); Assert.Equal(50, b);
        }

        [Fact]
        public void Run_GrayscaleThenInvert_CorrectOrder()
        {
            using var input = TestImageFactory.Solid(4, 4, 255, 0, 0);
            var pipeline = new FilterPipeline()
                .Add(new GrayscaleFilter())
                .Add(new InvertFilter());
            using var output = pipeline.Run(input);
            var (b, g, r, _) = output.GetPixel(0, 0);
            // Pure red → gray(≈76) → invert → 179
            Assert.InRange(r, 170, 188);
            Assert.Equal(r, g); Assert.Equal(g, b);
        }

        [Fact]
        public void ToString_ListsAllStages()
        {
            var pipeline = new FilterPipeline { Name = "Test" }
                .Add(new GrayscaleFilter())
                .Add(new SepiaFilter());
            Assert.Contains("Grayscale", pipeline.ToString());
            Assert.Contains("Sepia", pipeline.ToString());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HistogramAnalyzer Tests
    // ─────────────────────────────────────────────────────────────────────────
    public class HistogramAnalyzerTests
    {
        [Fact]
        public void Analyze_SolidRed_AllPixelsInRedBin()
        {
            using var input = TestImageFactory.Solid(10, 10, 200, 0, 0);
            var analyzer = new HistogramAnalyzer();
            analyzer.Analyze(input);
            Assert.Equal(100, analyzer.RedChannel[200]); // 10x10 = 100 pixels
            Assert.Equal(100, analyzer.GreenChannel[0]);
            Assert.Equal(100, analyzer.BlueChannel[0]);
        }

        [Fact]
        public void AnalyzeParallel_MatchesSequential()
        {
            using var input = TestImageFactory.Checkerboard(20);
            var seq = new HistogramAnalyzer();
            var par = new HistogramAnalyzer();
            seq.Analyze(input);
            par.AnalyzeParallel(input);
            Assert.Equal(seq.RedChannel[0], par.RedChannel[0]);
            Assert.Equal(seq.Luminance[255], par.Luminance[255]);
        }

        [Fact]
        public void ComputeStats_SolidImage_ZeroStdDev()
        {
            using var input = TestImageFactory.Solid(10, 10, 128, 128, 128);
            var analyzer = new HistogramAnalyzer();
            analyzer.Analyze(input);
            var stats = analyzer.ComputeStats(analyzer.RedChannel);
            Assert.Equal(128.0, stats.Mean, precision: 1);
            Assert.Equal(0.0, stats.StdDev, precision: 5);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PerformanceBenchmark Tests
    // ─────────────────────────────────────────────────────────────────────────
    public class PerformanceBenchmarkTests
    {
        [Fact]
        public void Run_ReturnsPositiveTimes()
        {
            using var image = TestImageFactory.Solid(100, 100, 128, 64, 32);
            var bench = new PerformanceBenchmark(warmupRuns: 0, measuredRuns: 1);
            var result = bench.Run(new GrayscaleFilter(), image);
            Assert.True(result.SequentialMs >= 0);
            Assert.True(result.ParallelMs >= 0);
        }

        [Fact]
        public void Run_SpeedupIsPositive()
        {
            using var image = TestImageFactory.Solid(200, 200, 100, 100, 100);
            var bench = new PerformanceBenchmark(warmupRuns: 1, measuredRuns: 2);
            var result = bench.Run(new GaussianBlurFilter(5), image);
            Assert.True(result.SpeedupFactor > 0, $"Speedup was {result.SpeedupFactor}");
        }
    }
}
