using System.Buffers;
using System.Drawing;
using Microsoft.Extensions.Logging;

namespace binview.cli.Processor
{
    public class BinaryImageProcessor
    {
        private readonly ILogger<BinaryImageProcessor> logger;
        private readonly FileInfo inputFile;
        private readonly FileInfo outputFile;

        private bool completed;
        private bool busy;

        public BinaryImageProcessor(
            ILogger<BinaryImageProcessor> logger,
            string inputPath,
            string outputPath)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            this.inputFile = BinaryImageProcessor.GetFileInfo(inputPath, true, nameof(inputPath));
            this.outputFile = BinaryImageProcessor.GetFileInfo(outputPath, null, nameof(outputPath));
        }

        public async Task ProcessAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // 3 bytes per pixel
            const int bytesPerPixel = 3;
            if (this.busy)
            {
                throw new InvalidOperationException($"Processing is already in progress. Create another instance of '{this.GetType().Name}' to process another file.");
            }
            else if (this.completed)
            {
                throw new InvalidOperationException($"Processing has already finished. Create a new instance of '{this.GetType().Name}' to process another file.");
            }

            var bufferPool = ArrayPool<byte>.Create();
            var widthHeightDbl = Math.Round(Math.Sqrt(inputFile.Length / bytesPerPixel), 0, MidpointRounding.ToPositiveInfinity);
            if (widthHeightDbl > int.MaxValue)
            {
                throw new NotSupportedException("Input file is too large");
            }

            var widthHeight = (int)widthHeightDbl;
            this.logger.LogDebug("Opening input file at: '{InputFilePath}'", this.inputFile.FullName);
            this.logger.LogTrace("File length is {FileLength}, bytes per pixel is {BytesPerPixel}, using Using width/height of {WidthAndHeight}", this.inputFile.Length, bytesPerPixel, widthHeight);

            var buffer = default(byte[]?);
            try
            {
                using (var inputStream = this.inputFile.OpenRead())
                using (var bitmap = new Bitmap(widthHeight, widthHeight))
                {
                    var x = 0;
                    var y = 0;
                    var position = 0;
                    buffer = bufferPool.Rent(bytesPerPixel);
                    var memoryBuffer = buffer.AsMemory();
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        while (position <= inputStream.Length)
                        {
                            var readCount = await inputStream.ReadAsync(buffer, 0, bytesPerPixel, cancellationToken);
                            var colour = Color.FromArgb(255, buffer[0], buffer[1], buffer[2]);
                            this.logger.LogTrace("Colour of pixel at {X}x{Y} is {Colour}", x, y, colour);
                            graphics.DrawRectangle(new Pen(colour), x, y, 1, 1);

                            if (x == widthHeight)
                            {
                                x = 0;
                                y++;
                            }
                            else
                            {
                                x++;
                            }

                            position += bytesPerPixel;
                        }
                    }

                    this.logger.LogDebug("Writing output file to: '{OutputFilePath}'", this.outputFile.FullName);
                    bitmap.Save(this.outputFile.FullName);
                }
            }
            finally
            {
                if (buffer != null)
                {
                    bufferPool.Return(buffer);
                }

                this.completed = true;
            }
        }

        private static FileInfo GetFileInfo(string? path, bool? shouldExist, string paramName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Must be a valid file path", paramName);
            }
            if (shouldExist != null && File.Exists(path) != shouldExist!.Value)
            {
                throw new ArgumentException(shouldExist!.Value ? "File must already exist" : "File must not already exist", paramName);
            }

            return new FileInfo(path);
        }
    }
}
