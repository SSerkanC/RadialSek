using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using RadialSek.Models;
using RadialSek.Services;

namespace RadialSek.UI
{
    public partial class RadialOverlayWindow
    {
        private async void StartWeatherVisuals()
        {
            if (RootCanvas == null || !IsVisible)
            {
                return;
            }

            if (!_weatherSettings.EnableAnimations)
            {
                ClearWeatherVisuals();
                return;
            }

            var fetchVersion = ++_weatherFetchVersion;

            if (!_weatherSettings.UseLiveData)
            {
                var manualSnapshot = CreateManualWeatherSnapshot();
                ApplyWeatherSnapshot(manualSnapshot, forcePreset: ResolveManualWeatherPreset(), forceIsDay: ResolveConfiguredDayNight(manualSnapshot.IsDay));
                return;
            }

            var cachedSnapshot = _weatherService.GetCachedSnapshot() ?? CreateFallbackWeatherSnapshot();
            ApplyWeatherSnapshot(cachedSnapshot, forceIsDay: ResolveConfiguredDayNight(cachedSnapshot.IsDay));

            try
            {
                var snapshot = await _weatherService.GetCurrentSnapshotAsync().ConfigureAwait(false);
                if (snapshot == null)
                {
                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (fetchVersion != _weatherFetchVersion ||
                        !IsVisible ||
                        RootCanvas == null)
                    {
                        return;
                    }

                    ApplyWeatherSnapshot(snapshot, forceIsDay: ResolveConfiguredDayNight(snapshot.IsDay));
                }, DispatcherPriority.Background);
            }
            catch
            {
                // keep fallback visuals
            }
        }

        private void StopWeatherVisuals(bool invalidatePendingFetch = true)
        {
            if (invalidatePendingFetch)
            {
                _weatherFetchVersion++;
            }

            ClearWeatherVisuals();
        }

        private void ClearWeatherVisuals()
        {
            StopWeatherCloudAnimation();
            _hasWeatherVisualSignature = false;
            _weatherVisualSignature = 0;
            _activeWeatherSpeedScale = 1.0;
            _activeWeatherIntensityScale = 1.0;

            if (_weatherVisualElements.Count == 0)
            {
                return;
            }

            var visuals = _weatherVisualElements.ToArray();
            foreach (var visual in visuals)
            {
                RemoveWeatherVisual(visual);
            }

            _weatherVisualElements.Clear();
        }

