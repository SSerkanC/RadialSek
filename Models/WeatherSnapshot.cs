using System;

namespace RadialSek.Models
{
    public sealed class WeatherSnapshot
    {
        public int WeatherCode { get; set; }
        public bool IsDay { get; set; }
        public double? TemperatureCelsius { get; set; }
        public double Precipitation { get; set; }
        public double CloudCover { get; set; }
        public DateTime RetrievedAtUtc { get; set; }
    }
}
