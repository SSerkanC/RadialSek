using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RadialSek.Models;

namespace RadialSek.Services
{
    public static class CategorySymbolService
    {
        private static readonly IReadOnlyList<CategorySymbolOption> Options = new List<CategorySymbolOption>
        {
            new CategorySymbolOption("DefaultGrid", "Varsayılan", "Dört kareli klasik kategori sembolü"),
            new CategorySymbolOption("Spark", "Parıltı", "Modern yıldız vurgusu"),
            new CategorySymbolOption("Layers", "Katmanlar", "Üst üste binen kartlar"),
            new CategorySymbolOption("Compass", "Pusula", "Yön ve keşif hissi"),
            new CategorySymbolOption("Rocket", "Roket", "Hızlı erişim ve üretkenlik"),
            new CategorySymbolOption("Shield", "Kalkan", "Koruma ve güvenlik"),
            new CategorySymbolOption("Globe", "Küre", "Web ve ağ kategorileri"),
            new CategorySymbolOption("Bookmark", "Yer İmi", "Kaydedilen koleksiyonlar"),
            new CategorySymbolOption("Camera", "Kamera", "Görsel medya odaklı gruplar"),
            new CategorySymbolOption("Gamepad", "Oyun Kolu", "Oyun klasörleri"),
            new CategorySymbolOption("Music", "Müzik", "Ses ve medya arşivi"),
            new CategorySymbolOption("Code", "Kod", "Geliştirme araçları"),
            new CategorySymbolOption("Home", "Ev", "Sık kullanılan başlangıç alanı"),
            new CategorySymbolOption("Trophy", "Kupa", "Öne çıkanlar"),
            new CategorySymbolOption("Heart", "Kalp", "Favoriler"),
            new CategorySymbolOption("Box", "Kutu", "Arşiv ve paketler")
        };

        public static IReadOnlyList<CategorySymbolOption> GetOptions()
        {
            return Options;
        }

        public static string NormalizeKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "DefaultGrid";
            }

            return Options.Any(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                ? key
                : "DefaultGrid";
        }

