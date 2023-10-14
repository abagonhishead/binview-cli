namespace binview.cli
{
    using System.Globalization;
    using System.Text;
    using binview.cli.Constants;
    using binview.cli.Extensions;
    using binview.cli.Processor;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Console;

    public static class BinviewCli
    {
        private const LogLevel defaultLogLevel = LogLevel.Trace;
        private static readonly Version version = new Version(0, 0, 2, 0);
        private static readonly string executableName = AppDomain.CurrentDomain.FriendlyName;

        public static async Task<int> Main(string[] args)
        {
            const int defaultUnsuccessfulReturnCode = 1;
            const int defaultSuccessfulReturnCode = 0;
            var returnCode = defaultUnsuccessfulReturnCode;

            if (args.ContainsKey(CommandLineArgs.Help))
            {
                await BinviewCli.ShowIntroAsync();
                await BinviewCli.ShowHelpAsync();
                returnCode = defaultSuccessfulReturnCode;
            }
            else if (args.ContainsKey(CommandLineArgs.Version))
            {
                await BinviewCli.ShowIntroAsync();
                await BinviewCli.ShowVersionAsync();
                returnCode = defaultSuccessfulReturnCode;
            }
            else
            {
                var configuration = default(IConfigurationRoot?);
                var loggerFactory = default(ILoggerFactory?);

                try
                {
                    configuration = BinviewCli.BuildCommandLineConfig(args);
                    loggerFactory = BinviewCli.BuildLoggerFactory(configuration, args.ContainsKey(CommandLineArgs.LogShowTimestamp));
                    var logger = loggerFactory.CreateLogger(nameof(BinviewCli));

                    using (var cts = new CancellationTokenSource())
                    {
                        Console.CancelKeyPress += (_, _) =>
                        {
                            logger.LogDebug("Cancelling processing...");
                            cts.Cancel();
                        };

                        var processor = BinviewCli.BuildProcessor(logger, loggerFactory, configuration, args);

                        try
                        {
                            logger.LogInformation("{ExecutableName} v{ExecutableVersion}", BinviewCli.executableName, BinviewCli.version);

                            await processor.ProcessAsync(cts.Token);

                            returnCode = defaultSuccessfulReturnCode;
                        }
                        catch (Exception ex)
                        {
                            returnCode = ex.HResult != 0 ? ex.HResult : defaultUnsuccessfulReturnCode;
                            logger.LogCritical(ex, "Something went wrong during processing");
                        }
                        finally
                        {
                            if (processor is IDisposable disposable)
                            {
                                logger.LogTrace("Disposing processor...");
                                disposable.Dispose();
                            }
                            else if (processor is IAsyncDisposable asyncDisposable)
                            {
                                logger.LogTrace("Disposing processor...");
                                await asyncDisposable.DisposeAsync();
                            }
                        }
                    }
                }
                finally
                {
                    loggerFactory?.Dispose();
                }
            }

            return returnCode;
        }

        private static IBinaryImageProcessor BuildProcessor(ILogger logger, ILoggerFactory loggerFactory, IConfigurationRoot configuration, string[] args)
        {
            var inputFilePath = configuration.GetValue<string>(CommandLineArgs.InputPath)!;
            logger.LogTrace("Configuration: using input file path '{ConfiguredInputFilePath}'", inputFilePath);

            var outputFilePath = configuration.GetValue<string>(CommandLineArgs.OutputPath)!;
            logger.LogTrace("Configuration: using output file path '{ConfiguredOutputFilePath}'", outputFilePath);

            if (args.ContainsKey(CommandLineArgs.ProcessSerial))
            {
                logger.LogTrace("Configuration: serial processing was requested. Using '{ProcessorTypeName}'", typeof(BinaryImageProcessor).FullName);
                return new BinaryImageProcessor(loggerFactory.CreateLogger<BinaryImageProcessor>(), inputFilePath, outputFilePath);
            }

            logger.LogTrace("Configuration: using '{ProcessorTypeName}'", typeof(ParallelBinaryImageProcessor).FullName);

            var threadCount = Environment.ProcessorCount + 1;
            if (configuration.TryGetInt32Value(CommandLineArgs.MaxConcurrency, out var passedThreadCount))
            {
                if (passedThreadCount > 0)
                {
                    logger.LogTrace("Configuration: using configured max concurrency of {MaxConcurrency}", passedThreadCount);
                    threadCount = passedThreadCount!.Value;
                }
                else
                {
                    logger.LogInformation("Configuration: configured max concurrency value of {PassedMaxConcurrency} is invalid. Must be an integer greater than zero. Falling back to default of {MaxConcurrency}", passedThreadCount, threadCount);
                }
            }

            return new ParallelBinaryImageProcessor(
                loggerFactory.CreateLogger<ParallelBinaryImageProcessor>(),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = threadCount
                },
                inputFilePath,
                outputFilePath);
        }

        private static Task ShowIntroAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Console.Out.WriteLineAsync($"{BinviewCli.executableName}: a small command-line application for generating images from binary files.{Environment.NewLine}".ToCharArray(), cancellationToken);
        }

        private static Task ShowVersionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Console.Out.WriteLineAsync($"Version: {BinviewCli.version}".ToCharArray(), cancellationToken);
        }

        private static async Task ShowHelpAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            const string tab = "    ";

            var cliBuilder = new StringBuilder();
            cliBuilder.Append($"Usage: {BinviewCli.executableName}");
            foreach (var kvp in CommandLineArgs.ConfigurationKeys)
            {
                cliBuilder.Append(' ');

                if (kvp.Value.Optional)
                {
                    cliBuilder.Append('[');
                }

                cliBuilder.Append($"--{kvp.Key}");
                if (!string.IsNullOrEmpty(kvp.Value.ParamName))
                {
                    cliBuilder.Append(string.Concat(" {", kvp.Value.ParamName, "}"));
                }

                if (kvp.Value.Optional)
                {
                    cliBuilder.Append(']');
                }
            }

            cliBuilder.AppendLine($"{Environment.NewLine}{Environment.NewLine}All command-line arguments should be prefixed with two dashes ('--')");
            cliBuilder.AppendLine("Command-line arguments:");
            var maxLengthCol1 = CommandLineArgs.ConfigurationKeys.Select(x => x.Key.Length + (!string.IsNullOrEmpty(x.Value.ParamName) ? x.Value.ParamName.Length : 0)).Max() + (tab.Length * 3);
            cliBuilder.AppendLine();
            foreach (var kvp in CommandLineArgs.ConfigurationKeys)
            {
                var thisArgString = new StringBuilder(string.Concat(tab, kvp.Key));
                if (!string.IsNullOrEmpty(kvp.Value.ParamName))
                {
                    thisArgString = thisArgString.Append(string.Concat(" {", kvp.Value.ParamName, "}"));
                }

                maxLengthCol1 = Math.Max(maxLengthCol1, thisArgString.Length);

                while (thisArgString.Length < maxLengthCol1)
                {
                    thisArgString.Append(' ');
                }

                thisArgString.Append($"{(kvp.Value.Optional ? "Optional." : "Required.")} ");
                thisArgString.Append(kvp.Value.Description);
                cliBuilder.AppendLine(thisArgString.ToString());
            }

            await Console.Out.WriteLineAsync(cliBuilder, cancellationToken);
        }

        private static IConfigurationRoot BuildCommandLineConfig(string[] args)
        {
            return new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();
        }

        private static ILoggerFactory BuildLoggerFactory(IConfigurationRoot configuration, bool showTimestamps)
        {
            return LoggerFactory.Create(builder =>
            {
                if (!configuration.TryGetEnumValue<LogLevel>(CommandLineArgs.LogLevel, out var logLevel))
                {
                    logLevel = defaultLogLevel;
                }

                builder.SetMinimumLevel(logLevel);

                var timestampFormat = showTimestamps
                    ? $"[{CultureInfo.CurrentCulture.DateTimeFormat.SortableDateTimePattern}.fff] "
                    : default(string?);

                builder.AddSimpleConsole(cfg =>
                {
                    cfg.IncludeScopes = false;
                    cfg.TimestampFormat = timestampFormat;
                    cfg.UseUtcTimestamp = true;
                    cfg.ColorBehavior = LoggerColorBehavior.Enabled;
                    cfg.SingleLine = false;
                });
            });
        }
    }
}