namespace binview.cli.Processor
{
    using System;
    using System.Buffers;
    using IronSoftware.Drawing;
    using Microsoft.Extensions.Logging;
    using Nito.AsyncEx;
    using Colour = IronSoftware.Drawing.Color;

    public class ParallelBinaryImageProcessor : IBinaryImageProcessor, IAsyncDisposable
    {
        private const int bytesPerPixel = Constants.Processing.BytesPerPixel;
        private const int disposeTimeoutMs = 30000;

        private readonly ILogger<ParallelBinaryImageProcessor> logger;
        private readonly ParallelOptions parallelOptions;
        private readonly ArrayPool<(bool Set, byte R, byte G, byte B)[]> jaggedDataBufferPool;
        private readonly ArrayPool<(bool Set, byte R, byte G, byte B)> dataBufferPool;
        private readonly ArrayPool<Colour[]> jaggedImageBufferPool;
        private readonly ArrayPool<Colour> imageBufferPool;
        private readonly AsyncManualResetEvent usedMre;
        private readonly SemaphoreSlim @lock;
        private readonly FileInfo inputFile;
        private readonly FileInfo outputFile;
        private readonly int imageWidth;
        private readonly int imageHeight;
        private readonly Colour backgroundColour;

        private CancellationTokenSource? cts;
        private bool disposed;

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

        public int MaxConcurrency
        {
            get => this.parallelOptions.MaxDegreeOfParallelism;
        }

        public ParallelBinaryImageProcessor(
            ILogger<ParallelBinaryImageProcessor> logger,
            ParallelOptions parallelOptions,
            string inputPath,
            string outputPath)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.parallelOptions = parallelOptions;
            this.backgroundColour = Constants.Processing.DefaultBackgroundColour;

            this.inputFile = GetFileInfo(inputPath, true, nameof(inputPath));
            this.outputFile = GetFileInfo(outputPath, null, nameof(outputPath));

            var widthHeightDbl = Math.Round(Math.Sqrt(this.inputFile.Length / bytesPerPixel), 0, MidpointRounding.ToPositiveInfinity);
            if (widthHeightDbl > int.MaxValue)
            {
                throw new NotSupportedException("Input file is too large");
            }

            this.imageWidth = (int)widthHeightDbl;
            this.imageHeight = (int)widthHeightDbl;

            this.@lock = new SemaphoreSlim(1, 1);
            this.usedMre = new AsyncManualResetEvent(true);
            this.dataBufferPool = ArrayPool<(bool Set, byte R, byte G, byte B)>.Shared;
            this.jaggedDataBufferPool = ArrayPool<(bool Set, byte R, byte G, byte B)[]>.Shared;
            this.imageBufferPool = ArrayPool<Colour>.Shared;
            this.jaggedImageBufferPool = ArrayPool<Colour[]>.Shared;

            this.logger.LogTrace("File length is {FileLength}, bytes per pixel is {BytesPerPixel}, using width/height of {WidthAndHeight}", this.inputFile.Length, bytesPerPixel, this.imageWidth);
        }

        public async ValueTask DisposeAsync()
        {
            if (!this.disposed)
            {
                this.cts?.Cancel();
                using (var disposeCts = new CancellationTokenSource(disposeTimeoutMs))
                using (await this.@lock.LockAsync(disposeCts.Token))
                {
                    this.cts?.Dispose();
                }

                this.@lock.Dispose();
                this.disposed = true;
            }
        }

        public async Task ProcessAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.ThrowIfDisposed();

