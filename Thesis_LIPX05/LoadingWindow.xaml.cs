using System.Windows;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for LoadingWindow.xaml
    /// </summary>
    public partial class LoadingWindow : Window
    {
        public LoadingWindow()
        {
            InitializeComponent();
        }

        // Decides the loading status based on the given range value (asynchronous)
        public async Task UpdateProgressAsync(string status)
        {
            if (Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => UpdateProgress(status));
                return;
            }
        }

        // Updates the loading text with the given status (synchronous)
        private void UpdateProgress(string status) => LoadingText.Text = status;
    }
}
