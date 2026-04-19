using System;
using System.Threading;
using System.Threading.Tasks;

namespace PixelFlow.Core
{
    /// <summary>
    /// Computes per-channel histograms (R, G, B, Luminance) over an image.
    /// Parallel version uses thread-local arrays to avoid lock contention,
    /// then merges them at the end.
    /// </summary>
    public sealed class HistogramAnalyzer
    {
        public int[] RedChannel   { get; private set; } = new int[256];
        public int[] GreenChannel { get; private set; } = new int[256];
        public int[] BlueChannel  { get; private set; } = new int[256];
        public int[] Luminance    { get; private set; } = new int[256];
        public int TotalPixels    { get; private set; }

        public void Analyze(ImageData image) => AnalyzeCore(image, parallel: false);

        public void AnalyzeParallel(ImageData image, CancellationToken ct = default)
            => AnalyzeCore(image, parallel: true, ct);

        private void AnalyzeCore(ImageData image, bool parallel, CancellationToken ct = default)
        {
            RedChannel   = new int[256];
            GreenChannel = new int[256];
            BlueChannel  = new int[256];
            Luminance    = new int[256];
            TotalPixels  = image.Width * image.Height;

            if (!parallel)
            {
                for (int y = 0; y < image.Height; y++)
                    for (int x = 0; x < image.Width; x++)
                        AccumulatePixel(image, x, y, RedChannel, GreenChannel, BlueChannel, Luminance);
                return;
            }

            // Parallel: each thread has private arrays, merged afterward
            var r = new ThreadLocal<int[]>(() => new int[256], trackAllValues: true);
            var g = new ThreadLocal<int[]>(() => new int[256], trackAllValues: true);
            var b = new ThreadLocal<int[]>(() => new int[256], trackAllValues: true);
            var l = new ThreadLocal<int[]>(() => new int[256], trackAllValues: true);

            Parallel.For(0, image.Height,
                new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
                y =>
                {
                    var lr = r.Value!; var lg = g.Value!;
                    var lb = b.Value!; var ll = l.Value!;
                    for (int x = 0; x < image.Width; x++)
                        AccumulatePixel(image, x, y, lr, lg, lb, ll);
                });

            // Merge thread-local accumulators
            foreach (var arr in r.Values!) for (int i = 0; i < 256; i++) RedChannel[i]   += arr[i];
            foreach (var arr in g.Values!) for (int i = 0; i < 256; i++) GreenChannel[i] += arr[i];
            foreach (var arr in b.Values!) for (int i = 0; i < 256; i++) BlueChannel[i]  += arr[i];
            foreach (var arr in l.Values!) for (int i = 0; i < 256; i++) Luminance[i]    += arr[i];

            r.Dispose(); g.Dispose(); b.Dispose(); l.Dispose();
        }

        private static void AccumulatePixel(ImageData img, int x, int y,
            int[] r, int[] g, int[] b, int[] lum)
        {
            var (bv, gv, rv, _) = img.GetPixel(x, y);
            r[rv]++;
            g[gv]++;
            b[bv]++;
            byte gray = (byte)(0.299 * rv + 0.587 * gv + 0.114 * bv);
            lum[gray]++;
        }

        public HistogramStats ComputeStats(int[] channel)
        {
            double mean = 0, variance = 0;
            int max = 0, min = 255, peak = 0;
            for (int i = 0; i < 256; i++)
            {
                mean += i * channel[i];
                if (channel[i] > peak) peak = i;
                if (channel[i] > 0) { min = Math.Min(min, i); max = Math.Max(max, i); }
            }
            mean /= TotalPixels;
            for (int i = 0; i < 256; i++)
                variance += channel[i] * Math.Pow(i - mean, 2);
            variance /= TotalPixels;

            return new HistogramStats(mean, Math.Sqrt(variance), min, max, peak);
        }
    }

    public sealed record HistogramStats(
        double Mean,
        double StdDev,
        int Min,
        int Max,
        int PeakBin);
}
