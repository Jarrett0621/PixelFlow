using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PixelFlow.Core
{
    /// <summary>
    /// Composable pipeline for applying a sequence of filters.
    /// Supports sequential and parallel execution of each stage.
    /// </summary>
    public sealed class FilterPipeline
    {
        private readonly List<IFilter> _stages = new();

        public string Name { get; set; } = "Pipeline";

        public FilterPipeline Add(IFilter filter)
        {
            _stages.Add(filter);
            return this; // fluent
        }

        public IReadOnlyList<IFilter> Stages => _stages.AsReadOnly();

        /// <summary>
        /// Runs each filter sequentially (single-threaded per stage).
        /// Good baseline for benchmarking.
        /// </summary>
        public ImageData Run(ImageData input)
        {
            var current = input.Clone();
            foreach (var filter in _stages)
            {
                var next = filter.Apply(current);
                current.Dispose();
                current = next;
            }
            return current;
        }

        /// <summary>
        /// Runs each stage with TPL-parallel pixel processing.
        /// Stages are still sequential — parallelism is within each filter.
        /// </summary>
        public ImageData RunParallel(ImageData input, CancellationToken ct = default)
        {
            var current = input.Clone();
            foreach (var filter in _stages)
            {
                ct.ThrowIfCancellationRequested();
                var next = filter.ApplyParallel(current, ct);
                current.Dispose();
                current = next;
            }
            return current;
        }

        public override string ToString()
            => $"{Name}: [{string.Join(" → ", _stages.Select(f => f.Name))}]";
    }
}
