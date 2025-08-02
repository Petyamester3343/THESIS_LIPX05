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

        public async Task UpdateProgressAsync(string status)
        {
            if (Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => UpdateProgress(status));
                return;
            }
        }

        private void UpdateProgress(string status) => LoadingText.Text = status;
    }
}
