using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PixelFlow.Core
{

    /// Processes multiple images through a filter pipeline concurrently.
    /// Uses Parallel.ForEachAsync (.NET 6+) for async-friendly parallelism.
    /// Progress is reported via IProgress<BatchProgress>.
    public sealed class BatchProcessor
    {
        public event EventHandler<BatchProgress>? ProgressChanged;

        /// Processes a list of input files and writes outputs to outputDir.
        /// Parallel at the file level — each image's pipeline also runs parallel.

        public async Task ProcessAsync(
            IEnumerable<string> inputFiles,
            string outputDir,
            FilterPipeline pipeline,
            int maxConcurrency = 4,
            CancellationToken ct = default)
        {
            Directory.CreateDirectory(outputDir);

            var files = new List<string>(inputFiles);
            int total = files.Count;
            int completed = 0;
            int failed = 0;

            var opts = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                CancellationToken = ct
            };

            await Parallel.ForEachAsync(files, opts, async (filePath, token) =>
            {
                string fileName = Path.GetFileName(filePath);
                try
                {
                    // Image I/O on thread pool (sync APIs but we yield between files)
                    await Task.Run(() =>
                    {
                        using var image = ImageData.FromFile(filePath);
                        using var result = pipeline.RunParallel(image, token);
                        string outPath = Path.Combine(outputDir, fileName);
                        result.SaveToFile(outPath);
                    }, token);

                    int done = Interlocked.Increment(ref completed);
                    RaiseProgress(done, total, Interlocked.CompareExchange(ref failed, 0, 0), fileName, null);
                }
                catch (OperationCanceledException)
                {
                    throw; // let Parallel.ForEachAsync propagate
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    int done = Interlocked.Increment(ref completed);
                    RaiseProgress(done, total, Interlocked.CompareExchange(ref failed, 0, 0), fileName, ex.Message);
                }
            });
        }

        private void RaiseProgress(int completed, int total, int failed, string fileName, string? error)
        {
            ProgressChanged?.Invoke(this, new BatchProgress(completed, total, failed, fileName, error));
        }
    }

    public sealed record BatchProgress(
        int Completed,
        int Total,
        int Failed,
        string CurrentFile,
        string? Error)
    {
        public double Percentage => Total > 0 ? (double)Completed / Total * 100 : 0;
        public bool IsComplete => Completed >= Total;
    }
}