            var acquiredLock = default(IDisposable?);
            try
            {
                // Prevent multiple threads waiting on acquiring the lock
                // There's probably a better way of doing this?
                acquiredLock = await this.GetLockAsync(cancellationToken);

                this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                this.parallelOptions.CancellationToken = this.cts.Token;

                await this.DoProcessFileAsync(this.cts.Token);
            }
            finally
            {
                acquiredLock?.Dispose();
            }
        }

        private async Task DoProcessFileAsync(CancellationToken cancellationToken)
        {
            this.logger.LogDebug("Opening input file at: '{InputFilePath}'", this.inputFile.FullName);
            var rawRowData = this.jaggedDataBufferPool.Rent(this.imageHeight + 1);
            var imageData = default(Colour[][]?);
            var maxWidthIndex = this.imageWidth - 1;
            var position = 0;
            var pixelCount = 0;
            var x = 0;
            var y = 0;

            try
            {
                using (var inputStream = this.inputFile.OpenRead())
                {
                    var buffer = new byte[bytesPerPixel];
                    this.logger.LogDebug("Reading {ByteCount} bytes of data...", inputStream.Length);
                    while (position < inputStream.Length &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        if (x == 0)
                        {
                            rawRowData[y] = this.dataBufferPool.Rent(this.imageWidth + 1);
                        }

                        var readCount = await inputStream.ReadAsync(buffer, 0, bytesPerPixel, cancellationToken);
                        rawRowData[y][x] = (true, buffer[0], buffer[1], buffer[2]);
                        pixelCount++;

                        if (x == maxWidthIndex)
                        {
                            x = 0;
                            y++;
                        }
                        else
                        {
                            x++;
                        }

                        position += readCount;
                    }
                }

                this.logger.LogDebug("Processing {ByteCount} of data into {PixelCount} pixel image...", position, pixelCount);
                imageData = this.jaggedImageBufferPool.Rent(rawRowData.Length);
                Parallel.For(0, this.imageHeight - 1, this.parallelOptions, rowIndex =>
                {
                    var thisRowData = rawRowData[rowIndex];
                    imageData[rowIndex] = this.imageBufferPool.Rent(this.imageWidth);

                    for (var pixelIndex = 0; pixelIndex < this.imageWidth; pixelIndex++)
                    {
                        var thisPixelData = thisRowData[pixelIndex];
                        var thisPixel = this.backgroundColour;
                        if (thisPixelData.Set)
                        {
                            thisPixel = Colour.FromArgb(255, thisPixelData.R, thisPixelData.G, thisPixelData.B);
                        }

                        imageData[rowIndex][pixelIndex] = thisPixel;
                    }
                });

                this.logger.LogDebug("Building image...");
                using (var image = new AnyBitmap(this.imageWidth, this.imageHeight))
                {
                    Parallel.For(0, this.imageHeight - 1, rowIndex =>
                    {
                        for (var pixelIndex = 0; pixelIndex < this.imageWidth; pixelIndex++)
                        {
                            image.SetPixel(pixelIndex, rowIndex, imageData[rowIndex][pixelIndex]);
                        }
                    });

                    image.SaveAs(this.outputFile.FullName);
                }
            }
            finally
            {
                this.logger.LogDebug("Cleaning up input data...");
                Parallel.ForEach(rawRowData, row =>
                {
                    if (row != null)
                    {
                        this.dataBufferPool.Return(row);
                    }
                });

                this.jaggedDataBufferPool.Return(rawRowData);

                if (imageData != null)
                {
                    this.logger.LogDebug("Cleaning up image data...");
                    Parallel.ForEach(imageData, row =>
                    {
                        if (row != null)
                        {
                            this.imageBufferPool.Return(row);
                        }
                    });

                    this.jaggedImageBufferPool.Return(imageData);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }

        private async Task<IDisposable> GetLockAsync(CancellationToken cancellationToken)
        {
            using (var lockCts = new CancellationTokenSource(60))
            using (var innerCts = CancellationTokenSource.CreateLinkedTokenSource(lockCts.Token, cancellationToken))
            {
                try
                {
                    var @lock = await this.@lock.LockAsync(lockCts.Token);
                    await this.usedMre.WaitAsync(innerCts.Token);
                    this.usedMre.Reset();
                    return @lock;
                }
                catch (OperationCanceledException ex)
                {
                    throw new InvalidOperationException($"Processing is in progress or has already finished. Create a new instance of '{this.GetType().Name}' to process another file.");
                }
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