        private void ApplyWeatherSnapshot(
            WeatherSnapshot snapshot,
            WeatherVisualPreset? forcePreset = null,
            bool? forceIsDay = null)
        {
            if (RootCanvas == null || !IsVisible)
            {
                return;
            }

            var profile = GetLayoutProfile();
            var centerSize = _centerPanel != null
                ? Math.Min(_centerPanel.Width, _centerPanel.Height)
                : profile.CenterSize;
            if (centerSize <= 0)
            {
                return;
            }

            var preset = forcePreset ?? ResolveWeatherVisualPreset(snapshot);
            var isDay = forceIsDay ?? snapshot.IsDay;
            var speedScale = WeatherSettingsService.ClampSpeedScale(_weatherSettings.AnimationSpeedScale);
            var intensityScale = WeatherSettingsService.ClampIntensityScale(_weatherSettings.AnimationIntensityScale);
            _activeWeatherSpeedScale = speedScale;
            _activeWeatherIntensityScale = intensityScale;

            var signature = BuildWeatherVisualSignature(snapshot, preset, isDay, centerSize);
            if (_hasWeatherVisualSignature &&
                _weatherVisualSignature == signature &&
                _weatherVisualElements.Count > 0)
            {
                return;
            }

            ClearWeatherVisuals();
            _activeWeatherSpeedScale = speedScale;
            _activeWeatherIntensityScale = intensityScale;
            AddDayNightCycleVisuals(isDay, centerSize);
            AddWeatherConditionLabel(preset, centerSize, snapshot.TemperatureCelsius);

            switch (preset)
            {
                case WeatherVisualPreset.ClearSkyDay:
                case WeatherVisualPreset.ClearSkyNight:
                    break;
                case WeatherVisualPreset.MostlyClear:
                    StartWeatherCloudAnimation(frontOpacity: 0.56, rearOpacity: 0.38, cloudScale: 0.78);
                    AddSupplementaryClouds(centerSize, frontClouds: 0, rearClouds: 1, density: 0.74);
                    break;
                case WeatherVisualPreset.PartlyCloudy:
                    StartWeatherCloudAnimation(frontOpacity: 0.82, rearOpacity: 0.62, cloudScale: 0.92);
                    AddSupplementaryClouds(centerSize, frontClouds: 1, rearClouds: 1, density: 0.90);
                    break;
                case WeatherVisualPreset.Overcast:
                    StartWeatherCloudAnimation(frontOpacity: 0.95, rearOpacity: 0.78, cloudScale: 1.08);
                    AddSupplementaryClouds(centerSize, frontClouds: 3, rearClouds: 3, density: 1.20);
                    AddFogOverlay(centerSize, dense: false);
                    break;
                case WeatherVisualPreset.Fog:
                    StartWeatherCloudAnimation(frontOpacity: 0.88, rearOpacity: 0.72, cloudScale: 1.04);
                    AddSupplementaryClouds(centerSize, frontClouds: 2, rearClouds: 3, density: 1.10);
                    AddFogOverlay(centerSize, dense: true);
                    break;
                case WeatherVisualPreset.Drizzle:
                    StartWeatherCloudAnimation(frontOpacity: 0.90, rearOpacity: 0.74, cloudScale: 1.04);
                    AddSupplementaryClouds(centerSize, frontClouds: 2, rearClouds: 2, density: 1.08);
                    AddRainOverlay(centerSize, dropCount: ScaleWeatherCount(16), fast: false, lengthScale: 0.70, opacityTarget: 0.58, windScale: -0.06, speedMultiplier: 0.78);
                    break;
                case WeatherVisualPreset.FreezingDrizzle:
                    StartWeatherCloudAnimation(frontOpacity: 0.92, rearOpacity: 0.76, cloudScale: 1.05);
                    AddSupplementaryClouds(centerSize, frontClouds: 2, rearClouds: 2, density: 1.10);
                    AddRainOverlay(centerSize, dropCount: ScaleWeatherCount(18), fast: false, icyTone: true, lengthScale: 0.74, opacityTarget: 0.64, windScale: -0.07, speedMultiplier: 0.80);
                    break;
                case WeatherVisualPreset.Rain:
                    StartWeatherCloudAnimation(frontOpacity: 0.95, rearOpacity: 0.80, cloudScale: 1.10);
                    AddSupplementaryClouds(centerSize, frontClouds: 3, rearClouds: 3, density: 1.24);
                    AddRainOverlay(centerSize, dropCount: ScaleWeatherCount(36), fast: true, lengthScale: 0.96, opacityTarget: 0.84, windScale: -0.10, speedMultiplier: 1.05);
                    break;
                case WeatherVisualPreset.FreezingRain:
                    StartWeatherCloudAnimation(frontOpacity: 0.95, rearOpacity: 0.80, cloudScale: 1.10);
                    AddSupplementaryClouds(centerSize, frontClouds: 3, rearClouds: 3, density: 1.22);
                    AddRainOverlay(centerSize, dropCount: ScaleWeatherCount(34), fast: true, icyTone: true, lengthScale: 1.00, opacityTarget: 0.88, windScale: -0.10, speedMultiplier: 1.08);
                    break;
                case WeatherVisualPreset.Snow:
                    StartWeatherCloudAnimation(frontOpacity: 0.90, rearOpacity: 0.74, cloudScale: 1.02);
                    AddSupplementaryClouds(centerSize, frontClouds: 2, rearClouds: 2, density: 1.00);
                    AddSnowOverlay(centerSize, flakeCount: ScaleWeatherCount(32), grainMode: false);
                    break;
                case WeatherVisualPreset.SnowGrains:
                    StartWeatherCloudAnimation(frontOpacity: 0.92, rearOpacity: 0.76, cloudScale: 1.04);
                    AddSupplementaryClouds(centerSize, frontClouds: 2, rearClouds: 2, density: 1.04);
                    AddSnowOverlay(centerSize, flakeCount: ScaleWeatherCount(48), grainMode: true);
                    break;
                case WeatherVisualPreset.RainShowers:
                    StartWeatherCloudAnimation(frontOpacity: 0.96, rearOpacity: 0.82, cloudScale: 1.14);
                    AddSupplementaryClouds(centerSize, frontClouds: 4, rearClouds: 3, density: 1.34);
                    AddRainOverlay(centerSize, dropCount: ScaleWeatherCount(54), fast: true, lengthScale: 1.08, opacityTarget: 0.92, windScale: -0.14, speedMultiplier: 1.30);
                    AddRainOverlay(centerSize, dropCount: ScaleWeatherCount(24), fast: true, lengthScale: 1.24, opacityTarget: 0.70, windScale: -0.18, speedMultiplier: 1.45);
                    break;
                case WeatherVisualPreset.SnowShowers:
                    StartWeatherCloudAnimation(frontOpacity: 0.93, rearOpacity: 0.78, cloudScale: 1.08);
                    AddSupplementaryClouds(centerSize, frontClouds: 3, rearClouds: 3, density: 1.16);
                    AddSnowOverlay(centerSize, flakeCount: ScaleWeatherCount(40), grainMode: false);
                    break;
                case WeatherVisualPreset.Thunderstorm:
                    StartWeatherCloudAnimation(frontOpacity: 0.98, rearOpacity: 0.84, cloudScale: 1.20);
                    AddSupplementaryClouds(centerSize, frontClouds: 5, rearClouds: 4, density: 1.44);
                    AddRainOverlay(centerSize, dropCount: ScaleWeatherCount(62), fast: true, lengthScale: 1.14, opacityTarget: 0.96, windScale: -0.18, speedMultiplier: 1.52);
                    AddRainOverlay(centerSize, dropCount: ScaleWeatherCount(26), fast: true, lengthScale: 1.34, opacityTarget: 0.74, windScale: -0.22, speedMultiplier: 1.70);
                    AddLightningOverlay(centerSize);
                    break;
                case WeatherVisualPreset.ThunderstormHail:
                    StartWeatherCloudAnimation(frontOpacity: 0.98, rearOpacity: 0.84, cloudScale: 1.22);
                    AddSupplementaryClouds(centerSize, frontClouds: 5, rearClouds: 4, density: 1.48);
                    AddRainOverlay(centerSize, dropCount: ScaleWeatherCount(58), fast: true, lengthScale: 1.16, opacityTarget: 0.94, windScale: -0.18, speedMultiplier: 1.56);
                    AddRainOverlay(centerSize, dropCount: ScaleWeatherCount(24), fast: true, lengthScale: 1.34, opacityTarget: 0.72, windScale: -0.22, speedMultiplier: 1.72);
                    AddHailOverlay(centerSize, pelletCount: ScaleWeatherCount(26));
                    AddLightningOverlay(centerSize);
                    break;
            }

            _weatherVisualSignature = signature;
            _hasWeatherVisualSignature = _weatherVisualElements.Count > 0;
        }

        private WeatherVisualPreset ResolveWeatherVisualPreset(WeatherSnapshot snapshot)
        {
            var mapped = MapWeatherCodeToPreset(snapshot.WeatherCode);
            if (mapped == WeatherVisualPreset.ClearSkyDay || mapped == WeatherVisualPreset.ClearSkyNight)
            {
                if (snapshot.Precipitation >= 0.25)
                {
                    return snapshot.IsDay ? WeatherVisualPreset.RainShowers : WeatherVisualPreset.Rain;
                }

                if (snapshot.CloudCover >= 70)
                {
                    return WeatherVisualPreset.Overcast;
                }

                if (snapshot.CloudCover >= 35)
                {
                    return WeatherVisualPreset.PartlyCloudy;
                }

                return snapshot.IsDay ? WeatherVisualPreset.ClearSkyDay : WeatherVisualPreset.ClearSkyNight;
            }

            return mapped;
        }

