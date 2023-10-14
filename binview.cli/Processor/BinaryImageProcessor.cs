namespace binview.cli.Processor
{
    using System.Buffers;
    using System.Diagnostics;
    using System.Drawing;
    using Microsoft.Extensions.Logging;

    public class BinaryImageProcessor : IBinaryImageProcessor
    {
        private const int bytesPerPixel = Constants.Processing.BytesPerPixel;

        private readonly ILogger<BinaryImageProcessor> logger;
        private readonly FileInfo inputFile;
        private readonly FileInfo outputFile;

        private bool completed;
        private bool busy;

        public string InputPath
        {
            get => this.inputFile.FullName;
        }

        public string OutputPath
        {
            get => this.outputFile.FullName;
        }

        public long InputFileLength
        {
            get => this.inputFile.Length;
        }

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
            if (this.busy)
            {
                throw new InvalidOperationException($"Processing is already in progress. Create another instance of '{this.GetType().Name}' to process another file.");
            }
            else if (this.completed)
            {
                throw new InvalidOperationException($"Processing has already finished. Create a new instance of '{this.GetType().Name}' to process another file.");
            }

            var bufferPool = ArrayPool<byte>.Create();
            var widthHeightDbl = Math.Round(Math.Sqrt(this.inputFile.Length / BinaryImageProcessor.bytesPerPixel), 0, MidpointRounding.ToPositiveInfinity);
            if (widthHeightDbl > int.MaxValue)
            {
                throw new NotSupportedException("Input file is too large");
            }

            var widthHeight = (int)widthHeightDbl;
            this.busy = true;
            this.logger.LogDebug("Opening input file at: '{InputFilePath}'", this.inputFile.FullName);
            this.logger.LogTrace("File length is {FileLength}, bytes per pixel is {BytesPerPixel}, using width/height of {WidthAndHeight}", this.inputFile.Length, BinaryImageProcessor.bytesPerPixel, widthHeight);

            var buffer = default(byte[]?);
            try
            {
                using (var inputStream = this.inputFile.OpenRead())
                using (var bitmap = new Bitmap(widthHeight, widthHeight))
                {
                    var x = 0;
                    var y = 0;
                    var position = 0;
                    var sw = Stopwatch.StartNew();
                    buffer = bufferPool.Rent(BinaryImageProcessor.bytesPerPixel);
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        var rowSw = Stopwatch.StartNew();
                        while (position <= inputStream.Length &&
                            !cancellationToken.IsCancellationRequested)
                        {
                            var readCount = await inputStream.ReadAsync(buffer, 0, BinaryImageProcessor.bytesPerPixel, cancellationToken);
                            var colour = Color.FromArgb(255, buffer[0], buffer[1], buffer[2]);
                            graphics.DrawRectangle(new Pen(colour), x, y, 1, 1);

                            if (x == widthHeight)
                            {
                                rowSw.Stop();
                                this.logger.LogTrace("Processing row {RowNumber} took {ElapsedMilliseconds} milliseconds ({ElapsedTicks} ticks)", rowSw.Elapsed, rowSw.ElapsedTicks);
                                x = 0;
                                y++;
                                rowSw.Restart();
                            }
                            else
                            {
                                x++;
                            }

                            position += BinaryImageProcessor.bytesPerPixel;
                        }
                    }

                    sw.Stop();
                    this.logger.LogTrace("Processing took {ElapsedMilliseconds} milliseconds ({ElapsedTicks} ticks)", sw.ElapsedMilliseconds, sw.ElapsedTicks);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
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
                this.busy = false;
            }
        }

        private static FileInfo GetFileInfo(string? path, bool? shouldExist, string paramName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Must be a valid file path", paramName);
            }

            if (shouldExist.HasValue && File.Exists(path) != shouldExist!.Value)
            {
                throw new ArgumentException(shouldExist!.Value ? "File must already exist" : "File must not already exist", paramName);
            }

            return new FileInfo(path);
        }
    }
}
