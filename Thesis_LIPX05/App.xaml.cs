using System.Diagnostics;
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
        private static Mutex? AppMTX;

        // Win32 imports
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(nint hWnd); // nint is IntPtr

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(nint hWnd, int nCmdShow);

        // Ensure single instance application using an instance of mutual exclusion (mutex)
        protected override void OnStartup(StartupEventArgs e)
        {
            const string MutexName = "Y0KAI Task Scheduler";

            try
            {
                AppMTX = new(true, MutexName, out bool createdNew);

                if (!createdNew)
                {
                    int currID = Environment.ProcessId;

                    Process runningProc = GetProcessesByName(GetCurrentProcess().ProcessName).FirstOrDefault(p => p.Id != currID)!;

                    if (runningProc is not null && runningProc.MainWindowHandle != nint.Zero)
                    {
                        MessageBox.Show("Another instance of Y0KAI Task Scheduler is already running. Bringing the existing instance to the foreground...",
                            "Instance Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
                        // Ensure the window is restored before bringing to foreground
                        ShowWindow(runningProc.MainWindowHandle, 9); // 9 -> SW_RESTORE
                        SetForegroundWindow(runningProc.MainWindowHandle);
                    }

                    Shutdown();
                    return;
                }

                base.OnStartup(e);
                ShutdownMode = ShutdownMode.OnLastWindowClose;
                Dispatcher.BeginInvoke(new Action(ApplicationStartupLogic), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fatal startup error during Mutex check: {ex.Message}",
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        // Clean up the mutex on application exit
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                AppMTX?.ReleaseMutex();
            }
            catch (ApplicationException appEx) when (appEx.Message.Contains("unsynchronized block")) {}
            catch (SynchronizationLockException) {}
            finally
            {
                AppMTX?.Dispose();
                base.OnExit(e);
            }
        }

        // The main entry point for the application
        private async void ApplicationStartupLogic()
        {
            LoadingWindow loading = new();
            loading.Show();

            // Simulate loading progress by updating the loading window's progress text
            for (uint i = 1; i <= 100; i++)
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
                        await loading.UpdateProgressAsync("Remembering task scheduling...");
                        break;
                    case LoadingStatusHelper.FOURTH:
                        await loading.UpdateProgressAsync("Preparing renderers...");
                        break;
                    case LoadingStatusHelper.FIFTH:
                        await loading.UpdateProgressAsync("Finalizing...");
                        break;
                }

                await Task.Delay(30);
            }

            // Show the main window and close the loading window
            LaunchMainWindowAsync(loading);
        }

        // Launches the main application window asynchronously (in maximized state)
        private async void LaunchMainWindowAsync(LoadingWindow shouldBeClosedByNow) =>
            await Dispatcher.InvokeAsync(() =>
            {
                MainWindow ts_app = new();
                MainWindow = ts_app;
                ts_app.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                // ts_app.ResizeMode = ResizeMode.NoResize; // for future plans
                ts_app.Show();
                ts_app.Activate();
                shouldBeClosedByNow.Close();
            });


        // Decides the loading status based on the given integer value
        private static LoadingStatusHelper DecideRang(uint i)
        {
            if (i <= 20) return LoadingStatusHelper.FIRST;
            else if (i <= 40) return LoadingStatusHelper.SECOND;
            else if (i <= 60) return LoadingStatusHelper.THIRD;
            else if (i <= 80) return LoadingStatusHelper.FOURTH;
            else return LoadingStatusHelper.FIFTH;
        }
    }

    // The loading status helper enum to determine the current loading stage (through simulation)
    enum LoadingStatusHelper
    {
        FIRST,
        SECOND,
        THIRD,
        FOURTH,
        FIFTH
    }
}