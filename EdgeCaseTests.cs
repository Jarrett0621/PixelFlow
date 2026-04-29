using System;
using PixelFlow.Core;
using PixelFlow.Core.Filters;

namespace PixelFlow
{
    public static class EdgeCaseTests
    {
        private static int _passed = 0;
        private static int _failed = 0;

        public static void RunAll()
        {
            Console.WriteLine("🧪 Edge Case Tests");
            Console.WriteLine(new string('─', 60));

            Test_1x1_Image();
            Test_AllBlack_Image();
            Test_AllWhite_Image();
            Test_SingleRow_Image();
            Test_SingleColumn_Image();
            Test_LargeImage();
            Test_InvertTwiceIsIdentity();
            Test_BrightnessClampDoesNotOverflow();
            Test_GrayscaleIsNeutralOnGrayImage();
            Test_PipelineEmptyReturnsClone();

            Console.WriteLine(new string('─', 60));
            Console.WriteLine($"  Results: {_passed} passed, {_failed} failed");
            Console.WriteLine();
        }

        private static void Assert(string testName, bool condition)
        {
            if (condition)
            {
                Console.WriteLine($"  ✓ {testName}");
                _passed++;
            }
            else
            {
                Console.WriteLine($"  ✗ FAILED: {testName}");
                _failed++;
            }
        }

        private static void Test_1x1_Image()
        {
            var img = new ImageData(1, 1);
            img.SetPixel(0, 0, 128, 64, 32);
            img.CommitBuffer();

            using var result = new GrayscaleFilter().Apply(img);
            var (b, g, r, a) = result.GetPixel(0, 0);
            Assert("1x1 image — Grayscale does not crash", r == g && g == b);
            img.Dispose();
        }

        private static void Test_AllBlack_Image()
        {
            var img = new ImageData(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    img.SetPixel(x, y, 0, 0, 0);
            img.CommitBuffer();

            using var result = new BrightnessFilter(50).Apply(img);
            var (b, g, r, a) = result.GetPixel(32, 32);
            Assert("All-black image — Brightness raises values above 0", r > 0);
            img.Dispose();
        }

        private static void Test_AllWhite_Image()
        {
            var img = new ImageData(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    img.SetPixel(x, y, 255, 255, 255);
            img.CommitBuffer();

            using var result = new BrightnessFilter(50).Apply(img);
            var (b, g, r, a) = result.GetPixel(32, 32);
            Assert("All-white image — Brightness clamps at 255, no overflow", r == 255);
            img.Dispose();
        }

        private static void Test_SingleRow_Image()
        {
            var img = new ImageData(512, 1);
            for (int x = 0; x < 512; x++)
                img.SetPixel(x, 0, 100, 150, 200);
            img.CommitBuffer();

            using var result = new GrayscaleFilter().Apply(img);
            Assert("Single-row image — Grayscale does not crash", result.Height == 1);
            img.Dispose();
        }

        private static void Test_SingleColumn_Image()
        {
            var img = new ImageData(1, 512);
            for (int y = 0; y < 512; y++)
                img.SetPixel(0, y, 100, 150, 200);
            img.CommitBuffer();

            using var result = new SepiaFilter().Apply(img);
            Assert("Single-column image — Sepia does not crash", result.Width == 1);
            img.Dispose();
        }

        private static void Test_LargeImage()
        {
            try
            {
                var img = new ImageData(2048, 2048);
                for (int y = 0; y < 2048; y++)
                    for (int x = 0; x < 2048; x++)
                        img.SetPixel(x, y, 128, 128, 128);
                img.CommitBuffer();

                using var result = new GrayscaleFilter().ApplyParallel(img);
                Assert("2048x2048 large image — parallel Grayscale completes", result.Width == 2048);
                img.Dispose();
            }
            catch (Exception ex)
            {
                Assert($"2048x2048 large image — crashed: {ex.Message}", false);
            }
        }

        private static void Test_InvertTwiceIsIdentity()
        {
            var img = new ImageData(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    img.SetPixel(x, y, 100, 150, 200);
            img.CommitBuffer();

            var filter = new InvertFilter();
            using var once  = filter.Apply(img);
            using var twice = filter.Apply(once);
            var (_, _, r, _) = twice.GetPixel(32, 32);
            Assert("Invert twice returns original pixel values", r == 100);
            img.Dispose();
        }

        private static void Test_BrightnessClampDoesNotOverflow()
        {
            var img = new ImageData(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    img.SetPixel(x, y, 255, 255, 255);
            img.CommitBuffer();

            using var result = new BrightnessFilter(100).Apply(img);
            var (b, g, r, a) = result.GetPixel(0, 0);
            Assert("Brightness(+100) on white — no byte overflow (stays 255)", r == 255 && g == 255 && b == 255);
            img.Dispose();
        }

        private static void Test_GrayscaleIsNeutralOnGrayImage()
        {
            var img = new ImageData(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    img.SetPixel(x, y, 128, 128, 128);
            img.CommitBuffer();

            using var result = new GrayscaleFilter().Apply(img);
            var (b, g, r, a) = result.GetPixel(32, 32);
            Assert("Grayscale on already-gray image — R=G=B", r == g && g == b);
            img.Dispose();
        }

        private static void Test_PipelineEmptyReturnsClone()
        {
            var img = new ImageData(64, 64);
            img.SetPixel(10, 10, 99, 88, 77);
            img.CommitBuffer();

            var pipeline = new FilterPipeline();
            using var result = pipeline.Run(img);
            var (_, _, r, _) = result.GetPixel(10, 10);
            Assert("Empty pipeline returns clone of original image", r == 99);
            img.Dispose();
        }
    }
}
