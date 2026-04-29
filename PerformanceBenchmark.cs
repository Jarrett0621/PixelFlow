using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace PixelFlow.Core
{

    /// Measures sequential vs. parallel processing time and reports speedup.
    /// This is the lightweight in-app benchmark. For micro-benchmarks with
    /// statistical rigor, use BenchmarkDotNet in the PixelFlow.Benchmarks project.

    public sealed class PerformanceBenchmark
    {
        private readonly int _warmupRuns;
        private readonly int _measuredRuns;

        public PerformanceBenchmark(int warmupRuns = 1, int measuredRuns = 3)
        {
            _warmupRuns = warmupRuns;
            _measuredRuns = measuredRuns;
        }


        /// Runs a filter both sequentially and in parallel over the given image,
        /// returning a BenchmarkResult with timing statistics.

        public BenchmarkResult Run(IFilter filter, ImageData image)
        {
            // Warmup — ensures JIT compilation doesn't skew first run
            for (int i = 0; i < _warmupRuns; i++)
            {
                using var _ = filter.Apply(image);
                using var __ = filter.ApplyParallel(image);
            }

            // Sequential measurement
            var seqTimes = new List<long>(_measuredRuns);
            for (int i = 0; i < _measuredRuns; i++)
            {
                var sw = Stopwatch.StartNew();
                using var result = filter.Apply(image);
                sw.Stop();
                seqTimes.Add(sw.ElapsedMilliseconds);
            }

            // Parallel measurement
            var parTimes = new List<long>(_measuredRuns);
            for (int i = 0; i < _measuredRuns; i++)
            {
                var sw = Stopwatch.StartNew();
                using var result = filter.ApplyParallel(image);
                sw.Stop();
                parTimes.Add(sw.ElapsedMilliseconds);
            }

            long seqMedian = Median(seqTimes);
            long parMedian = Median(parTimes);
            double speedup = parMedian > 0 ? (double)seqMedian / parMedian : 0;

            return new BenchmarkResult(
                FilterName: filter.Name,
                ImageWidth: image.Width,
                ImageHeight: image.Height,
                Threads: Environment.ProcessorCount,
                SequentialMs: seqMedian,
                ParallelMs: parMedian,
                SpeedupFactor: speedup);
        }

        /// <summary>
        /// Runs all registered filters and returns a list of results.
        /// </summary>
        public List<BenchmarkResult> RunAll(IEnumerable<IFilter> filters, ImageData image)
        {
            var results = new List<BenchmarkResult>();
            foreach (var filter in filters)
                results.Add(Run(filter, image));
            return results;
        }

        private static long Median(List<long> values)
        {
            values.Sort();
            return values[values.Count / 2];
        }
    }

    public sealed record BenchmarkResult(
        string FilterName,
        int ImageWidth,
        int ImageHeight,
        int Threads,
        long SequentialMs,
        long ParallelMs,
        double SpeedupFactor)
    {
        public int TotalPixels => ImageWidth * ImageHeight;
        public string Summary =>
            $"{FilterName,-30} | {ImageWidth}x{ImageHeight} | " +
            $"Seq: {SequentialMs,5}ms | Par: {ParallelMs,5}ms | " +
            $"Speedup: {SpeedupFactor:F2}x | Threads: {Threads}";
    }
}
