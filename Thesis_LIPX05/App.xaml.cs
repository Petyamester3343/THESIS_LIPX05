using System.Runtime.InteropServices;
using System.Windows;

using static System.Diagnostics.Process;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? appMutex;

        // Win32 imports
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "Thesis_LIPX05";
            appMutex = new(true, appName, out bool createdNew);
            if (!createdNew) // Application is already running
            {
                BringExistingInstanceToFront();
                Shutdown();
                return;
            }
            base.OnStartup(e);
        }

        private static void BringExistingInstanceToFront()
        {
            try
            {
                var curr = GetCurrentProcess();
                var running = GetProcessesByName(curr.ProcessName)
                    .FirstOrDefault(p => p.Id != curr.Id);

                if (running is not null)
                {
                    IntPtr hWnd = running.MainWindowHandle;
                    if (hWnd != IntPtr.Zero)
                    {
                        ShowWindow(hWnd, SW_RESTORE); // Restore if minimized
                        SetForegroundWindow(hWnd); // Bring to foreground
                    }
                }
            }
            catch
            {
                MessageBox.Show("Another instance is already running.",
                    "Instance Running",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            appMutex?.ReleaseMutex();
            appMutex?.Dispose();
            base.OnExit(e);
        }

        // The loading status helper enum to determine the current loading stage (through simulation)
        public enum LoadingStatusHelper
        {
            FIRST,
            SECOND,
            THIRD,
            FOURTH,
            FIFTH
        }

        private Window? jumper; // used to jump to the main window after loading

        // The main entry point for the application
        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            var loading = new LoadingWindow();
            loading.Show();

            // Create a dummy window to prevent the main window from showing up before the loading is complete
            jumper = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false
            };
            jumper.Show();

            // Simulates loading progress by updating the loading window's progress
            for (int i = 0; i <= 100; i++)
            {
                switch (DecideRang(i))
                {
                    case LoadingStatusHelper.FIRST:
                        await loading.UpdateProgressAsync("Building window...");
                        break;
                    case LoadingStatusHelper.SECOND:
                        await loading.UpdateProgressAsync("Fetching solvers...");
                        break;
                    case LoadingStatusHelper.THIRD:
                        await loading.UpdateProgressAsync("Remembering S-Graph...");
                        break;
                    case LoadingStatusHelper.FOURTH:
                        await loading.UpdateProgressAsync("Preparing renderers...");
                        break;
                    case LoadingStatusHelper.FIFTH:
                        await loading.UpdateProgressAsync("Finalizing...");
                        break;
                }

                await Task.Delay(33);
            }

            // Close the loading window and show the main window
            await loading.Dispatcher.InvokeAsync(loading.Close);

            await Dispatcher.InvokeAsync(() =>
            {
                var ts_app = new MainWindow();
                MainWindow = ts_app;
                ts_app.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                ts_app.Show();
                ts_app.Activate();
            });

            jumper.Close();
        }

        // Decides the loading status based on the given integer value
        public static LoadingStatusHelper DecideRang(int i)
        {
            if (i <= 20) return LoadingStatusHelper.FIRST;
            else if (i >= 20 && i <= 40) return LoadingStatusHelper.SECOND;
            else if (i >= 40 && i <= 60) return LoadingStatusHelper.THIRD;
            else if (i >= 60 && i <= 80) return LoadingStatusHelper.FOURTH;
            else return LoadingStatusHelper.FIFTH;
        }
    }
}