        private static WeatherVisualPreset MapWeatherCodeToPreset(int weatherCode)
        {
            switch (weatherCode)
            {
                case 0: return WeatherVisualPreset.ClearSkyDay;
                case 1: return WeatherVisualPreset.MostlyClear;
                case 2: return WeatherVisualPreset.PartlyCloudy;
                case 3: return WeatherVisualPreset.Overcast;
                case 45:
                case 48: return WeatherVisualPreset.Fog;
                case 51:
                case 53:
                case 55: return WeatherVisualPreset.Drizzle;
                case 56:
                case 57: return WeatherVisualPreset.FreezingDrizzle;
                case 61:
                case 63:
                case 65: return WeatherVisualPreset.Rain;
                case 66:
                case 67: return WeatherVisualPreset.FreezingRain;
                case 71:
                case 73:
                case 75: return WeatherVisualPreset.Snow;
                case 77: return WeatherVisualPreset.SnowGrains;
                case 80:
                case 81:
                case 82: return WeatherVisualPreset.RainShowers;
                case 85:
                case 86: return WeatherVisualPreset.SnowShowers;
                case 95: return WeatherVisualPreset.Thunderstorm;
                case 96:
                case 99: return WeatherVisualPreset.ThunderstormHail;
                default: return WeatherVisualPreset.PartlyCloudy;
            }
        }

        private WeatherVisualPreset ResolveManualWeatherPreset()
        {
            var key = WeatherSettingsService.ResolveWeatherPresetKey(_weatherSettings.ManualPreset);
            switch (key)
            {
                case "ClearSky":
                    return ResolveConfiguredDayNight(true) ? WeatherVisualPreset.ClearSkyDay : WeatherVisualPreset.ClearSkyNight;
                case "MostlyClear":
                    return WeatherVisualPreset.MostlyClear;
                case "PartlyCloudy":
                    return WeatherVisualPreset.PartlyCloudy;
                case "Overcast":
                    return WeatherVisualPreset.Overcast;
                case "Fog":
                    return WeatherVisualPreset.Fog;
                case "Drizzle":
                    return WeatherVisualPreset.Drizzle;
                case "FreezingDrizzle":
                    return WeatherVisualPreset.FreezingDrizzle;
                case "Rain":
                    return WeatherVisualPreset.Rain;
                case "FreezingRain":
                    return WeatherVisualPreset.FreezingRain;
                case "Snow":
                    return WeatherVisualPreset.Snow;
                case "SnowGrains":
                    return WeatherVisualPreset.SnowGrains;
                case "RainShowers":
                    return WeatherVisualPreset.RainShowers;
                case "SnowShowers":
                    return WeatherVisualPreset.SnowShowers;
                case "Thunderstorm":
                    return WeatherVisualPreset.Thunderstorm;
                case "ThunderstormHail":
                    return WeatherVisualPreset.ThunderstormHail;
                default:
                    return WeatherVisualPreset.PartlyCloudy;
            }
        }

        private WeatherSnapshot CreateManualWeatherSnapshot()
        {
            var isDay = ResolveConfiguredDayNight(DateTime.Now.Hour >= 6 && DateTime.Now.Hour < 19);
            var preset = ResolveManualWeatherPreset();
            var mappedCode = GetRepresentativeWeatherCodeForPreset(preset);
            return new WeatherSnapshot
            {
                WeatherCode = mappedCode,
                IsDay = isDay,
                TemperatureCelsius = EstimateTemperatureForPreset(preset, isDay),
                CloudCover = EstimateCloudCoverForPreset(preset),
                Precipitation = EstimatePrecipitationForPreset(preset),
                RetrievedAtUtc = DateTime.UtcNow
            };
        }

        private int GetRepresentativeWeatherCodeForPreset(WeatherVisualPreset preset)
        {
            switch (preset)
            {
                case WeatherVisualPreset.ClearSkyDay:
                case WeatherVisualPreset.ClearSkyNight:
                    return 0;
                case WeatherVisualPreset.MostlyClear:
                    return 1;
                case WeatherVisualPreset.PartlyCloudy:
                    return 2;
                case WeatherVisualPreset.Overcast:
                    return 3;
                case WeatherVisualPreset.Fog:
                    return 45;
                case WeatherVisualPreset.Drizzle:
                    return 53;
                case WeatherVisualPreset.FreezingDrizzle:
                    return 56;
                case WeatherVisualPreset.Rain:
                    return 63;
                case WeatherVisualPreset.FreezingRain:
                    return 67;
                case WeatherVisualPreset.Snow:
                    return 73;
                case WeatherVisualPreset.SnowGrains:
                    return 77;
                case WeatherVisualPreset.RainShowers:
                    return 81;
                case WeatherVisualPreset.SnowShowers:
                    return 85;
                case WeatherVisualPreset.Thunderstorm:
                    return 95;
                case WeatherVisualPreset.ThunderstormHail:
                    return 99;
                default:
                    return 2;
            }
        }

        private static double EstimateCloudCoverForPreset(WeatherVisualPreset preset)
        {
            switch (preset)
            {
                case WeatherVisualPreset.ClearSkyDay:
                case WeatherVisualPreset.ClearSkyNight:
                    return 8;
                case WeatherVisualPreset.MostlyClear:
                    return 24;
                case WeatherVisualPreset.PartlyCloudy:
                    return 52;
                case WeatherVisualPreset.Overcast:
                case WeatherVisualPreset.Fog:
                case WeatherVisualPreset.Drizzle:
                case WeatherVisualPreset.FreezingDrizzle:
                case WeatherVisualPreset.Rain:
                case WeatherVisualPreset.FreezingRain:
                case WeatherVisualPreset.Snow:
                case WeatherVisualPreset.SnowGrains:
                case WeatherVisualPreset.RainShowers:
                case WeatherVisualPreset.SnowShowers:
                case WeatherVisualPreset.Thunderstorm:
                case WeatherVisualPreset.ThunderstormHail:
                    return 88;
                default:
                    return 48;
            }
        }

        private static double EstimatePrecipitationForPreset(WeatherVisualPreset preset)
        {
            switch (preset)
            {
                case WeatherVisualPreset.Drizzle:
                case WeatherVisualPreset.FreezingDrizzle:
                    return 0.2;
                case WeatherVisualPreset.Rain:
                case WeatherVisualPreset.FreezingRain:
                case WeatherVisualPreset.Snow:
                case WeatherVisualPreset.SnowGrains:
                case WeatherVisualPreset.RainShowers:
                case WeatherVisualPreset.SnowShowers:
                    return 0.6;
                case WeatherVisualPreset.Thunderstorm:
                case WeatherVisualPreset.ThunderstormHail:
                    return 1.0;
                default:
                    return 0.0;
            }
        }

