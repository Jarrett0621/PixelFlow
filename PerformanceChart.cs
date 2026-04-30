using System;
using System.Collections.Generic;
using PixelFlow.Core;

namespace PixelFlow
{
    public static class PerformanceChart
    {
        public static void PrintSpeedupChart(List<BenchmarkResult> results)
        {
            Console.WriteLine("📈 Parallel Speedup Chart (Sequential vs Parallel)");
            Console.WriteLine(new string('─', 60));

            double maxSpeedup = 0;
            foreach (var r in results)
                if (r.SpeedupFactor > maxSpeedup)
                    maxSpeedup = r.SpeedupFactor;

            int barWidth = 30;

            foreach (var r in results)
            {
                int filled = (int)(r.SpeedupFactor / maxSpeedup * barWidth);
                int empty  = barWidth - filled;
                string bar = new string('█', filled) + new string('░', empty);
                Console.WriteLine($"  {r.FilterName,-22} |{bar}| {r.SpeedupFactor:F2}x");
            }

            Console.WriteLine(new string('─', 60));
            Console.WriteLine();
            Console.WriteLine("📊 Execution Time Chart (ms) — Sequential vs Parallel");
            Console.WriteLine(new string('─', 60));

            long maxMs = 0;
            foreach (var r in results)
                if (r.SequentialMs > maxMs) maxMs = r.SequentialMs;

            foreach (var r in results)
            {
                int seqBar = (int)((double)r.SequentialMs / maxMs * barWidth);
                int parBar = (int)((double)r.ParallelMs   / maxMs * barWidth);
                Console.WriteLine($"  {r.FilterName,-22}");
                Console.WriteLine($"    Seq {new string('█', seqBar),-32} {r.SequentialMs}ms");
                Console.WriteLine($"    Par {new string('▓', parBar),-32} {r.ParallelMs}ms");
            }

            Console.WriteLine(new string('─', 60));
            Console.WriteLine();
        }
    }
}
