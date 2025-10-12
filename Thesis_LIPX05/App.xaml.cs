﻿using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

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

        protected override void OnStartup(StartupEventArgs e)
        {
            const string MutexName = "Y0KAI Task Scheduler";

            try
            {
                appMutex = new(true, MutexName, out bool createdNew);

                if (!createdNew)
                {
                    int currID = Environment.ProcessId;

                    Process runningProc = GetProcessesByName(GetCurrentProcess().ProcessName)
                        .FirstOrDefault(p => p.Id != currID)!;

                    if (runningProc is not null && runningProc.MainWindowHandle != IntPtr.Zero)
                    {
                        // Ensure the window is restored before bringing to foreground
                        ShowWindow(runningProc.MainWindowHandle, 9); // 9 -> SW_RESTORE
                        SetForegroundWindow(runningProc.MainWindowHandle);
                    }

                    Shutdown();
                    return;
                }

                base.OnStartup(e);
                ShutdownMode = ShutdownMode.OnLastWindowClose;
                Dispatcher.BeginInvoke(new Action(() => ApplicationStartupLogic(e)), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fatal startup error during Mutex check: {ex.Message}",
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            appMutex?.ReleaseMutex();
            appMutex?.Dispose();
            base.OnExit(e);
        }

        // The main entry point for the application
        private async void ApplicationStartupLogic(StartupEventArgs e)
        {
            LoadingWindow loading = new();
            loading.Show();

            // Simulate loading progress by updating the loading window's progress text
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

            // Show the main window and close the loading window
            LaunchMainWindowAsync();
            loading.Close();

            e.Equals(null);
        }

        // Launches the main application window asynchronously
        private async void LaunchMainWindowAsync() => await Dispatcher.InvokeAsync(() =>
            {
                MainWindow ts_app = new();
                MainWindow = ts_app;
                ts_app.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                ts_app.Show();
                ts_app.Activate();
            });


        // Decides the loading status based on the given integer value
        public static LoadingStatusHelper DecideRang(int i)
        {
            if (i <= 20) return LoadingStatusHelper.FIRST;
            else if (i <= 40) return LoadingStatusHelper.SECOND;
            else if (i <= 60) return LoadingStatusHelper.THIRD;
            else if (i <= 80) return LoadingStatusHelper.FOURTH;
            else return LoadingStatusHelper.FIFTH;
        }
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
}