using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RadialSek.Models;

namespace RadialSek.Services
{
    public sealed class OpenMeteoWeatherService
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(4.5)
        };

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
        private readonly object _sync = new object();
        private WeatherSnapshot? _cachedSnapshot;
        private DateTime _cachedAtUtc = DateTime.MinValue;
        private Task<WeatherSnapshot?>? _pendingFetchTask;

        private const double DefaultLatitude = 41.0082;
        private const double DefaultLongitude = 28.9784;

        public static OpenMeteoWeatherService Instance { get; } = new OpenMeteoWeatherService();

        private OpenMeteoWeatherService()
        {
        }

        public WeatherSnapshot? GetCachedSnapshot()
        {
            lock (_sync)
            {
                return _cachedSnapshot;
            }
        }

        public Task<WeatherSnapshot?> GetCurrentSnapshotAsync()
        {
            lock (_sync)
            {
                if (_cachedSnapshot != null && DateTime.UtcNow - _cachedAtUtc <= CacheDuration)
                {
                    return Task.FromResult<WeatherSnapshot?>(_cachedSnapshot);
                }

                if (_pendingFetchTask != null)
                {
                    return _pendingFetchTask;
                }

                _pendingFetchTask = FetchSnapshotAsync();
                return _pendingFetchTask;
            }
        }

        private async Task<WeatherSnapshot?> FetchSnapshotAsync()
        {
            try
            {
                var url = BuildUrl(DefaultLatitude, DefaultLongitude);
                using var response = await Client.GetAsync(url).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

                if (!document.RootElement.TryGetProperty("current", out var current))
                {
                    return GetCachedSnapshot();
                }

                var weatherCode = TryReadInt(current, "weather_code", fallback: 2);
                var isDay = TryReadInt(current, "is_day", fallback: 1) == 1;
                var temperature = TryReadDouble(current, "temperature_2m", fallback: double.NaN);
                var precipitation = TryReadDouble(current, "precipitation", fallback: 0.0);
                var cloudCover = TryReadDouble(current, "cloud_cover", fallback: 0.0);

                var snapshot = new WeatherSnapshot
                {
                    WeatherCode = weatherCode,
                    IsDay = isDay,
                    TemperatureCelsius = double.IsNaN(temperature) ? (double?)null : temperature,
                    Precipitation = precipitation,
                    CloudCover = cloudCover,
                    RetrievedAtUtc = DateTime.UtcNow
                };

                lock (_sync)
                {
                    _cachedSnapshot = snapshot;
                    _cachedAtUtc = DateTime.UtcNow;
                }

                return snapshot;
            }
            catch
            {
                return GetCachedSnapshot();
            }
            finally
            {
                lock (_sync)
                {
                    _pendingFetchTask = null;
                }
            }
        }

        private static string BuildUrl(double latitude, double longitude)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=weather_code,is_day,temperature_2m,precipitation,cloud_cover&timezone=auto&forecast_days=1",
                latitude,
                longitude);
        }

        private static int TryReadInt(JsonElement source, string propertyName, int fallback)
        {
            if (source.TryGetProperty(propertyName, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
                {
                    return intValue;
                }

                if (value.ValueKind == JsonValueKind.String &&
                    int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return fallback;
        }

        private static double TryReadDouble(JsonElement source, string propertyName, double fallback)
        {
            if (source.TryGetProperty(propertyName, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var doubleValue))
                {
                    return doubleValue;
                }

                if (value.ValueKind == JsonValueKind.String &&
                    double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return fallback;
        }
    }
}
