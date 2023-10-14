namespace binview.cli.Processor
{
    using System.Buffers;
    using System.Diagnostics;
    using System.Drawing;
    using binview.cli.Extensions;
    using Microsoft.Extensions.Logging;
    using Nito.AsyncEx;

    public class ParallelBinaryImageProcessor : IBinaryImageProcessor, IAsyncDisposable
    {
        private const int bytesPerPixel = Constants.Processing.BytesPerPixel;
        private const int disposeTimeoutMs = 30000;

        private readonly ILogger<ParallelBinaryImageProcessor> logger;
        private readonly ParallelOptions parallelOptions;
        private readonly ArrayPool<byte> bufferPool;
        private readonly SemaphoreSlim @lock;
        private readonly FileInfo inputFile;
        private readonly FileInfo outputFile;
        private readonly int widthHeight;

        private CancellationTokenSource? cts;
        private bool completed;
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

            this.inputFile = ParallelBinaryImageProcessor.GetFileInfo(inputPath, true, nameof(inputPath));
            this.outputFile = ParallelBinaryImageProcessor.GetFileInfo(outputPath, null, nameof(outputPath));

            var widthHeightDbl = Math.Round(Math.Sqrt(this.inputFile.Length / ParallelBinaryImageProcessor.bytesPerPixel), 0, MidpointRounding.ToPositiveInfinity);
            if (widthHeightDbl > int.MaxValue)
            {
                throw new NotSupportedException("Input file is too large");
            }

            this.widthHeight = (int)widthHeightDbl;

            this.@lock = new SemaphoreSlim(1, 1);
            this.bufferPool = ArrayPool<byte>.Shared;

            this.logger.LogTrace("File length is {FileLength}, bytes per pixel is {BytesPerPixel}, using width/height of {WidthAndHeight}", this.inputFile.Length, ParallelBinaryImageProcessor.bytesPerPixel, this.widthHeight);
        }

        public async ValueTask DisposeAsync()
        {
            if (!this.disposed)
            {
                this.cts?.Cancel();
                using (var disposeCts = new CancellationTokenSource(ParallelBinaryImageProcessor.disposeTimeoutMs))
                using (await this.@lock.LockAsync(disposeCts.Token))
                {
                    this.completed = true;
                    this.cts?.Dispose();
                }

                this.@lock.Dispose();
                this.disposed = true;
            }
        }

        public async Task ProcessAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.ThrowIfDisposed();

            if (this.@lock.CurrentCount == 0)
            {
                throw new InvalidOperationException($"Processing is already in progress. Create another instance of '{this.GetType().Name}' to process another file.");
            }
            else if (this.completed)
            {
                throw new InvalidOperationException($"Processing has already finished. Create a new instance of '{this.GetType().Name}' to process another file.");
            }

            var acquiredLock = default(IDisposable?);
            try
            {
                // Prevent multiple threads waiting on acquiring the lock
                // There's probably a better way of doing this?
                using (var lockCts = new CancellationTokenSource(50))
                using (var linkedLockCts = CancellationTokenSource.CreateLinkedTokenSource(lockCts.Token, cancellationToken))
                {
                    acquiredLock = await this.@lock.LockAsync(linkedLockCts.Token);
                }

                if (!this.completed)
                {
                    this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    await this.DoProcessFileAsync(this.cts.Token);
                    this.completed = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                acquiredLock?.Dispose();
            }
        }

        private async Task DoProcessFileAsync(CancellationToken cancellationToken)
        {
            this.logger.LogDebug("Opening input file at: '{InputFilePath}'", this.inputFile.FullName);
            var buffer = default(byte[]?);
            using (var inputStream = this.inputFile.OpenRead())
            using (var bitmap = new Bitmap(this.widthHeight, this.widthHeight))
            {
                var x = 0;
                var y = 0;
                var position = 0;
                var sw = Stopwatch.StartNew();
                buffer = this.bufferPool.Rent(ParallelBinaryImageProcessor.bytesPerPixel);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    var rowSw = Stopwatch.StartNew();
                    while (position <= inputStream.Length &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        var readCount = await inputStream.ReadAsync(buffer, 0, ParallelBinaryImageProcessor.bytesPerPixel, cancellationToken);
                        graphics.DrawPixel(x, y, buffer[0], buffer[1], buffer[2]);

                        if (x == this.widthHeight)
                        {
                            rowSw.Stop();
                            this.logger.LogTrace("Processing row {RowNumber} took {ElapsedMilliseconds} milliseconds ({ElapsedTicks} ticks)", y, rowSw.ElapsedMilliseconds, rowSw.ElapsedTicks);
                            x = 0;
                            y++;
                            rowSw.Restart();
                        }
                        else
                        {
                            x++;
                        }

                        position += ParallelBinaryImageProcessor.bytesPerPixel;
                    }

                    rowSw.Stop();
                    this.logger.LogTrace("Processing row {RowNumber} took {ElapsedMilliseconds} milliseconds ({ElapsedTicks} ticks)", y, rowSw.ElapsedMilliseconds, rowSw.ElapsedTicks);
                }

                sw.Stop();
                this.logger.LogTrace("Processing took {ElapsedMilliseconds} milliseconds total ({ElapsedTicks} ticks)", sw.ElapsedMilliseconds, sw.ElapsedTicks);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                this.logger.LogDebug("Writing output file to: '{OutputFilePath}'", this.outputFile.FullName);
                bitmap.Save(this.outputFile.FullName);
            }
        }

        private Color[] StartRowWorker(byte[] bytes)
        {
            var result = new Color[bytes.Length / 3];
            var resultIndex = 0;
            for (var i = 0; i < bytes.Length; i += 3)
            {
                result[resultIndex] = Color.FromArgb(255, bytes[i], bytes.ElementAtOrDefault(i + 1), bytes.ElementAtOrDefault(i + 2));
            }

            return result;
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
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