        private static double EstimateTemperatureForPreset(WeatherVisualPreset preset, bool isDay)
        {
            switch (preset)
            {
                case WeatherVisualPreset.ClearSkyDay:
                case WeatherVisualPreset.ClearSkyNight:
                    return isDay ? 25.0 : 17.0;
                case WeatherVisualPreset.MostlyClear:
                    return isDay ? 24.0 : 16.0;
                case WeatherVisualPreset.PartlyCloudy:
                    return isDay ? 22.0 : 15.0;
                case WeatherVisualPreset.Overcast:
                    return isDay ? 20.0 : 14.0;
                case WeatherVisualPreset.Fog:
                    return isDay ? 17.0 : 12.0;
                case WeatherVisualPreset.Drizzle:
                case WeatherVisualPreset.FreezingDrizzle:
                case WeatherVisualPreset.Rain:
                case WeatherVisualPreset.FreezingRain:
                case WeatherVisualPreset.RainShowers:
                    return isDay ? 16.0 : 11.0;
                case WeatherVisualPreset.Snow:
                case WeatherVisualPreset.SnowGrains:
                case WeatherVisualPreset.SnowShowers:
                    return isDay ? 1.0 : -2.0;
                case WeatherVisualPreset.Thunderstorm:
                case WeatherVisualPreset.ThunderstormHail:
                    return isDay ? 15.0 : 12.0;
                default:
                    return isDay ? 21.0 : 14.0;
            }
        }

        private bool ResolveConfiguredDayNight(bool fallbackValue)
        {
            var mode = WeatherSettingsService.ResolveDayNightModeKey(_weatherSettings.DayNightMode);
            switch (mode)
            {
                case WeatherSettingsService.DayNightDay:
                    return true;
                case WeatherSettingsService.DayNightNight:
                    return false;
                default:
                    return fallbackValue;
            }
        }

