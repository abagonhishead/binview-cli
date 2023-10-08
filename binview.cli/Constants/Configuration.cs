using Microsoft.Extensions.Logging;

namespace binview.cli.Constants
{
    public static class Configuration
    {
        private static readonly IReadOnlyDictionary<LogLevel, int> logLevels = Enum.GetValues<LogLevel>().ToDictionary(x => x, x => x.GetHashCode());

        public const string LogLevel = "log-level";
        public const string LogShowTimestamp = "log-show-timestamp";
        public const string InputPath = "input-path";
        public const string OutputPath = "output-path";
        public const string Help = "help";
        public const string Version = "version";

        public static readonly IReadOnlyDictionary<string, (string? ParamName, string Description, bool Optional)> ConfigurationKeys = new Dictionary<string, (string? ParamName, string Description, bool Optional)>(StringComparer.OrdinalIgnoreCase)
        {
            { Configuration.InputPath, ("file-path", "The path to the file to read from.", false) },
            { Configuration.OutputPath, ("file-path", "The path to write the output file to.", false) },
            { Configuration.LogLevel, ("level", $"Set the log level. Valid values are: {string.Join(", ", logLevels.Select(x => $"'{x.Key}' / '{x.Value}'"))}", true) },
            { Configuration.LogShowTimestamp, (null, "Show timestamps for log output", true) },
            { Configuration.Help, (null, "Print the help output (this)", true) },
            { Configuration.Version, (null, "Print the current version and exit.", true) },
        };
    }
}
