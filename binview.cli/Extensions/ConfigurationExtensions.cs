﻿namespace binview.cli.Extensions
{
    using Microsoft.Extensions.Configuration;

    public static class ConfigurationExtensions
    {
        public static bool TryGetValue<T>(this IConfiguration source, string key, out T? result)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Cannot be null or empty", nameof(key));
            }

            result = source.GetValue<T>(key);
            return result != null;
        }

        public static bool TryGetInt64Value(this IConfiguration source, string key, out long? result)
        {
            result = default(long?);
            if (source.TryGetValue(key, out string stringResult) && long.TryParse(stringResult, out var longResult))
            {
                result = longResult;
            }

            return result.HasValue;
        }

        public static bool TryGetInt32Value(this IConfiguration source, string key, out int? result)
        {
            result = default(int?);
            if (source.TryGetInt64Value(key, out var longResult) &&
                longResult < int.MaxValue)
            {
                result = (int)longResult;
            }

            return result.HasValue;
        }

        public static bool TryGetEnumValue<T>(this IConfiguration source, string key, out T? result)
            where T : Enum
        {
            result = default(T?);
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Cannot be null or empty", nameof(key));
            }

            var intResult = source.GetValue<int?>(key);
            if (intResult.HasValue &&
                Enum.IsDefined(typeof(T), intResult))
            {
                result = Enum.GetValues(typeof(T))
                    .Cast<T>()
                    .Single(x => x.GetHashCode() == intResult!.Value);
                return true;
            }

            var stringResult = source.GetValue<string>(key);
            if (!string.IsNullOrEmpty(stringResult) &&
                Enum.TryParse(typeof(T), stringResult, true, out var enumResult))
            {
                result = (T)enumResult;
                return true;
            }

            return false;
        }

        public static bool ContainsKey(this string[] source, string key)
        {
            key = key.TrimStart('-').TrimStart('/').Trim();
            var keys = new[]
            {
                $"-{key}",
                $"--{key}",
                $"/{key}",
            };

            return source.Any(x => keys.Contains(x, StringComparer.OrdinalIgnoreCase));
        }
    }
}