        private int BuildWeatherVisualSignature(WeatherSnapshot snapshot, WeatherVisualPreset preset, bool isDay, double centerSize)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (int)preset;
                hash = (hash * 31) + (isDay ? 1 : 0);
                hash = (hash * 31) + (int)Math.Round(centerSize);
                hash = (hash * 31) + (snapshot.TemperatureCelsius.HasValue
                    ? (int)Math.Round(snapshot.TemperatureCelsius.Value * 10.0)
                    : int.MinValue);
                hash = (hash * 31) + (int)Math.Round(_activeWeatherSpeedScale * 100.0);
                hash = (hash * 31) + (int)Math.Round(_activeWeatherIntensityScale * 100.0);
                return hash;
            }
        }

        private int ScaleWeatherCount(int baseCount)
        {
            return Math.Max(1, (int)Math.Round(baseCount * GetWeatherIntensityScale()));
        }

        private double GetWeatherSpeedScale()
        {
            return _activeWeatherSpeedScale > 0
                ? _activeWeatherSpeedScale
                : WeatherSettingsService.ClampSpeedScale(_weatherSettings.AnimationSpeedScale);
        }

        private double GetWeatherIntensityScale()
        {
            return _activeWeatherIntensityScale > 0
                ? _activeWeatherIntensityScale
                : WeatherSettingsService.ClampIntensityScale(_weatherSettings.AnimationIntensityScale);
        }

        private TimeSpan ScaleWeatherDuration(double milliseconds)
        {
            return TimeSpan.FromMilliseconds(Math.Max(60.0, milliseconds / GetWeatherSpeedScale()));
        }

        private KeyTime ScaleWeatherKeyTime(double milliseconds)
        {
            return KeyTime.FromTimeSpan(ScaleWeatherDuration(milliseconds));
        }

        private int GetWeatherParticleLimit(int lowTierLimit, int midTierLimit, int highTierLimit)
        {
            if (WeatherRenderTier <= 0)
            {
                return lowTierLimit;
            }

            if (WeatherRenderTier == 1)
            {
                return midTierLimit;
            }

            return highTierLimit;
        }

        private int CapRainParticleCount(int requestedCount)
        {
            var limit = GetWeatherParticleLimit(lowTierLimit: 34, midTierLimit: 56, highTierLimit: 82);
            return Math.Max(1, Math.Min(requestedCount, limit));
        }

        private int CapSnowParticleCount(int requestedCount)
        {
            var limit = GetWeatherParticleLimit(lowTierLimit: 26, midTierLimit: 42, highTierLimit: 64);
            return Math.Max(1, Math.Min(requestedCount, limit));
        }

        private int CapHailParticleCount(int requestedCount)
        {
            var limit = GetWeatherParticleLimit(lowTierLimit: 16, midTierLimit: 22, highTierLimit: 30);
            return Math.Max(1, Math.Min(requestedCount, limit));
        }

        private int CapStarCount(int requestedCount)
        {
            var limit = GetWeatherParticleLimit(lowTierLimit: 6, midTierLimit: 8, highTierLimit: 10);
            return Math.Max(1, Math.Min(requestedCount, limit));
        }

        private bool ShouldUseLightweightCloudEffects()
        {
            return WeatherRenderTier <= 1 || GetWeatherIntensityScale() >= 1.35;
        }

        private static WeatherSnapshot CreateFallbackWeatherSnapshot()
        {
            var now = DateTime.Now;
            var isDay = now.Hour >= 6 && now.Hour < 19;
            return new WeatherSnapshot
            {
                WeatherCode = isDay ? 1 : 0,
                IsDay = isDay,
                TemperatureCelsius = isDay ? 24.0 : 17.0,
                CloudCover = isDay ? 28.0 : 10.0,
                Precipitation = 0.0,
                RetrievedAtUtc = DateTime.UtcNow
            };
        }

        private void AddDayNightCycleVisuals(bool isDay, double centerSize)
        {
            var cycleDuration = ScaleWeatherDuration(WeatherCycleDuration.TotalMilliseconds);
            var glow = new Ellipse
            {
                Width = centerSize * 2.55,
                Height = centerSize * 2.55,
                Fill = GetCachedBrush(isDay
                    ? Color.FromArgb(72, 255, 194, 80)
                    : Color.FromArgb(92, 96, 146, 255)),
                Opacity = isDay ? 0.28 : 0.34,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(glow, _centerX - glow.Width / 2.0);
            Canvas.SetTop(glow, _centerY - glow.Height / 2.0);
            RegisterWeatherVisual(glow, WeatherCloudRearZIndex - 8);

            glow.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = isDay ? 0.22 : 0.28,
                To = isDay ? 0.36 : 0.46,
                Duration = cycleDuration,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            });

            var bodySize = Math.Clamp(centerSize * 0.28, 32, 62);
            var celestial = new Ellipse
            {
                Width = bodySize,
                Height = bodySize,
                Fill = GetCachedBrush(isDay
                    ? Color.FromArgb(238, 255, 226, 112)
                    : Color.FromArgb(228, 232, 239, 255)),
                Opacity = isDay ? 0.90 : 0.82,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(celestial, _centerX + centerSize * 0.24 - bodySize / 2.0);
            Canvas.SetTop(celestial, _centerY - centerSize * 0.70 - bodySize / 2.0);
            RegisterWeatherVisual(celestial, WeatherCelestialZIndex);

            var translate = new TranslateTransform();
            celestial.RenderTransform = translate;
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
            {
                From = -centerSize * 0.18,
                To = centerSize * 0.18,
                Duration = cycleDuration,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });

            if (!isDay)
            {
                var starLayer = CreateWeatherLayer(centerSize * 2.2, centerSize * 1.1, _centerX - centerSize * 1.1, _centerY - centerSize * 0.95, WeatherCelestialZIndex + 1);
                var starCount = CapStarCount(10);
                for (var i = 0; i < starCount; i++)
                {
                    var size = 1.8 + _weatherRandom.NextDouble() * 2.6;
                    var star = new Ellipse
                    {
                        Width = size,
                        Height = size,
                        Fill = GetCachedBrush(Color.FromArgb(230, 244, 249, 255)),
                        Opacity = 0.18 + _weatherRandom.NextDouble() * 0.60,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(star, _weatherRandom.NextDouble() * Math.Max(1.0, starLayer.Width - size));
                    Canvas.SetTop(star, _weatherRandom.NextDouble() * Math.Max(1.0, starLayer.Height - size));
                    starLayer.Children.Add(star);
                    star.BeginAnimation(OpacityProperty, new DoubleAnimation
                    {
                        From = 0.12,
                        To = 0.90,
                        Duration = ScaleWeatherDuration(3000 + _weatherRandom.Next(0, 2600)),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        BeginTime = ScaleWeatherDuration(_weatherRandom.Next(0, 1800))
                    });
                }
            }
        }

        private void AddWeatherConditionLabel(WeatherVisualPreset preset, double centerSize, double? temperatureCelsius)
        {
            var labelText = GetWeatherDisplayName(preset);
            if (string.IsNullOrWhiteSpace(labelText))
            {
                return;
            }

            const double labelPeakOpacity = 0.96;
            var temperatureText = FormatTemperatureLabel(temperatureCelsius);
            var hasTemperature = !string.IsNullOrWhiteSpace(temperatureText);
            var startDelay = GetWeatherConditionLabelStartDelay();
            var fadeInDuration = GetWeatherConditionLabelFadeInDuration();
            var fadeInEnd = startDelay + fadeInDuration;
            var holdStart = fadeInEnd + TimeSpan.FromMilliseconds(2700);
            var fadeOutEnd = holdStart + TimeSpan.FromMilliseconds(700);

            var panel = new Border
            {
                Background = GetCachedBrush(Color.FromArgb(128, 18, 24, 36)),
                BorderBrush = GetCachedBrush(Color.FromArgb(188, 255, 201, 98)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = hasTemperature ? new Thickness(8, 3, 8, 4) : new Thickness(8, 2, 8, 2),
                Opacity = 0.0,
                IsHitTestVisible = false
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            content.Children.Add(new TextBlock
            {
                Text = labelText,
                Foreground = GetCachedBrush(Color.FromArgb(236, 255, 214, 126)),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = Math.Clamp(centerSize * 0.092, 9.0, 13.0),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            if (hasTemperature)
            {
                content.Children.Add(new TextBlock
                {
                    Text = temperatureText,
                    Margin = new Thickness(0, 1, 0, 0),
                    Foreground = GetCachedBrush(Color.FromArgb(242, 255, 235, 166)),
                    FontFamily = new FontFamily("Segoe UI Semibold"),
                    FontSize = Math.Clamp(centerSize * 0.108, 10.0, 15.0),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }

            panel.Child = content;
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var labelWidth = Math.Max(70.0, panel.DesiredSize.Width);
            var labelHeight = Math.Max(hasTemperature ? 34.0 : 22.0, panel.DesiredSize.Height);
            panel.Width = labelWidth;
            panel.Height = labelHeight;

            Canvas.SetLeft(panel, _centerX - (labelWidth / 2.0));
            var labelCenterY = _centerY - centerSize * (hasTemperature ? 0.24 : 0.20);
            var labelTop = labelCenterY - (labelHeight / 2.0);
            var minTop = _centerY - (centerSize * 0.46);
            var maxTop = _centerY - (centerSize * 0.10) - labelHeight;
            Canvas.SetTop(panel, Math.Max(minTop, Math.Min(labelTop, maxTop)));
            RegisterWeatherVisual(panel, WeatherConditionLabelZIndex);

            var fadeOut = new DoubleAnimationUsingKeyFrames
            {
                FillBehavior = FillBehavior.Stop
            };
            fadeOut.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fadeOut.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(startDelay)));
            fadeOut.KeyFrames.Add(new EasingDoubleKeyFrame(labelPeakOpacity, KeyTime.FromTimeSpan(fadeInEnd), WeatherLabelFadeInEase));
            fadeOut.KeyFrames.Add(new DiscreteDoubleKeyFrame(labelPeakOpacity, KeyTime.FromTimeSpan(holdStart)));
            fadeOut.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(fadeOutEnd), WeatherLabelFadeOutEase));
            fadeOut.Completed += (_, __) => RemoveWeatherVisual(panel);
            panel.BeginAnimation(OpacityProperty, fadeOut);
        }

        private TimeSpan GetWeatherConditionLabelStartDelay()
        {
            switch (_openAnimationStyle)
            {
                case "ArcCascade":
                    return TimeSpan.FromMilliseconds(170 + 40);
                case "MeteorDrop":
                    return TimeSpan.FromMilliseconds(220 + 40);
                case "CenterUnfold":
                    return TimeSpan.FromMilliseconds(180 + 40);
                case "NovaBloom":
                    return TimeSpan.FromMilliseconds(240 + 40);
                case "VelvetCurtain":
                    return TimeSpan.FromMilliseconds(220 + 40);
                case "SoftFade":
                    return TimeSpan.FromMilliseconds(200 + 40);
                case "OdakKaskadi":
                    return TimeSpan.FromMilliseconds(1000 + 40);
                case "None":
                    return TimeSpan.Zero;
                default:
                    return TimeSpan.FromMilliseconds(286 + 40);
            }
        }

        private static TimeSpan GetWeatherConditionLabelFadeInDuration()
        {
            return TimeSpan.FromMilliseconds(350);
        }

        private static string GetWeatherDisplayName(WeatherVisualPreset preset)
        {
            switch (preset)
            {
                case WeatherVisualPreset.ClearSkyDay:
                case WeatherVisualPreset.ClearSkyNight:
                    return "Acik Hava";
                case WeatherVisualPreset.MostlyClear:
                    return "Az Bulutlu";
                case WeatherVisualPreset.PartlyCloudy:
                    return "Parcali Bulutlu";
                case WeatherVisualPreset.Overcast:
                    return "Cok Bulutlu";
                case WeatherVisualPreset.Fog:
                    return "Sisli";
                case WeatherVisualPreset.Drizzle:
                    return "Cisenti";
                case WeatherVisualPreset.FreezingDrizzle:
                    return "Donan Cisenti";
                case WeatherVisualPreset.Rain:
                    return "Yagmurlu";
                case WeatherVisualPreset.FreezingRain:
                    return "Donan Yagmur";
                case WeatherVisualPreset.Snow:
                    return "Karli";
                case WeatherVisualPreset.SnowGrains:
                    return "Ince Kar";
                case WeatherVisualPreset.RainShowers:
                    return "Saganak Yagmur";
                case WeatherVisualPreset.SnowShowers:
                    return "Kar Saganagi";
                case WeatherVisualPreset.Thunderstorm:
                    return "Firtina";
                case WeatherVisualPreset.ThunderstormHail:
                    return "Dolu Firtinasi";
                default:
                    return "Parcali Bulutlu";
            }
        }

        private static string? FormatTemperatureLabel(double? temperatureCelsius)
        {
            if (!temperatureCelsius.HasValue || double.IsNaN(temperatureCelsius.Value) || double.IsInfinity(temperatureCelsius.Value))
            {
                return null;
            }

            return $"{Math.Round(temperatureCelsius.Value):0}\u00B0C";
        }

        private void AddSupplementaryClouds(double centerSize, int frontClouds, int rearClouds, double density)
        {
            if (frontClouds <= 0 && rearClouds <= 0)
            {
                return;
            }

            AddSupplementaryCloudLayer(centerSize, rearClouds, isRearLayer: true, density);
            AddSupplementaryCloudLayer(centerSize, frontClouds, isRearLayer: false, density);
        }

        private void AddSupplementaryCloudLayer(double centerSize, int cloudCount, bool isRearLayer, double density)
        {
            if (cloudCount <= 0)
            {
                return;
            }

            var baseOrbit = isRearLayer ? centerSize * 0.62 : centerSize * 0.52;
            var baseOpacity = isRearLayer ? 0.42 : 0.62;
            var opacityScale = isRearLayer ? 0.68 : 0.78;
            var lightweightCloud = ShouldUseLightweightCloudEffects();

            for (var i = 0; i < cloudCount; i++)
            {
                var widthScale = isRearLayer ? 0.62 : 0.74;
                var width = Math.Clamp(centerSize * widthScale * (0.84 + (_weatherRandom.NextDouble() * 0.38)), 86.0, 220.0);
                var height = ResolveWeatherCloudHeight(width, centerSize);
                var cloud = CreateWeatherCloudVisual(width, height, lightweightCloud);

                var angleStep = 270.0 / Math.Max(1, cloudCount + 1);
                var baseAngle = -205.0 + (i + 1) * angleStep + (_weatherRandom.NextDouble() * 22.0 - 11.0);
                var radians = ToRadians(baseAngle);
                var startX = Math.Cos(radians) * baseOrbit;
                var startY = Math.Sin(radians) * (centerSize * 0.58);

                var driftX = centerSize * (isRearLayer ? 0.12 : 0.18) * (i % 2 == 0 ? 1.0 : -1.0);
                var driftY = centerSize * (isRearLayer ? 0.03 : 0.05) * (_weatherRandom.NextDouble() - 0.5);

                var peakOpacity = Math.Max(0.18, Math.Min(0.94, (baseOpacity + _weatherRandom.NextDouble() * 0.18) * density * opacityScale));
                var zIndex = isRearLayer ? WeatherCloudRearZIndex - 4 : WeatherCloudZIndex - 3;

                BeginWeatherCloudTrack(
                    cloud,
                    startOffsetX: startX,
                    startOffsetY: startY,
                    endOffsetX: startX + driftX,
                    endOffsetY: startY + driftY,
                    peakOpacity: peakOpacity,
                    zIndex: zIndex);
            }
        }

        private void AddFogOverlay(double centerSize, bool dense)
        {
            var layer = CreateWeatherLayer(centerSize * 2.3, centerSize * 0.95, _centerX - centerSize * 1.15, _centerY - centerSize * 0.40, WeatherFogZIndex, dense ? 0.90 : 0.75);
            var count = Math.Min(dense ? 5 : 3, GetWeatherParticleLimit(lowTierLimit: 3, midTierLimit: 4, highTierLimit: 5));
            for (var i = 0; i < count; i++)
            {
                var w = centerSize * (0.95 + _weatherRandom.NextDouble() * 0.45);
                var h = centerSize * (0.14 + _weatherRandom.NextDouble() * 0.08);
                var band = new Border
                {
                    Width = w,
                    Height = h,
                    CornerRadius = new CornerRadius(h),
                    Background = GetCachedBrush(Color.FromArgb((byte)(dense ? 132 : 112), 232, 238, 246)),
                    Opacity = dense ? 0.55 : 0.42,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(band, (_weatherRandom.NextDouble() - 0.25) * Math.Max(1.0, layer.Width - w));
                Canvas.SetTop(band, _weatherRandom.NextDouble() * Math.Max(1.0, layer.Height - h));
                layer.Children.Add(band);

                var move = new TranslateTransform();
                band.RenderTransform = move;
                move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
                {
                    From = -centerSize * 0.15,
                    To = centerSize * 0.15,
                    Duration = ScaleWeatherDuration(5600 + _weatherRandom.Next(0, 2200)),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                });
            }
        }

        private void AddRainOverlay(
            double centerSize,
            int dropCount,
            bool fast,
            bool icyTone = false,
            double lengthScale = 1.0,
            double opacityTarget = 0.82,
            double windScale = -0.10,
            double speedMultiplier = 1.0)
        {
            dropCount = CapRainParticleCount(dropCount);
            var layer = CreateWeatherLayer(centerSize * 1.9, centerSize * 1.5, _centerX - centerSize * 0.95, _centerY - centerSize * 0.54, WeatherPrecipitationZIndex, icyTone ? 0.94 : 0.88);
            var dropColor = icyTone ? Color.FromArgb(210, 190, 218, 255) : Color.FromArgb(198, 164, 192, 236);
            var minDurationMs = fast ? 520.0 : 860.0;
            var maxDurationMs = fast ? 940.0 : 1320.0;
            var normalizedLengthScale = Math.Max(0.45, Math.Min(1.6, lengthScale));
            var normalizedOpacityTarget = Math.Max(0.32, Math.Min(0.98, opacityTarget));
            var normalizedSpeed = Math.Max(0.45, Math.Min(2.2, speedMultiplier));
            var normalizedWind = Math.Max(-0.30, Math.Min(0.30, windScale));
            var rainSkewAngle = Math.Max(-14.0, Math.Min(14.0, normalizedWind * -110.0));
            if (Math.Abs(rainSkewAngle) > 0.1)
            {
                layer.RenderTransform = new SkewTransform(rainSkewAngle, 0.0, layer.Width * 0.5, 0.0);
            }

            for (var i = 0; i < dropCount; i++)
            {
                var length = Math.Clamp(centerSize * (0.07 + _weatherRandom.NextDouble() * 0.08) * normalizedLengthScale, 5.0, 36.0);
                var width = 1.0 + _weatherRandom.NextDouble() * (icyTone ? 1.4 : 0.9);
                var drop = new Rectangle
                {
                    Width = width,
                    Height = length,
                    RadiusX = width * 0.55,
                    RadiusY = width * 0.55,
                    Fill = GetCachedBrush(dropColor),
                    Opacity = Math.Max(
                        0.12,
                        Math.Min(
                            1.0,
                            normalizedOpacityTarget * (icyTone ? 0.72 : 0.46) + (_weatherRandom.NextDouble() * (icyTone ? 0.22 : 0.26)))),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(drop, _weatherRandom.NextDouble() * Math.Max(1.0, layer.Width - width));
                Canvas.SetTop(drop, -length - (_weatherRandom.NextDouble() * layer.Height * 0.24));
                layer.Children.Add(drop);

                var duration = ScaleWeatherDuration((minDurationMs + _weatherRandom.NextDouble() * (maxDurationMs - minDurationMs)) / normalizedSpeed);
                var begin = ScaleWeatherDuration(_weatherRandom.Next(0, (int)Math.Max(220.0, WeatherLoopDuration.TotalMilliseconds)));
                var move = new TranslateTransform();
                drop.RenderTransform = move;
                move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
                {
                    From = 0.0,
                    To = layer.Height + length + centerSize * 0.10,
                    Duration = duration,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = begin
                });
            }
        }

        private void AddSnowOverlay(double centerSize, int flakeCount, bool grainMode)
        {
            flakeCount = CapSnowParticleCount(flakeCount);
            var layer = CreateWeatherLayer(centerSize * 1.9, centerSize * 1.5, _centerX - centerSize * 0.95, _centerY - centerSize * 0.54, WeatherPrecipitationZIndex, grainMode ? 0.92 : 0.86);
            for (var i = 0; i < flakeCount; i++)
            {
                var size = grainMode ? 1.6 + _weatherRandom.NextDouble() * 1.6 : 2.2 + _weatherRandom.NextDouble() * 3.6;
                var flake = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = GetCachedBrush(Color.FromArgb((byte)(grainMode ? 214 : 232), 246, 249, 255)),
                    Opacity = grainMode
                        ? (0.44 + _weatherRandom.NextDouble() * 0.28)
                        : (0.54 + _weatherRandom.NextDouble() * 0.34),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(flake, _weatherRandom.NextDouble() * Math.Max(1.0, layer.Width - size));
                Canvas.SetTop(flake, -size - (_weatherRandom.NextDouble() * layer.Height * 0.22));
                layer.Children.Add(flake);

                var duration = ScaleWeatherDuration(grainMode ? 1300 + _weatherRandom.Next(0, 900) : 2200 + _weatherRandom.Next(0, 1700));
                var begin = ScaleWeatherDuration(_weatherRandom.Next(0, (int)Math.Max(220.0, WeatherLoopDuration.TotalMilliseconds)));
                var move = new TranslateTransform();
                flake.RenderTransform = move;
                move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
                {
                    From = 0.0,
                    To = layer.Height + centerSize * 0.08,
                    Duration = duration,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = begin
                });
                move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
                {
                    From = -centerSize * 0.08,
                    To = centerSize * 0.08,
                    Duration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.65),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = begin
                });
            }
        }

        private void AddLightningOverlay(double centerSize)
        {
            var diameter = centerSize * 2.12;
            var layer = CreateWeatherLayer(
                diameter,
                diameter,
                _centerX - diameter / 2.0,
                _centerY - diameter / 2.0,
                WeatherLightningZIndex);

            layer.Clip = new EllipseGeometry(
                new Point(diameter / 2.0, diameter / 2.0),
                centerSize * 0.68,
                centerSize * 0.68);

            var flash = new Ellipse
            {
                Width = diameter * 1.02,
                Height = diameter * 1.02,
                Fill = GetCachedBrush(Color.FromArgb(146, 255, 248, 226)),
                Opacity = 0.0,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(flash, (diameter - flash.Width) / 2.0);
            Canvas.SetTop(flash, (diameter - flash.Height) / 2.0);

            var coreFlash = new Ellipse
            {
                Width = centerSize * 1.28,
                Height = centerSize * 1.28,
                Fill = GetCachedBrush(Color.FromArgb(196, 255, 246, 206)),
                Opacity = 0.0,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(coreFlash, (diameter - coreFlash.Width) / 2.0);
            Canvas.SetTop(coreFlash, (diameter - coreFlash.Height) / 2.0);

            layer.Children.Add(flash);
            layer.Children.Add(coreFlash);

            flash.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = RepeatBehavior.Forever,
                KeyFrames =
                {
                    new DiscreteDoubleKeyFrame(0.0, ScaleWeatherKeyTime(0)),
                    new DiscreteDoubleKeyFrame(0.0, ScaleWeatherKeyTime(3000)),
                    new DiscreteDoubleKeyFrame(0.46, ScaleWeatherKeyTime(3060)),
                    new DiscreteDoubleKeyFrame(0.08, ScaleWeatherKeyTime(3140)),
                    new DiscreteDoubleKeyFrame(0.38, ScaleWeatherKeyTime(3220)),
                    new DiscreteDoubleKeyFrame(0.0, ScaleWeatherKeyTime(3360)),
                    new DiscreteDoubleKeyFrame(0.0, ScaleWeatherKeyTime(5200))
                }
            });

            coreFlash.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = RepeatBehavior.Forever,
                KeyFrames =
                {
                    new DiscreteDoubleKeyFrame(0.0, ScaleWeatherKeyTime(0)),
                    new DiscreteDoubleKeyFrame(0.0, ScaleWeatherKeyTime(3020)),
                    new DiscreteDoubleKeyFrame(0.52, ScaleWeatherKeyTime(3070)),
                    new DiscreteDoubleKeyFrame(0.06, ScaleWeatherKeyTime(3145)),
                    new DiscreteDoubleKeyFrame(0.42, ScaleWeatherKeyTime(3230)),
                    new DiscreteDoubleKeyFrame(0.0, ScaleWeatherKeyTime(3360)),
                    new DiscreteDoubleKeyFrame(0.0, ScaleWeatherKeyTime(5200))
                }
            });
        }

        private void AddHailOverlay(double centerSize, int pelletCount)
        {
            pelletCount = CapHailParticleCount(pelletCount);
            var layer = CreateWeatherLayer(centerSize * 1.9, centerSize * 1.5, _centerX - centerSize * 0.95, _centerY - centerSize * 0.54, WeatherPrecipitationZIndex + 1, 0.94);
            for (var i = 0; i < pelletCount; i++)
            {
                var size = 2.0 + _weatherRandom.NextDouble() * 2.0;
                var hail = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = GetCachedBrush(Color.FromArgb(228, 236, 244, 255)),
                    Opacity = 0.58 + (_weatherRandom.NextDouble() * 0.30),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(hail, _weatherRandom.NextDouble() * Math.Max(1.0, layer.Width - size));
                Canvas.SetTop(hail, -size - (_weatherRandom.NextDouble() * layer.Height * 0.24));
                layer.Children.Add(hail);

                var duration = ScaleWeatherDuration(520 + _weatherRandom.Next(0, 340));
                var begin = ScaleWeatherDuration(_weatherRandom.Next(0, (int)Math.Max(220.0, WeatherLoopDuration.TotalMilliseconds)));
                var move = new TranslateTransform();
                hail.RenderTransform = move;
                move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
                {
                    From = 0.0,
                    To = layer.Height + size + centerSize * 0.10,
                    Duration = duration,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = begin
                });
            }
        }

        private Canvas CreateWeatherLayer(double width, double height, double left, double top, int zIndex, double opacity = 1.0)
        {
            var layer = new Canvas
            {
                Width = width,
                Height = height,
                Opacity = opacity,
                ClipToBounds = true,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(layer, left);
            Canvas.SetTop(layer, top);
            RegisterWeatherVisual(layer, zIndex);
            return layer;
        }

        private void RegisterWeatherVisual(FrameworkElement visual, int zIndex)
        {
            if (RootCanvas == null)
            {
                return;
            }

            if (ReferenceEquals(visual.Parent, RootCanvas))
            {
                Panel.SetZIndex(visual, zIndex);
                _weatherVisualElements.Add(visual);
                return;
            }

            if (visual.Parent is Panel existingParent)
            {
                existingParent.Children.Remove(visual);
            }

            Panel.SetZIndex(visual, zIndex);
            RootCanvas.Children.Add(visual);
            _weatherVisualElements.Add(visual);
        }

        private void RemoveWeatherVisual(FrameworkElement? visual)
        {
            if (visual == null)
            {
                return;
            }

            visual.BeginAnimation(OpacityProperty, null);
            StopVisualTreeAnimations(visual);
            StopTransformAnimations(visual.RenderTransform);

            if (visual.Parent is Panel parent)
            {
                parent.Children.Remove(visual);
            }

            _weatherVisualElements.Remove(visual);
        }

        private static void StopVisualTreeAnimations(DependencyObject root)
        {
            if (root is UIElement element)
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
            }

            if (root is FrameworkElement frameworkElement)
            {
                StopTransformAnimations(frameworkElement.RenderTransform);
            }

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < childCount; index++)
            {
                StopVisualTreeAnimations(VisualTreeHelper.GetChild(root, index));
            }
        }

        private static void StopTransformAnimations(Transform? transform)
        {
            if (transform == null)
            {
                return;
            }

            switch (transform)
            {
                case TranslateTransform translate:
                    translate.BeginAnimation(TranslateTransform.XProperty, null);
                    translate.BeginAnimation(TranslateTransform.YProperty, null);
                    break;
                case ScaleTransform scale:
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    break;
                case RotateTransform rotate:
                    rotate.BeginAnimation(RotateTransform.AngleProperty, null);
                    break;
                case SkewTransform skew:
                    skew.BeginAnimation(SkewTransform.AngleXProperty, null);
                    skew.BeginAnimation(SkewTransform.AngleYProperty, null);
                    break;
                case TransformGroup group:
                    foreach (var child in group.Children)
                    {
                        StopTransformAnimations(child);
                    }
                    break;
            }
        }
    }
}
