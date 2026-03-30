using System.Windows;
using System;
using System.IO;
using RadialSek.Services;

namespace RadialSek
{
    public partial class App : Application
    {
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            _mainWindow = new MainWindow();
            _mainWindow.Hide();
        }

        private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            TryLogUnhandledException("UI thread exception", e.Exception);
            e.Handled = true;
        }

        private static void TryLogUnhandledException(string context, Exception exception)
        {
            try
            {
                var logPath = ApplicationStorageService.GetDiagnosticLogPath();
                var content =
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + context + Environment.NewLine +
                    exception + Environment.NewLine + Environment.NewLine;
                File.AppendAllText(logPath, content);
            }
            catch
            {
            }
        }
    }
}
