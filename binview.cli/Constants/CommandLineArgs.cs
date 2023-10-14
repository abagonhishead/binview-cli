namespace binview.cli.Constants
{
    using Microsoft.Extensions.Logging;

    public static class CommandLineArgs
    {
        private static readonly IReadOnlyDictionary<LogLevel, int> logLevels = Enum.GetValues<LogLevel>().ToDictionary(x => x, x => x.GetHashCode());

        public const string LogLevel = "log-level";
        public const string LogShowTimestamp = "log-show-timestamp";
        public const string ProcessSerial = "process-in-serial";
        public const string MaxConcurrency = "max-concurrency";
        public const string InputPath = "input-path";
        public const string OutputPath = "output-path";
        public const string Help = "help";
        public const string Version = "version";

        public static readonly IReadOnlyDictionary<string, (string? ParamName, string Description, bool Optional)> ConfigurationKeys = new Dictionary<string, (string? ParamName, string Description, bool Optional)>(StringComparer.OrdinalIgnoreCase)
        {
            { CommandLineArgs.InputPath, ("file-path", "String. The path to the file to read from.", false) },
            { CommandLineArgs.OutputPath, ("file-path", "String. The path to write the output file to.", false) },
            { CommandLineArgs.ProcessSerial, (null, $"Switch. Process the input data in serial, rather than in parallel. Mostly only useful for debugging. If this is passed, '{CommandLineArgs.MaxConcurrency}' is ignored.", true) },
            { CommandLineArgs.MaxConcurrency, ("{max-thread-count}", "Integer. The maximum number of worker threads to run concurrently. Defaults to the logical processor count plus 1.", true) },
            { CommandLineArgs.LogLevel, ("level", $"Set the log level. Valid values are: {string.Join(", ", logLevels.Select(x => $"'{x.Key}' / '{x.Value}'"))}", true) },
            { CommandLineArgs.LogShowTimestamp, (null, "Switch. Show timestamps for log output", true) },
            { CommandLineArgs.Help, (null, "Switch. Print the help output (this)", true) },
            { CommandLineArgs.Version, (null, "Switch. Print the current version and exit.", true) },
        };
    }
}
