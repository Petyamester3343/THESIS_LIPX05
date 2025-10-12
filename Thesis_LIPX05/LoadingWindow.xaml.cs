using System.Windows;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for LoadingWindow.xaml
    /// </summary>
    public partial class LoadingWindow : Window
    {
        public LoadingWindow() => InitializeComponent();

        // Updates the loading text with the given status (asynchronous)
        public async Task UpdateProgressAsync(string s)
        {
            if (!Dispatcher.CheckAccess()) await Dispatcher.InvokeAsync(() => UpdateProgress(s));
            else UpdateProgress(s);
        }

        // Updates the loading text with the given status (synchronous)
        private void UpdateProgress(string s) => LoadingText.Text = s;
    }
}