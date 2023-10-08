using System.Globalization;
using System.Text;
using binview.cli.Constants;
using binview.cli.Extensions;
using binview.cli.Processor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace binview.cli
{
    public class BinviewCli
    {
        private const LogLevel defaultLogLevel = LogLevel.Debug;
        private static readonly Version version = new Version(0, 0, 1, 0);
        private static readonly string executableName = AppDomain.CurrentDomain.FriendlyName;

        public static async Task<int> Main(string[] args)
        {
            const int defaultUnsuccessfulReturnCode = 1;
            const int defaultSuccessfulReturnCode = 0;
            var returnCode = defaultUnsuccessfulReturnCode;

            if (args.ContainsKey("help"))
            {
                await BinviewCli.ShowIntroAsync();
                await BinviewCli.ShowHelpAsync();
                returnCode = defaultSuccessfulReturnCode;
            }
            else if (args.ContainsKey("version"))
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
                    loggerFactory = BinviewCli.BuildLoggerFactory(configuration);
                    var logger = loggerFactory.CreateLogger<BinviewCli>();
                    try
                    {
                        logger.LogInformation("{ExecutableName} v{ExecutableVersion}", BinviewCli.executableName, BinviewCli.version);

                        var inputFilePath = configuration.GetValue<string>(Configuration.InputPath)!;
                        var outputFilePath = configuration.GetValue<string>(Configuration.OutputPath)!;
                        var processor = new BinaryImageProcessor(loggerFactory.CreateLogger<BinaryImageProcessor>(), inputFilePath, outputFilePath);
                        await processor.ProcessAsync();

                        returnCode = defaultSuccessfulReturnCode;
                    }
                    catch (Exception ex)
                    {
                        returnCode = ex.HResult != 0 ? ex.HResult : defaultUnsuccessfulReturnCode;
                        logger.LogError(ex, "Something went wrong");
                    }
                }
                finally
                {
                    loggerFactory?.Dispose();
                }
            }

            return returnCode;
        }

        private static Task ShowIntroAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Console.Out.WriteLineAsync($"{BinviewCli.executableName}: a small command-line application for generating images from binary files.{Environment.NewLine}");
        }

        private static Task ShowVersionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Console.Out.WriteLineAsync($"Version: {BinviewCli.version}");
        }

        private static async Task ShowHelpAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            const string tab = "    ";

            var cliBuilder = new StringBuilder();
            cliBuilder.Append($"Usage: {BinviewCli.executableName}");
            foreach (var kvp in Configuration.ConfigurationKeys)
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
            var maxLengthCol1 = Configuration.ConfigurationKeys.Select(x => x.Key.Length + (!string.IsNullOrEmpty(x.Value.ParamName) ? x.Value.ParamName.Length : 0)).Max() + (tab.Length * 3);
            cliBuilder.AppendLine();
            foreach (var kvp in Configuration.ConfigurationKeys)
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

        private static ILoggerFactory BuildLoggerFactory(IConfigurationRoot configuration)
        {
            return LoggerFactory.Create(builder =>
            {
                if (!configuration.TryGetEnumValue<LogLevel>(Configuration.LogLevel, out var logLevel))
                {
                    logLevel = defaultLogLevel;
                }

                builder.SetMinimumLevel(logLevel);

                var timestampFormat = configuration.ContainsKey(Configuration.LogShowTimestamp)
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