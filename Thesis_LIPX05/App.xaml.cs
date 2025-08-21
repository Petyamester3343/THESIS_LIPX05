using System.Windows;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
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