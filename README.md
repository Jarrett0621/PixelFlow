# PixelFlow — Parallel Image Processing Tool

**Project 9 | Team PixelFlow | .NET 8.0 / C#**

> High-performance image processing using the Task Parallel Library (TPL).  
> Every filter runs both sequentially and in parallel — you can benchmark the difference.

---

## Team Responsibilities

| Member | Responsibility | Files |
|--------|---------------|-------|
| **Taylor** | 8 Image Filters | `src/Filters.cs`, `src/IFilter.cs` |
| **Karma** | TPL / Parallel Processing | `src/FilterPipeline.cs`, `src/BatchProcessor.cs`, `src/Program.cs` |
| **Jarrett** | Analysis & Testing | `src/HistogramAnalyzer.cs`, `src/PerformanceBenchmark.cs`, `tests/FilterTests.cs`, `src/Benchmarks.cs` |

---

## Architecture

```
PixelFlow/
├── src/
│   ├── ImageData.cs           # Fast pixel buffer (direct byte[] access, no per-pixel GetPixel)
│   ├── IFilter.cs             # IFilter interface + FilterBase abstract class
│   ├── Filters.cs             # All 8 filters (Grayscale, Sepia, Brightness, Contrast, 
│   │                          #   Invert, Sharpen, GaussianBlur, EdgeDetection)
│   ├── FilterPipeline.cs      # Composable filter chains (fluent API)
│   ├── BatchProcessor.cs      # Multi-image parallel processing with progress reporting
│   ├── HistogramAnalyzer.cs   # Per-channel histogram with thread-local accumulation
│   ├── PerformanceBenchmark.cs # Stopwatch-based seq vs. parallel comparison
│   ├── Benchmarks.cs          # BenchmarkDotNet micro-benchmarks
│   └── Program.cs             # Console demo entry point
└── tests/
    └── FilterTests.cs         # xUnit tests for all components
```

---

## The 8 Filters

| # | Filter | Algorithm | Parallel Strategy |
|---|--------|-----------|-------------------|
| 1 | **Grayscale** | ITU-R BT.601 luminance | `Parallel.For` over rows |
| 2 | **Sepia** | 3×3 color matrix multiply | `Parallel.For` over rows |
| 3 | **Brightness** | Per-channel offset | `Parallel.For` over rows |
| 4 | **Contrast** | Scale from midpoint (128) | `Parallel.For` over rows |
| 5 | **Invert** | `255 - channel` | `Parallel.For` over rows |
| 6 | **Sharpen** | 3×3 convolution kernel | `Parallel.For` over rows (read-only input) |
| 7 | **Gaussian Blur** | Separable 2-pass Gaussian | `Parallel.For` per pass |
| 8 | **Edge Detection** | Sobel Gx/Gy gradient | Grayscale → `Parallel.For` rows |

---

## Key Design Decisions

### Why Parallel.For over rows?
Row parallelism is safe because pixel writes within a row never depend on other rows.  
For simple per-pixel filters (grayscale, sepia, etc.) this gives near-linear speedup up to CPU core count.

### Why thread-local accumulation in HistogramAnalyzer?
If every thread wrote to shared `int[]` arrays, every increment would require a lock or Interlocked — creating heavy contention at 256 bins. Thread-local arrays let each thread accumulate independently, then merge once. Zero contention during parallel phase.

### Why separable Gaussian blur?
A naïve 2D Gaussian with radius `r` is O(n·(2r+1)²). Separability lets us do horizontal pass then vertical pass: O(n·(2r+1)) each. For r=5 that's a 5.5× reduction in work.

### Memory optimization
- `ImageData` uses a single `byte[]` buffer instead of per-pixel allocation.
- Stride-based offset calculation: `offset = y * Stride + x * 4`.
- `CommitBuffer()` is called once after a loop, not per-pixel.

---

## Setup & Running

### Prerequisites
- Visual Studio 2022 or .NET 8 SDK
- Windows (System.Drawing.Common uses GDI+ on Windows)

### Run the demo
```bash
cd PixelFlow
dotnet run --project PixelFlow.csproj           # synthetic test image
dotnet run --project PixelFlow.csproj -- my.png # your own image
```

### Run tests
```bash
dotnet test tests/PixelFlow.Tests.csproj
```

### Run BenchmarkDotNet benchmarks
```bash
dotnet run -c Release --project PixelFlow.csproj
```

---

## Performance Notes

On a modern 8-core machine, expect:
- Simple filters (grayscale, sepia): **4–6× speedup** parallel vs sequential on 512×512 images
- Convolution filters (blur, sharpen): **6–8× speedup** on large images (kernel work dominates)
- Batch processing: scales with `maxConcurrency` setting (default: 4 concurrent images)

Amdahl's Law applies: the larger the image, the higher the parallel speedup (setup overhead becomes negligible).

---

## References
- [Task Parallel Library (Microsoft)](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [Gaussian Blur (Wikipedia)](https://en.wikipedia.org/wiki/Gaussian_blur)
- [Sobel Operator (Wikipedia)](https://en.wikipedia.org/wiki/Sobel_operator)
- [Digital Image Processing (Wikipedia)](https://en.wikipedia.org/wiki/Digital_image_processing)
