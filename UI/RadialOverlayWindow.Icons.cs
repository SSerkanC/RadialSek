using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RadialSek.UI
{
    public partial class RadialOverlayWindow
    {
        private const int IconCacheLimit = 160;
        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiLargeIcon = 0x000000000;
        private const uint ShgfiUseFileAttributes = 0x000000010;
        private const uint FileAttributeDirectory = 0x00000010;
        private const uint FileAttributeNormal = 0x00000080;

        private static readonly Dictionary<string, ImageSource> IconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, LinkedListNode<string>> IconCacheNodes = new Dictionary<string, LinkedListNode<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> IconCacheUsage = new LinkedList<string>();
        private static readonly Dictionary<string, ImageSource> ExtensionIconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> PathSpecificIconExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe",
            ".lnk",
            ".ico",
            ".cur",
            ".ani",
            ".url",
            ".appref-ms"
        };
        private static readonly ImageSource FallbackIconImage = CreateFallbackIconImage();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShFileInfo
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref ShFileInfo psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static ImageSource LoadIcon(string targetPath)
        {
            var resolvedPath = ResolveIconPath(targetPath);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return FallbackIconImage;
            }

            if (TryGetCachedIcon(resolvedPath, out var cached))
            {
                return cached;
            }

            try
            {
                ImageSource? imageSource = null;
                if (Directory.Exists(resolvedPath))
                {
                    imageSource = LoadShellIcon(resolvedPath, isDirectory: true, useFileAttributes: false);
                }
                else if (File.Exists(resolvedPath))
                {
                    imageSource = LoadFileIcon(resolvedPath);
                }

                if (imageSource != null)
                {
                    CacheIcon(resolvedPath, imageSource);
                    return imageSource;
                }
            }
            catch
            {
            }

            CacheIcon(resolvedPath, FallbackIconImage);
            return FallbackIconImage;
        }

        private static ImageSource? LoadFileIcon(string resolvedPath)
        {
            var extension = System.IO.Path.GetExtension(resolvedPath);
            if (!string.IsNullOrWhiteSpace(extension) && !PathSpecificIconExtensions.Contains(extension))
            {
                if (TryGetCachedExtensionIcon(extension, out var cachedExtensionIcon))
                {
                    return cachedExtensionIcon;
                }

                var extensionIcon = LoadShellIcon(extension, isDirectory: false, useFileAttributes: true);
                if (extensionIcon != null)
                {
                    CacheExtensionIcon(extension, extensionIcon);
                    return extensionIcon;
                }
            }

            var shellIcon = LoadShellIcon(resolvedPath, isDirectory: false, useFileAttributes: false);
            if (shellIcon != null)
            {
                return shellIcon;
            }

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(resolvedPath);
            return ConvertIconToImageSource(icon);
        }

        private static bool TryGetCachedIcon(string resolvedPath, out ImageSource cached)
        {
            lock (IconCache)
            {
                if (!IconCache.TryGetValue(resolvedPath, out cached!))
                {
                    return false;
                }

                TouchIconCacheEntry(resolvedPath);
                return true;
            }
        }

        private static void CacheIcon(string resolvedPath, ImageSource imageSource)
        {
            lock (IconCache)
            {
                IconCache[resolvedPath] = imageSource;
                TouchIconCacheEntry(resolvedPath);

                while (IconCache.Count > IconCacheLimit && IconCacheUsage.First != null)
                {
                    var oldestKey = IconCacheUsage.First.Value;
                    IconCacheUsage.RemoveFirst();
                    IconCacheNodes.Remove(oldestKey);
                    IconCache.Remove(oldestKey);
                }
            }
        }

        private static void TouchIconCacheEntry(string resolvedPath)
        {
            if (IconCacheNodes.TryGetValue(resolvedPath, out var existingNode))
            {
                IconCacheUsage.Remove(existingNode);
                IconCacheUsage.AddLast(existingNode);
                return;
            }

            var node = new LinkedListNode<string>(resolvedPath);
            IconCacheNodes[resolvedPath] = node;
            IconCacheUsage.AddLast(node);
        }

        private static bool TryGetCachedExtensionIcon(string extension, out ImageSource cached)
        {
            lock (IconCache)
            {
                return ExtensionIconCache.TryGetValue(extension, out cached!);
            }
        }

        private static void CacheExtensionIcon(string extension, ImageSource imageSource)
        {
            lock (IconCache)
            {
                ExtensionIconCache[extension] = imageSource;
            }
        }

        private static string ResolveIconPath(string targetPath)
        {
            return string.IsNullOrWhiteSpace(targetPath)
                ? string.Empty
                : Environment.ExpandEnvironmentVariables(targetPath.Trim());
        }

        private static ImageSource? LoadShellIcon(string path, bool isDirectory, bool useFileAttributes)
        {
            var flags = ShgfiIcon | ShgfiLargeIcon;
            var attributes = 0u;
            if (useFileAttributes)
            {
                flags |= ShgfiUseFileAttributes;
                attributes = isDirectory ? FileAttributeDirectory : FileAttributeNormal;
            }

            var fileInfo = new ShFileInfo();
            var result = SHGetFileInfo(path, attributes, ref fileInfo, (uint)Marshal.SizeOf<ShFileInfo>(), flags);
            if (result == IntPtr.Zero || fileInfo.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return CreateBitmapSource(fileInfo.hIcon);
            }
            finally
            {
                DestroyIcon(fileInfo.hIcon);
            }
        }

        private static ImageSource? ConvertIconToImageSource(System.Drawing.Icon? icon)
        {
            if (icon == null)
            {
                return null;
            }

            return CreateBitmapSource(icon.Handle);
        }

        private static ImageSource CreateBitmapSource(IntPtr iconHandle)
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(64, 64));
            source.Freeze();
            return source;
        }

        private static ImageSource CreateFallbackIcon()
        {
            return FallbackIconImage;
        }

        private static ImageSource CreateFallbackIconImage()
        {
            var drawing = new GeometryDrawing(
                System.Windows.Media.Brushes.White,
                null,
                new RectangleGeometry(new Rect(12, 12, 40, 40), 8, 8));
            drawing.Freeze();
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }
    }
}