        public static FrameworkElement CreateSymbolVisual(string? key, double size, Brush strokeBrush)
        {
            var normalized = NormalizeKey(key);
            var visual = normalized switch
            {
                "Spark" => CreateSpark(size, strokeBrush),
                "Layers" => CreateLayers(size, strokeBrush),
                "Compass" => CreateCompass(size, strokeBrush),
                "Rocket" => CreateRocket(size, strokeBrush),
                "Shield" => CreateShield(size, strokeBrush),
                "Globe" => CreateGlobe(size, strokeBrush),
                "Bookmark" => CreateBookmark(size, strokeBrush),
                "Camera" => CreateCamera(size, strokeBrush),
                "Gamepad" => CreateGamepad(size, strokeBrush),
                "Music" => CreateMusic(size, strokeBrush),
                "Code" => CreateCode(size, strokeBrush),
                "Home" => CreateHome(size, strokeBrush),
                "Trophy" => CreateTrophy(size, strokeBrush),
                "Heart" => CreateHeart(size, strokeBrush),
                "Box" => CreateBox(size, strokeBrush),
                _ => CreateDefaultGrid(size, strokeBrush)
            };

            return new Viewbox
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Child = visual
            };
        }

        private static Canvas CreateCanvas(double size)
        {
            return new Canvas
            {
                Width = size,
                Height = size,
                SnapsToDevicePixels = true,
                ClipToBounds = false,
                Background = Brushes.Transparent
            };
        }

        private static Shape CreateStrokeShape(Shape shape, Brush strokeBrush, double thickness)
        {
            shape.Stroke = strokeBrush;
            shape.StrokeThickness = thickness;
            shape.StrokeStartLineCap = PenLineCap.Round;
            shape.StrokeEndLineCap = PenLineCap.Round;
            shape.StrokeLineJoin = PenLineJoin.Round;
            shape.SnapsToDevicePixels = true;
            return shape;
        }

        private static FrameworkElement CreateDefaultGrid(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var cell = size * 0.22;
            var gap = size * 0.14;
            var radius = cell * 0.28;
            var start = (size - ((cell * 2) + gap)) / 2;
            for (var row = 0; row < 2; row++)
            {
                for (var column = 0; column < 2; column++)
                {
                    var rect = new Rectangle
                    {
                        Width = cell,
                        Height = cell,
                        RadiusX = radius,
                        RadiusY = radius,
                        Fill = Brushes.Transparent
                    };
                    CreateStrokeShape(rect, strokeBrush, Math.Max(1.6, size * 0.08));
                    Canvas.SetLeft(rect, start + (column * (cell + gap)));
                    Canvas.SetTop(rect, start + (row * (cell + gap)));
                    canvas.Children.Add(rect);
                }
            }

            return canvas;
        }

        private static FrameworkElement CreateSpark(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var star = new Path
            {
                Data = Geometry.Parse($"M {size * 0.5},{size * 0.16} L {size * 0.6},{size * 0.38} L {size * 0.84},{size * 0.5} L {size * 0.6},{size * 0.62} L {size * 0.5},{size * 0.84} L {size * 0.4},{size * 0.62} L {size * 0.16},{size * 0.5} L {size * 0.4},{size * 0.38} Z"),
                Fill = Brushes.Transparent
            };
            CreateStrokeShape(star, strokeBrush, Math.Max(1.6, size * 0.08));
            canvas.Children.Add(star);
            return canvas;
        }

        private static FrameworkElement CreateLayers(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var rect1 = new Rectangle { Width = size * 0.42, Height = size * 0.42, RadiusX = size * 0.08, RadiusY = size * 0.08, Fill = Brushes.Transparent };
            var rect2 = new Rectangle { Width = size * 0.42, Height = size * 0.42, RadiusX = size * 0.08, RadiusY = size * 0.08, Fill = Brushes.Transparent };
            CreateStrokeShape(rect1, strokeBrush, Math.Max(1.5, size * 0.07));
            CreateStrokeShape(rect2, strokeBrush, Math.Max(1.5, size * 0.07));
            Canvas.SetLeft(rect1, size * 0.16);
            Canvas.SetTop(rect1, size * 0.2);
            Canvas.SetLeft(rect2, size * 0.36);
            Canvas.SetTop(rect2, size * 0.34);
            canvas.Children.Add(rect1);
            canvas.Children.Add(rect2);
            return canvas;
        }

        private static FrameworkElement CreateCompass(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var circle = new Ellipse { Width = size * 0.68, Height = size * 0.68, Fill = Brushes.Transparent };
            CreateStrokeShape(circle, strokeBrush, Math.Max(1.5, size * 0.07));
            Canvas.SetLeft(circle, size * 0.16);
            Canvas.SetTop(circle, size * 0.16);
            canvas.Children.Add(circle);

            var pointer = new Path
            {
                Data = Geometry.Parse($"M {size * 0.52},{size * 0.28} L {size * 0.66},{size * 0.56} L {size * 0.48},{size * 0.5} L {size * 0.34},{size * 0.72} L {size * 0.42},{size * 0.46} Z"),
                Fill = Brushes.Transparent
            };
            CreateStrokeShape(pointer, strokeBrush, Math.Max(1.5, size * 0.07));
            canvas.Children.Add(pointer);
            return canvas;
        }

        private static FrameworkElement CreateRocket(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var body = new Path
            {
                Data = Geometry.Parse($"M {size * 0.5},{size * 0.18} C {size * 0.68},{size * 0.25} {size * 0.76},{size * 0.44} {size * 0.68},{size * 0.6} C {size * 0.6},{size * 0.74} {size * 0.4},{size * 0.74} {size * 0.32},{size * 0.6} C {size * 0.24},{size * 0.44} {size * 0.32},{size * 0.25} {size * 0.5},{size * 0.18} Z"),
                Fill = Brushes.Transparent
            };
            CreateStrokeShape(body, strokeBrush, Math.Max(1.5, size * 0.07));
            var window = new Ellipse { Width = size * 0.16, Height = size * 0.16, Fill = Brushes.Transparent };
            CreateStrokeShape(window, strokeBrush, Math.Max(1.3, size * 0.06));
            Canvas.SetLeft(window, size * 0.42);
            Canvas.SetTop(window, size * 0.34);
            canvas.Children.Add(body);
            canvas.Children.Add(window);
            return canvas;
        }

        private static FrameworkElement CreateShield(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var shield = new Path
            {
                Data = Geometry.Parse($"M {size * 0.5},{size * 0.18} L {size * 0.76},{size * 0.28} V {size * 0.48} C {size * 0.76},{size * 0.64} {size * 0.66},{size * 0.8} {size * 0.5},{size * 0.86} C {size * 0.34},{size * 0.8} {size * 0.24},{size * 0.64} {size * 0.24},{size * 0.48} V {size * 0.28} Z"),
                Fill = Brushes.Transparent
            };
            CreateStrokeShape(shield, strokeBrush, Math.Max(1.5, size * 0.07));
            canvas.Children.Add(shield);
            return canvas;
        }

        private static FrameworkElement CreateGlobe(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var circle = new Ellipse { Width = size * 0.68, Height = size * 0.68, Fill = Brushes.Transparent };
            CreateStrokeShape(circle, strokeBrush, Math.Max(1.5, size * 0.07));
            Canvas.SetLeft(circle, size * 0.16);
            Canvas.SetTop(circle, size * 0.16);
            canvas.Children.Add(circle);
            canvas.Children.Add(CreateStrokeShape(new Line { X1 = size * 0.5, Y1 = size * 0.16, X2 = size * 0.5, Y2 = size * 0.84 }, strokeBrush, Math.Max(1.3, size * 0.06)));
            canvas.Children.Add(CreateStrokeShape(new Line { X1 = size * 0.16, Y1 = size * 0.5, X2 = size * 0.84, Y2 = size * 0.5 }, strokeBrush, Math.Max(1.3, size * 0.06)));
            return canvas;
        }

        private static FrameworkElement CreateBookmark(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var bookmark = new Path
            {
                Data = Geometry.Parse($"M {size * 0.34},{size * 0.18} H {size * 0.66} V {size * 0.8} L {size * 0.5},{size * 0.64} L {size * 0.34},{size * 0.8} Z"),
                Fill = Brushes.Transparent
            };
            CreateStrokeShape(bookmark, strokeBrush, Math.Max(1.5, size * 0.07));
            canvas.Children.Add(bookmark);
            return canvas;
        }

        private static FrameworkElement CreateCamera(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var body = new Rectangle { Width = size * 0.6, Height = size * 0.4, RadiusX = size * 0.08, RadiusY = size * 0.08, Fill = Brushes.Transparent };
            CreateStrokeShape(body, strokeBrush, Math.Max(1.5, size * 0.07));
            Canvas.SetLeft(body, size * 0.2);
            Canvas.SetTop(body, size * 0.32);
            var lens = new Ellipse { Width = size * 0.2, Height = size * 0.2, Fill = Brushes.Transparent };
            CreateStrokeShape(lens, strokeBrush, Math.Max(1.3, size * 0.06));
            Canvas.SetLeft(lens, size * 0.4);
            Canvas.SetTop(lens, size * 0.42);
            canvas.Children.Add(body);
            canvas.Children.Add(lens);
            return canvas;
        }

        private static FrameworkElement CreateGamepad(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var pad = new Path
            {
                Data = Geometry.Parse($"M {size * 0.28},{size * 0.54} C {size * 0.26},{size * 0.38} {size * 0.36},{size * 0.28} {size * 0.5},{size * 0.28} C {size * 0.64},{size * 0.28} {size * 0.74},{size * 0.38} {size * 0.72},{size * 0.54} C {size * 0.7},{size * 0.66} {size * 0.62},{size * 0.72} {size * 0.54},{size * 0.66} L {size * 0.46},{size * 0.66} C {size * 0.38},{size * 0.72} {size * 0.3},{size * 0.66} {size * 0.28},{size * 0.54} Z"),
                Fill = Brushes.Transparent
            };
            CreateStrokeShape(pad, strokeBrush, Math.Max(1.5, size * 0.07));
            canvas.Children.Add(pad);
            return canvas;
        }

        private static FrameworkElement CreateMusic(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var stem = CreateStrokeShape(new Line { X1 = size * 0.62, Y1 = size * 0.22, X2 = size * 0.62, Y2 = size * 0.66 }, strokeBrush, Math.Max(1.6, size * 0.08));
            var beam = CreateStrokeShape(new Line { X1 = size * 0.62, Y1 = size * 0.22, X2 = size * 0.42, Y2 = size * 0.28 }, strokeBrush, Math.Max(1.6, size * 0.08));
            var note = new Ellipse { Width = size * 0.18, Height = size * 0.14, Fill = Brushes.Transparent };
            CreateStrokeShape(note, strokeBrush, Math.Max(1.4, size * 0.06));
            Canvas.SetLeft(note, size * 0.34);
            Canvas.SetTop(note, size * 0.58);
            canvas.Children.Add(stem);
            canvas.Children.Add(beam);
            canvas.Children.Add(note);
            return canvas;
        }

        private static FrameworkElement CreateCode(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var left = CreateStrokeShape(new Polyline { Points = new PointCollection { new Point(size * 0.42, size * 0.26), new Point(size * 0.26, size * 0.5), new Point(size * 0.42, size * 0.74) }, Fill = Brushes.Transparent }, strokeBrush, Math.Max(1.6, size * 0.08));
            var right = CreateStrokeShape(new Polyline { Points = new PointCollection { new Point(size * 0.58, size * 0.26), new Point(size * 0.74, size * 0.5), new Point(size * 0.58, size * 0.74) }, Fill = Brushes.Transparent }, strokeBrush, Math.Max(1.6, size * 0.08));
            var slash = CreateStrokeShape(new Line { X1 = size * 0.54, Y1 = size * 0.24, X2 = size * 0.46, Y2 = size * 0.76 }, strokeBrush, Math.Max(1.4, size * 0.06));
            canvas.Children.Add(left);
            canvas.Children.Add(right);
            canvas.Children.Add(slash);
            return canvas;
        }

        private static FrameworkElement CreateHome(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var roof = CreateStrokeShape(new Polyline { Points = new PointCollection { new Point(size * 0.24, size * 0.48), new Point(size * 0.5, size * 0.26), new Point(size * 0.76, size * 0.48) }, Fill = Brushes.Transparent }, strokeBrush, Math.Max(1.6, size * 0.08));
            var body = new Rectangle { Width = size * 0.4, Height = size * 0.28, RadiusX = size * 0.05, RadiusY = size * 0.05, Fill = Brushes.Transparent };
            CreateStrokeShape(body, strokeBrush, Math.Max(1.4, size * 0.06));
            Canvas.SetLeft(body, size * 0.3);
            Canvas.SetTop(body, size * 0.48);
            canvas.Children.Add(roof);
            canvas.Children.Add(body);
            return canvas;
        }

        private static FrameworkElement CreateTrophy(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var cup = new Path
            {
                Data = Geometry.Parse($"M {size * 0.36},{size * 0.26} H {size * 0.64} V {size * 0.42} C {size * 0.64},{size * 0.54} {size * 0.58},{size * 0.62} {size * 0.5},{size * 0.64} C {size * 0.42},{size * 0.62} {size * 0.36},{size * 0.54} {size * 0.36},{size * 0.42} Z"),
                Fill = Brushes.Transparent
            };
            CreateStrokeShape(cup, strokeBrush, Math.Max(1.5, size * 0.07));
            var stem = CreateStrokeShape(new Line { X1 = size * 0.5, Y1 = size * 0.64, X2 = size * 0.5, Y2 = size * 0.78 }, strokeBrush, Math.Max(1.4, size * 0.06));
            var baseLine = CreateStrokeShape(new Line { X1 = size * 0.4, Y1 = size * 0.8, X2 = size * 0.6, Y2 = size * 0.8 }, strokeBrush, Math.Max(1.4, size * 0.06));
            canvas.Children.Add(cup);
            canvas.Children.Add(stem);
            canvas.Children.Add(baseLine);
            return canvas;
        }

        private static FrameworkElement CreateHeart(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var heart = new Path
            {
                Data = Geometry.Parse($"M {size * 0.5},{size * 0.78} C {size * 0.22},{size * 0.58} {size * 0.2},{size * 0.3} {size * 0.38},{size * 0.28} C {size * 0.46},{size * 0.28} {size * 0.5},{size * 0.34} {size * 0.5},{size * 0.34} C {size * 0.54},{size * 0.28} {size * 0.62},{size * 0.28} {size * 0.72},{size * 0.34} C {size * 0.84},{size * 0.44} {size * 0.78},{size * 0.66} {size * 0.5},{size * 0.78} Z"),
                Fill = Brushes.Transparent
            };
            CreateStrokeShape(heart, strokeBrush, Math.Max(1.5, size * 0.07));
            canvas.Children.Add(heart);
            return canvas;
        }

        private static FrameworkElement CreateBox(double size, Brush strokeBrush)
        {
            var canvas = CreateCanvas(size);
            var cube = new Path
            {
                Data = Geometry.Parse($"M {size * 0.5},{size * 0.2} L {size * 0.76},{size * 0.34} L {size * 0.76},{size * 0.64} L {size * 0.5},{size * 0.8} L {size * 0.24},{size * 0.64} L {size * 0.24},{size * 0.34} Z"),
                Fill = Brushes.Transparent
            };
            CreateStrokeShape(cube, strokeBrush, Math.Max(1.5, size * 0.07));
            canvas.Children.Add(cube);
            return canvas;
        }
    }
}
