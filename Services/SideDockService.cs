using System;
using System.Collections.Generic;
using System.Windows;
using RadialSek.Models;
using RadialSek.UI;

namespace RadialSek.Services
{
    public class SideDockService
    {
        private static SideDockService? _instance;
        public static SideDockService Instance => _instance ??= new SideDockService();

        private readonly List<SideDockWindow> _windows = new();
        private LauncherService? _launcherService;
        private MenuConfigService? _configService;
        private MenuConfig? _config;

        public void Initialize(LauncherService launcherService, MenuConfigService configService, MenuConfig config)
        {
            _launcherService = launcherService;
            _configService = configService;
            _config = config;

            if (_config?.Tools?.SideMenu?.EnableSideMenu == true)
            {
                EnsureWindows();
            }
        }

        public void RefreshConfig(MenuConfig config)
        {
            _config = config;
            if (_config?.Tools?.SideMenu?.EnableSideMenu == true)
            {
                if (_windows.Count == 0)
                {
                    EnsureWindows();
                }
                else
                {
                    foreach (var window in _windows)
                    {
                        window.RefreshConfig(_config);
                    }
                }
            }
            else
            {
                CloseWindows();
            }
        }

        public void SetEnabled(bool enabled)
        {
            if (_config?.Tools?.SideMenu != null)
            {
                _config.Tools.SideMenu.EnableSideMenu = enabled;
            }

            if (enabled)
            {
                EnsureWindows();
            }
            else
            {
                CloseWindows();
            }
        }

        public void EnsureWindows()
        {
            if (_launcherService == null || _configService == null || _config == null)
            {
                return;
            }

            CloseWindows();

            try
            {
                var virtualRight = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
                var virtualTop = SystemParameters.VirtualScreenTop;
                var virtualHeight = SystemParameters.WorkArea.Height;

                var primaryRight = SystemParameters.PrimaryScreenWidth;
                var primaryTop = SystemParameters.WorkArea.Top;
                var primaryHeight = SystemParameters.WorkArea.Height;

                // 1. Sağdaki monitörün en sağı (X = 3072 veya tek monitörde 1536)
                var rightmostWin = new SideDockWindow(virtualRight, primaryTop, primaryHeight, _launcherService, _configService, _config);
                _windows.Add(rightmostWin);
                rightmostWin.Show();

                // 2. Eğer çift monitör varsa, birinci monitörün sağına da (X = 1536) ekle!
                if (Math.Abs(virtualRight - primaryRight) > 60)
                {
                    var primaryWin = new SideDockWindow(primaryRight, primaryTop, primaryHeight, _launcherService, _configService, _config);
                    _windows.Add(primaryWin);
                    primaryWin.Show();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        public void CloseWindow()
        {
            CloseWindows();
        }

        public void CloseWindows()
        {
            foreach (var window in _windows)
            {
                try
                {
                    window.Close();
                }
                catch
                {
                }
            }
            _windows.Clear();
        }

        public void Reposition()
        {
            EnsureWindows();
        }
    }
}
