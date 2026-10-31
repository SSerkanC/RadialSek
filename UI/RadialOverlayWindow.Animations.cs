using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RadialSek.Models;
using ShapePath = System.Windows.Shapes.Path;
using WpfPoint = System.Windows.Point;

namespace RadialSek.UI
{
    public partial class RadialOverlayWindow
    {
        private static void ApplyDesiredFrameRate(Timeline timeline, int fps)
        {
            if (timeline == null)
            {
                return;
            }

            // Do not throttle animation framerate below 60 FPS; let WPF render at native monitor refresh rate (60Hz/144Hz+) for maximum smoothness.
            if (fps >= 60)
            {
                Timeline.SetDesiredFrameRate(timeline, fps);
            }
        }

        private static void StartCenterAccentRingAmbientOpacityAnimation(Ellipse ring)
        {
            var animation = new DoubleAnimation
            {
                From = 0.62,
                To = 0.96,
                Duration = TimeSpan.FromMilliseconds(2200),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            ApplyDesiredFrameRate(animation, RingAmbientAnimationFps);
            ring.BeginAnimation(OpacityProperty, animation);
        }

        private static void StartOuterAccentRingAmbientOpacityAnimation(Ellipse ring)
        {
            var animation = new DoubleAnimation
            {
                From = 0.72,
                To = 0.98,
                Duration = TimeSpan.FromMilliseconds(2800),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            ApplyDesiredFrameRate(animation, RingAmbientAnimationFps);
            ring.BeginAnimation(OpacityProperty, animation);
        }

        private void PlayOpenAnimation()
        {
            if (_openAnimationStyle == "MeteorDrop")
            {
                PlayMeteorDropOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "ArcCascade")
            {
                PlayArcCascadeOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "NovaBloom")
            {
                PlayNovaBloomOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "VelvetCurtain")
            {
                PlayVelvetCurtainOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "CenterUnfold")
            {
                PlayCenterUnfoldOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "OdakKaskadi")
            {
                PlayOdakKaskadiOpenAnimation();
                return;
            }

            if (_openAnimationStyle == "SoftFade")
            {
                PlaySoftFadeOpenAnimation();
                return;
            }

            var fadeEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            var settleEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.28 };

            var opacityAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(176),
                EasingFunction = fadeEase
            };
            BeginAnimation(OpacityProperty, opacityAnimation);

            var scaleAnimation = new DoubleAnimationUsingKeyFrames();
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0.978, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.008, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)))
            {
                EasingFunction = settleEase
            });
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(270)))
            {
                EasingFunction = settleEase
            });
            RootScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnimation);
            RootScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnimation);

            var translateAnimation = new DoubleAnimationUsingKeyFrames();
            translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(14, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-1.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)))
            {
                EasingFunction = settleEase
            });
            translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(270)))
            {
                EasingFunction = settleEase
            });
            RootTranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, translateAnimation);

            foreach (var centerTarget in EnumerateCenterAnimationTargets())
            {
                centerTarget.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
                var centerTransforms = new TransformGroup();
                var centerScale = new ScaleTransform(0.94, 0.94);
                var centerTranslate = new TranslateTransform(0, 10);
                centerTransforms.Children.Add(centerScale);
                centerTransforms.Children.Add(centerTranslate);
                centerTarget.RenderTransform = centerTransforms;

                var centerScaleAnim = new DoubleAnimationUsingKeyFrames();
                centerScaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.95, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                centerScaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.015, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(188)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                centerScaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(286)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, centerScaleAnim);
                centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, centerScaleAnim);

                var centerTranslateAnim = new DoubleAnimationUsingKeyFrames();
                centerTranslateAnim.KeyFrames.Add(new EasingDoubleKeyFrame(12, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                centerTranslateAnim.KeyFrames.Add(new EasingDoubleKeyFrame(-1.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(188)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                centerTranslateAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(286)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                centerTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, centerTranslateAnim);
            }

            AnimateUtilityDockEntrance(0.0, 1.0, 0.95, 1.015, 188, 286);
        }

        private void PlayVelvetCurtainOpenAnimation()
        {
            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;
            AnimateCenterPanelEntrance(0.0, 1.0, 0.92, 1.0, 130, 170);

            var interactions = GetMainRingInteractions();

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var begin = TimeSpan.FromMilliseconds(30 + (i * 18));
                AnimateSegmentScaleEntrance(interaction.Segment, 0.18, 1.0, begin, 180, new CubicEase { EasingMode = EasingMode.EaseOut });
                AnimateIconScaleEntrance(interaction.IconHost, 0.7, 1.0, begin + TimeSpan.FromMilliseconds(25), 140, new QuadraticEase { EasingMode = EasingMode.EaseOut });
            }
        }

        private void PlayNovaBloomOpenAnimation()
        {
            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;
            AnimateCenterPanelEntrance(0.0, 1.0, 0.88, 1.03, 130, 170);

            var profile = GetLayoutProfile();
            var interactions = GetMainRingInteractions();
            var inwardDistance = Math.Max(24, (profile.InnerRadius - (profile.CenterSize / 2.0)) + 14);

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var radians = ToRadians((interaction.StartAngle + interaction.EndAngle) / 2.0);
                var offsetX = -Math.Cos(radians) * inwardDistance;
                var offsetY = -Math.Sin(radians) * inwardDistance;
                var begin = TimeSpan.FromMilliseconds(30 + (i * 12));

                AnimateSegmentEntrance(interaction.Segment, offsetX, offsetY, begin, 180, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 });
                AnimateIconEntrance(interaction.IconHost, offsetX * 1.12, offsetY * 1.12, begin + TimeSpan.FromMilliseconds(20), 160, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.20 });
            }
        }

        private void PlayArcCascadeOpenAnimation()
        {
            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;
            AnimateCenterPanelEntrance(0.0, 1.0, 0.97, 1.0, 100, 140);

            var profile = GetLayoutProfile();
            var interactions = GetMainRingInteractions();
            var sweepOffset = Math.Max(20, profile.OuterRadius * 0.08);
            var inwardDistance = Math.Max(16, (profile.InnerRadius - (profile.CenterSize / 2.0)) * 0.40);

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var radians = ToRadians((interaction.StartAngle + interaction.EndAngle) / 2.0);
                var tangentX = -Math.Sin(radians) * sweepOffset;
                var tangentY = Math.Cos(radians) * sweepOffset;
                var radialX = -Math.Cos(radians) * inwardDistance;
                var radialY = -Math.Sin(radians) * inwardDistance;
                var begin = TimeSpan.FromMilliseconds(25 + (i * 16));

                AnimateSegmentEntrance(interaction.Segment, tangentX + radialX, tangentY + radialY, begin, 170, new CubicEase { EasingMode = EasingMode.EaseOut });
                AnimateIconEntrance(interaction.IconHost, tangentX * 0.9 + radialX, tangentY * 0.9 + radialY, begin + TimeSpan.FromMilliseconds(20), 150, new QuadraticEase { EasingMode = EasingMode.EaseOut });
            }
        }

        private void PlayMeteorDropOpenAnimation()
        {
            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;
            AnimateCenterPanelEntrance(0.0, 1.0, 0.9, 1.02, 110, 160);

            var profile = GetLayoutProfile();
            var interactions = GetMainRingInteractions();
            var dropDistance = Math.Max(60, profile.OuterRadius * 0.30);

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var begin = TimeSpan.FromMilliseconds(25 + (i * 14));
                var sideDrift = ((i % 2 == 0) ? -1 : 1) * Math.Max(8, profile.OuterRadius * 0.03);

                AnimateSegmentEntrance(interaction.Segment, sideDrift, -dropDistance, begin, 190, new BounceEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Bounces = 1,
                    Bounciness = 1.3
                });

                AnimateIconEntrance(interaction.IconHost, sideDrift * 1.2, -(dropDistance + 16), begin + TimeSpan.FromMilliseconds(25), 170, new BackEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Amplitude = 0.20
                });
            }
        }

        private void PlayCenterUnfoldOpenAnimation()
        {
            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;

            AnimateCenterPanelEntrance(0.0, 1.0, 0.94, 1.0, 110, 150);

            var settleEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            var profile = GetLayoutProfile();
            var inwardDistance = Math.Max(20, (profile.InnerRadius - (profile.CenterSize / 2.0)) + 10);
            var interactions = GetMainRingInteractions();

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var middleAngle = (interaction.StartAngle + interaction.EndAngle) / 2.0;
                var radians = ToRadians(middleAngle);
                var offsetX = -Math.Cos(radians) * inwardDistance;
                var offsetY = -Math.Sin(radians) * inwardDistance;
                var begin = TimeSpan.FromMilliseconds(25 + (i * 14));

                AnimateSegmentEntrance(interaction.Segment, offsetX, offsetY, begin, 160, settleEase);
                AnimateIconEntrance(interaction.IconHost, offsetX * 1.05, offsetY * 1.05, begin + TimeSpan.FromMilliseconds(18), 140, settleEase);
            }
        }

        private void PlayOdakKaskadiOpenAnimation()
        {
            EnsureOdakKaskadiAnimationSettled();
            _odakKaskadiAnimatedSegments.Clear();
            _odakKaskadiIconFinalPositions.Clear();
            _isOdakKaskadiAnimationActive = true;

            Opacity = 1.0;
            RootScaleTransform.ScaleX = 1.0;
            RootScaleTransform.ScaleY = 1.0;
            RootTranslateTransform.Y = 0.0;

            var centerEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            foreach (var centerTarget in EnumerateCenterAnimationTargets())
            {
                centerTarget.BeginAnimation(OpacityProperty, null);
                centerTarget.Opacity = 0.0;
                centerTarget.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
                centerTarget.RenderTransform = new ScaleTransform(0.0, 0.0);

                centerTarget.BeginAnimation(OpacityProperty, new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(180),
                    EasingFunction = centerEase
                });

                if (centerTarget.RenderTransform is ScaleTransform centerScale)
                {
                    centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation
                    {
                        From = 0.0,
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(180),
                        EasingFunction = centerEase
                    });
                    centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation
                    {
                        From = 0.0,
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(180),
                        EasingFunction = centerEase
                    });
                }
            }

            AnimateUtilityDockEntrance(0.0, 1.0, 0.0, 1.0, 180, 180);

            var profile = GetLayoutProfile();
            if (_enableGradientRingAnimations)
            {
                PlayOdakKaskadiCenterAccentRingSequence();
                PlayOdakKaskadiOuterAccentRingSequence(profile);
            }

            var interactions = GetMainRingInteractions();
            if (interactions.Count == 0)
            {
                _isOdakKaskadiAnimationActive = false;
                return;
            }

            var centerRadius = profile.CenterSize * 0.5;
            var inwardDistance = Math.Max(20.0, (profile.InnerRadius - centerRadius) * 0.65 + 8.0);
            const double cascadeStartMs = 0.0;
            const double cascadeWindowMs = 180.0;
            var segmentDurationMs = interactions.Count <= 1 ? cascadeWindowMs : 140.0;
            var stepMs = interactions.Count <= 1
                ? 0.0
                : Math.Max(0.0, (cascadeWindowMs - segmentDurationMs) / (interactions.Count - 1));
            var segmentEase = new CubicEase { EasingMode = EasingMode.EaseOut };

            for (var i = 0; i < interactions.Count; i++)
            {
                var interaction = interactions[i];
                var angle = ToRadians((interaction.StartAngle + interaction.EndAngle) * 0.5);
                var offsetX = -Math.Cos(angle) * inwardDistance;
                var offsetY = -Math.Sin(angle) * inwardDistance;
                var begin = TimeSpan.FromMilliseconds(cascadeStartMs + (stepMs * i));

                _odakKaskadiAnimatedSegments.Add(interaction.Segment);
                _odakKaskadiIconFinalPositions[interaction.IconHost] = new WpfPoint(
                    Canvas.GetLeft(interaction.IconHost),
                    Canvas.GetTop(interaction.IconHost));

                AnimateSegmentEntrance(
                    interaction.Segment,
                    offsetX,
                    offsetY,
                    begin,
                    (int)Math.Round(segmentDurationMs),
                    segmentEase);
                AnimateIconEntrance(
                    interaction.IconHost,
                    offsetX * 1.04,
                    offsetY * 1.04,
                    begin,
                    (int)Math.Round(segmentDurationMs),
                    segmentEase);
            }

            var ringTailMs = _enableGradientRingAnimations
                ? OdakKaskadiCenterAccentReturnMs + OdakKaskadiFinalizeBufferMs
                : 0.0;
            var finalizeMs = Math.Max(cascadeStartMs + cascadeWindowMs + 40.0, ringTailMs);
            _openAnimationFinalizeTimer.Stop();
            _openAnimationFinalizeTimer.Interval = TimeSpan.FromMilliseconds(finalizeMs);
            _openAnimationFinalizeTimer.Start();
        }

        private void PlayOdakKaskadiCenterAccentRingSequence()
        {
            if (_centerAccentRing == null)
            {
                return;
            }

            var ring = _centerAccentRing;
            var gradientStroke = _centerAccentRingBaseStroke ?? ring.Stroke;
            _centerAccentRingBaseStroke ??= gradientStroke;
            if (_centerAccentRingBaseOpacity <= 0.0)
            {
                _centerAccentRingBaseOpacity = ring.Opacity > 0 ? ring.Opacity : 0.92;
            }

            if (_centerAccentRingBaseStrokeThickness <= 0.0)
            {
                _centerAccentRingBaseStrokeThickness = ring.StrokeThickness > 0.0 ? ring.StrokeThickness : Math.Max(2.2, ring.Width * 0.018);
            }

            var baseOpacity = _centerAccentRingBaseOpacity;
            var baseStrokeThickness = _centerAccentRingBaseStrokeThickness;
            var peakStrokeThickness = baseStrokeThickness * 4.0;
            var thicknessHoldMs = OdakKaskadiCenterAccentHoldMs;
            var peakReachMs = OdakKaskadiCenterAccentPeakMs;
            var returnToBaseMs = OdakKaskadiCenterAccentReturnMs;
            var solidOpacity = Math.Min(1.0, baseOpacity + 0.08);
            var solidColor = BlendColors(_theme.CenterBorderColor, _theme.SegmentActiveColor, 0.74);
            var solidStroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(236, solidColor.R, solidColor.G, solidColor.B));

            ClearCenterAccentRingSolidOverlay();
            ring.BeginAnimation(Shape.StrokeProperty, null);
            ring.BeginAnimation(OpacityProperty, null);
            ring.BeginAnimation(Shape.StrokeThicknessProperty, null);
            ring.Stroke = gradientStroke;
            ring.StrokeThickness = baseStrokeThickness;
            ring.Opacity = 0.0;

            var opacitySequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(opacitySequence, RingSequenceAnimationFps);
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = null
            });

            var thicknessSequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(thicknessSequence, RingSequenceAnimationFps);
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(peakStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });

            opacitySequence.Completed += (_, __) =>
            {
                if (!ReferenceEquals(_centerAccentRing, ring))
                {
                    return;
                }

                ClearCenterAccentRingSolidOverlay();
                ring.BeginAnimation(Shape.StrokeProperty, null);
                ring.Stroke = _centerAccentRingBaseStroke ?? gradientStroke;
                ring.BeginAnimation(OpacityProperty, null);
                ring.Opacity = _centerAccentRingBaseOpacity;
                ring.BeginAnimation(Shape.StrokeThicknessProperty, null);
                ring.StrokeThickness = _centerAccentRingBaseStrokeThickness > 0.0 ? _centerAccentRingBaseStrokeThickness : baseStrokeThickness;
                StartCenterAccentRingAmbientOpacityAnimation(ring);
            };

            ring.BeginAnimation(OpacityProperty, opacitySequence);
            ring.BeginAnimation(Shape.StrokeThicknessProperty, thicknessSequence);

            var solidOverlay = new Ellipse
            {
                Width = ring.Width,
                Height = ring.Height,
                Stroke = solidStroke,
                StrokeThickness = baseStrokeThickness,
                Opacity = 0.0,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };

            Canvas.SetLeft(solidOverlay, Canvas.GetLeft(ring));
            Canvas.SetTop(solidOverlay, Canvas.GetTop(ring));
            var ringIndex = RootCanvas.Children.IndexOf(ring);
            if (ringIndex >= 0)
            {
                RootCanvas.Children.Insert(ringIndex + 1, solidOverlay);
            }
            else
            {
                RootCanvas.Children.Add(solidOverlay);
            }

            _centerAccentRingSolidOverlay = solidOverlay;

            var solidOpacitySequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(solidOpacitySequence, RingSequenceAnimationFps);
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs))));
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(solidOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(solidOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = null
            });
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });
            solidOpacitySequence.Completed += (_, __) =>
            {
                if (!ReferenceEquals(_centerAccentRingSolidOverlay, solidOverlay))
                {
                    return;
                }

                ClearCenterAccentRingSolidOverlay();
            };

            var solidThicknessSequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(solidThicknessSequence, RingSequenceAnimationFps);
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(peakStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });

            solidOverlay.BeginAnimation(OpacityProperty, solidOpacitySequence);
            solidOverlay.BeginAnimation(Shape.StrokeThicknessProperty, solidThicknessSequence);
        }

        private void PlayOdakKaskadiOuterAccentRingSequence(LayoutProfile profile)
        {
            if (_outerAccentRing == null)
            {
                return;
            }

            var ring = _outerAccentRing;
            var gradientStroke = _outerAccentRingBaseStroke ?? ring.Stroke;
            _outerAccentRingBaseStroke ??= gradientStroke;
            if (_outerAccentRingBaseOpacity <= 0.0)
            {
                _outerAccentRingBaseOpacity = ring.Opacity > 0 ? ring.Opacity : 0.94;
            }

            if (_outerAccentRingBaseStrokeThickness <= 0.0)
            {
                _outerAccentRingBaseStrokeThickness = ring.StrokeThickness > 0.0 ? ring.StrokeThickness : Math.Max(3.2, ring.Width * 0.018);
            }

            var baseOpacity = _outerAccentRingBaseOpacity;
            var baseStrokeThickness = _outerAccentRingBaseStrokeThickness;
            var segmentBandThickness = Math.Max(0.0, profile.OuterRadius - profile.InnerRadius);
            var peakStrokeThickness = baseStrokeThickness + segmentBandThickness;
            var thicknessHoldMs = OdakKaskadiCenterAccentHoldMs;
            var peakReachMs = OdakKaskadiCenterAccentPeakMs;
            var returnToBaseMs = OdakKaskadiCenterAccentReturnMs;
            var solidOpacity = Math.Min(1.0, baseOpacity + 0.06);
            var solidColor = BlendColors(_theme.SegmentActiveColor, _theme.TitleColor, 0.58);
            var solidStroke = GetCachedBrush(System.Windows.Media.Color.FromArgb(228, solidColor.R, solidColor.G, solidColor.B));

            ClearOuterAccentRingSolidOverlay();
            ring.BeginAnimation(Shape.StrokeProperty, null);
            ring.BeginAnimation(OpacityProperty, null);
            ring.BeginAnimation(Shape.StrokeThicknessProperty, null);
            ring.Stroke = gradientStroke;
            ring.StrokeThickness = baseStrokeThickness;
            ring.Opacity = 0.0;

            var opacitySequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(opacitySequence, RingSequenceAnimationFps);
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            opacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = null
            });

            var thicknessSequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(thicknessSequence, RingSequenceAnimationFps);
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(peakStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            thicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });

            opacitySequence.Completed += (_, __) =>
            {
                if (!ReferenceEquals(_outerAccentRing, ring))
                {
                    return;
                }

                ClearOuterAccentRingSolidOverlay();
                ring.BeginAnimation(Shape.StrokeProperty, null);
                ring.Stroke = _outerAccentRingBaseStroke ?? gradientStroke;
                ring.BeginAnimation(OpacityProperty, null);
                ring.Opacity = _outerAccentRingBaseOpacity;
                ring.BeginAnimation(Shape.StrokeThicknessProperty, null);
                ring.StrokeThickness = _outerAccentRingBaseStrokeThickness > 0.0 ? _outerAccentRingBaseStrokeThickness : baseStrokeThickness;
                StartOuterAccentRingAmbientOpacityAnimation(ring);
            };

            ring.BeginAnimation(OpacityProperty, opacitySequence);
            ring.BeginAnimation(Shape.StrokeThicknessProperty, thicknessSequence);

            var solidOverlay = new Ellipse
            {
                Width = ring.Width,
                Height = ring.Height,
                Stroke = solidStroke,
                StrokeThickness = baseStrokeThickness,
                Opacity = 0.0,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };

            Canvas.SetLeft(solidOverlay, Canvas.GetLeft(ring));
            Canvas.SetTop(solidOverlay, Canvas.GetTop(ring));
            var ringIndex = RootCanvas.Children.IndexOf(ring);
            if (ringIndex >= 0)
            {
                RootCanvas.Children.Insert(ringIndex + 1, solidOverlay);
            }
            else
            {
                RootCanvas.Children.Add(solidOverlay);
            }

            _outerAccentRingSolidOverlay = solidOverlay;

            var solidOpacitySequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(solidOpacitySequence, RingSequenceAnimationFps);
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs))));
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(solidOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(solidOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = null
            });
            solidOpacitySequence.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });
            solidOpacitySequence.Completed += (_, __) =>
            {
                if (!ReferenceEquals(_outerAccentRingSolidOverlay, solidOverlay))
                {
                    return;
                }

                ClearOuterAccentRingSolidOverlay();
            };

            var solidThicknessSequence = new DoubleAnimationUsingKeyFrames();
            ApplyDesiredFrameRate(solidThicknessSequence, RingSequenceAnimationFps);
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(thicknessHoldMs)))
            {
                EasingFunction = null
            });
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(peakStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(peakReachMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            solidThicknessSequence.KeyFrames.Add(new EasingDoubleKeyFrame(baseStrokeThickness, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(returnToBaseMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });

            solidOverlay.BeginAnimation(OpacityProperty, solidOpacitySequence);
            solidOverlay.BeginAnimation(Shape.StrokeThicknessProperty, solidThicknessSequence);
        }

        private void OnOpenAnimationFinalizeTimerTick(object? sender, EventArgs e)
        {
            _openAnimationFinalizeTimer.Stop();
            EnsureOdakKaskadiAnimationSettled();
        }

        private void EnsureOdakKaskadiAnimationSettled()
        {
            if (!_isOdakKaskadiAnimationActive &&
                _odakKaskadiAnimatedSegments.Count == 0 &&
                _odakKaskadiIconFinalPositions.Count == 0 &&
                _centerAccentRing == null &&
                _centerAccentRingSolidOverlay == null &&
                _outerAccentRing == null &&
                _outerAccentRingSolidOverlay == null)
            {
                return;
            }

            _openAnimationFinalizeTimer.Stop();

            foreach (var segment in _odakKaskadiAnimatedSegments)
            {
                segment.BeginAnimation(OpacityProperty, null);
                segment.Opacity = 1.0;

                if (segment.RenderTransform is TranslateTransform translate)
                {
                    translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
                    translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
                }

                segment.RenderTransform = Transform.Identity;
            }

            foreach (var iconPair in _odakKaskadiIconFinalPositions)
            {
                var iconHost = iconPair.Key;
                var finalPosition = iconPair.Value;
                iconHost.BeginAnimation(OpacityProperty, null);
                if (iconHost.RenderTransform is TranslateTransform translate)
                {
                    translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
                    translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
                }
                iconHost.RenderTransform = Transform.Identity;
                iconHost.Opacity = 1.0;
                Canvas.SetLeft(iconHost, finalPosition.X);
                Canvas.SetTop(iconHost, finalPosition.Y);
            }

            ClearCenterAccentRingSolidOverlay();
            if (_centerAccentRing != null)
            {
                _centerAccentRing.BeginAnimation(Shape.StrokeProperty, null);
                _centerAccentRing.BeginAnimation(OpacityProperty, null);
                _centerAccentRing.BeginAnimation(Shape.StrokeThicknessProperty, null);
                if (_centerAccentRingBaseStroke != null)
                {
                    _centerAccentRing.Stroke = _centerAccentRingBaseStroke;
                }

                var baseOpacity = _centerAccentRingBaseOpacity > 0 ? _centerAccentRingBaseOpacity : 0.92;
                _centerAccentRing.Opacity = baseOpacity;
                if (_centerAccentRingBaseStrokeThickness > 0.0)
                {
                    _centerAccentRing.StrokeThickness = _centerAccentRingBaseStrokeThickness;
                }
                if (_enableGradientRingAnimations)
                {
                    StartCenterAccentRingAmbientOpacityAnimation(_centerAccentRing);
                }
            }

            ClearOuterAccentRingSolidOverlay();
            if (_outerAccentRing != null)
            {
                _outerAccentRing.BeginAnimation(Shape.StrokeProperty, null);
                _outerAccentRing.BeginAnimation(OpacityProperty, null);
                _outerAccentRing.BeginAnimation(Shape.StrokeThicknessProperty, null);
                if (_outerAccentRingBaseStroke != null)
                {
                    _outerAccentRing.Stroke = _outerAccentRingBaseStroke;
                }

                var baseOpacity = _outerAccentRingBaseOpacity > 0 ? _outerAccentRingBaseOpacity : 0.94;
                _outerAccentRing.Opacity = baseOpacity;
                if (_outerAccentRingBaseStrokeThickness > 0.0)
                {
                    _outerAccentRing.StrokeThickness = _outerAccentRingBaseStrokeThickness;
                }
                if (_enableGradientRingAnimations)
                {
                    StartOuterAccentRingAmbientOpacityAnimation(_outerAccentRing);
                }
            }

            _odakKaskadiAnimatedSegments.Clear();
            _odakKaskadiIconFinalPositions.Clear();
            _isOdakKaskadiAnimationActive = false;
            RefreshCenterCountdownRingVisual();
        }

        private void ClearCenterAccentRingSolidOverlay()
        {
            if (_centerAccentRingSolidOverlay == null)
            {
                return;
            }

            var overlay = _centerAccentRingSolidOverlay;
            _centerAccentRingSolidOverlay = null;
            overlay.BeginAnimation(OpacityProperty, null);
            overlay.BeginAnimation(Shape.StrokeThicknessProperty, null);
            RootCanvas.Children.Remove(overlay);
        }

        private void ClearOuterAccentRingSolidOverlay()
        {
            if (_outerAccentRingSolidOverlay == null)
            {
                return;
            }

            var overlay = _outerAccentRingSolidOverlay;
            _outerAccentRingSolidOverlay = null;
            overlay.BeginAnimation(OpacityProperty, null);
            overlay.BeginAnimation(Shape.StrokeThicknessProperty, null);
            RootCanvas.Children.Remove(overlay);
        }

        private List<SegmentInteraction> GetMainRingInteractions()
        {
            return _segments
                .Where(segment => _segmentInteractions.ContainsKey(segment))
                .Select(segment => _segmentInteractions[segment])
                .OrderBy(interaction => interaction.ItemIndex)
                .ToList();
        }

        private void AnimateCenterPanelEntrance(double fromOpacity, double toOpacity, double fromScale, double peakScale, int fadeMs, int scaleMs)
        {
            var centerTargets = EnumerateCenterAnimationTargets().ToList();
            if (centerTargets.Count == 0)
            {
                AnimateUtilityDockEntrance(fromOpacity, toOpacity, fromScale, peakScale, fadeMs, scaleMs);
                return;
            }

            foreach (var centerTarget in centerTargets)
            {
                centerTarget.Opacity = fromOpacity;
                centerTarget.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
                centerTarget.RenderTransform = new ScaleTransform(fromScale, fromScale);

                centerTarget.BeginAnimation(OpacityProperty, new DoubleAnimation
                {
                    From = fromOpacity,
                    To = toOpacity,
                    Duration = TimeSpan.FromMilliseconds(fadeMs),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });

                if (centerTarget.RenderTransform is ScaleTransform centerScale)
                {
                    var scaleAnimation = new DoubleAnimationUsingKeyFrames();
                    scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(fromScale, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(peakScale, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scaleMs * 0.72)))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    });
                    scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scaleMs)))
                    {
                        EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 }
                    });
                    centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnimation);
                    centerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnimation);
                }
            }

            AnimateUtilityDockEntrance(fromOpacity, toOpacity, fromScale, peakScale, fadeMs, scaleMs);
        }

        private void AnimateUtilityDockEntrance(double fromOpacity, double toOpacity, double fromScale, double peakScale, int fadeMs, int scaleMs)
        {
            if (_utilityDockButton == null)
            {
                return;
            }

            _utilityDockButton.BeginAnimation(OpacityProperty, null);
            _utilityDockButton.Opacity = fromOpacity;
            _utilityDockButton.RenderTransformOrigin = new WpfPoint(0.5, 0.5);

            if (!(_utilityDockButton.RenderTransform is ScaleTransform buttonScale))
            {
                buttonScale = new ScaleTransform(fromScale, fromScale);
                _utilityDockButton.RenderTransform = buttonScale;
            }

            buttonScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            buttonScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
            buttonScale.ScaleX = fromScale;
            buttonScale.ScaleY = fromScale;

            _utilityDockButton.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = fromOpacity,
                To = toOpacity,
                Duration = TimeSpan.FromMilliseconds(fadeMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            var scaleAnimation = new DoubleAnimationUsingKeyFrames();
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(fromScale, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(peakScale, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scaleMs * 0.72)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(scaleMs)))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 }
            });
            buttonScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnimation);
            buttonScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        private static void AnimateSegmentEntrance(UIElement element, double offsetX, double offsetY, TimeSpan beginTime, int durationMs, IEasingFunction easing)
        {
            element.Opacity = 0.0;
            element.RenderTransform = new TranslateTransform(offsetX, offsetY);

            element.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(Math.Max(120, durationMs - 40)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            if (element.RenderTransform is TranslateTransform translate)
            {
                translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, new DoubleAnimation
                {
                    From = offsetX,
                    To = 0.0,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
                translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation
                {
                    From = offsetY,
                    To = 0.0,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
            }
        }

        private static void AnimateIconEntrance(Border iconHost, double offsetX, double offsetY, TimeSpan beginTime, int durationMs, IEasingFunction easing)
        {
            iconHost.Opacity = 0.0;
            var translate = new TranslateTransform(offsetX, offsetY);
            iconHost.RenderTransform = translate;

            iconHost.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(Math.Max(80, durationMs - 30)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            translate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, new DoubleAnimation
            {
                From = offsetX,
                To = 0.0,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = easing
            });
            translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation
            {
                From = offsetY,
                To = 0.0,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = easing
            });
        }

        private static void AnimateSegmentScaleEntrance(UIElement element, double fromScale, double toScale, TimeSpan beginTime, int durationMs, IEasingFunction easing)
        {
            element.Opacity = 0.0;
            element.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(fromScale, fromScale);

            element.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(Math.Max(120, durationMs - 60)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            if (element.RenderTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation
                {
                    From = fromScale,
                    To = toScale,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation
                {
                    From = fromScale,
                    To = toScale,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
            }
        }

        private static void AnimateIconScaleEntrance(Border iconHost, double fromScale, double toScale, TimeSpan beginTime, int durationMs, IEasingFunction easing)
        {
            iconHost.Opacity = 0.0;
            iconHost.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
            iconHost.RenderTransform = new ScaleTransform(fromScale, fromScale);

            iconHost.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = beginTime,
                Duration = TimeSpan.FromMilliseconds(Math.Max(120, durationMs - 50)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            if (iconHost.RenderTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation
                {
                    From = fromScale,
                    To = toScale,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation
                {
                    From = fromScale,
                    To = toScale,
                    BeginTime = beginTime,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = easing
                });
            }
        }

        private void PlaySoftFadeOpenAnimation()
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(170),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            RootScaleTransform.ScaleX = 0.995;
            RootScaleTransform.ScaleY = 0.995;
            RootTranslateTransform.Y = 4.0;

            RootScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation
            {
                From = 0.995,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            RootScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation
            {
                From = 0.995,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            RootTranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation
            {
                From = 4.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            foreach (var centerTarget in EnumerateCenterAnimationTargets())
            {
                centerTarget.Opacity = 0.0;
                centerTarget.BeginAnimation(OpacityProperty, new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
            }

            AnimateUtilityDockEntrance(0.0, 1.0, 0.92, 1.0, 200, 200);
        }

        private void AnimateSegmentHighlight(ShapePath segment, bool active)
        {
            if (!_segmentBrushes.TryGetValue(segment, out var brush))
            {
                return;
            }

            var target = active
                ? IsNeonOrbitStyle() && segment.Stroke is SolidColorBrush activeStroke
                    ? System.Windows.Media.Color.FromArgb(54, activeStroke.Color.R, activeStroke.Color.G, activeStroke.Color.B)
                    : segment.Tag is MenuItemConfig activeItem
                        ? GetItemActiveSegmentColor(activeItem)
                        : _theme.SegmentActiveColor
                : IsNeonOrbitStyle() && segment.Stroke is SolidColorBrush baseStroke
                    ? System.Windows.Media.Color.FromArgb(0, baseStroke.Color.R, baseStroke.Color.G, baseStroke.Color.B)
                    : segment.Tag is MenuItemConfig baseItem
                        ? GetItemBaseSegmentColor(baseItem)
                        : _theme.SegmentColor;

            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            brush.Color = target;
        }
    }
}
