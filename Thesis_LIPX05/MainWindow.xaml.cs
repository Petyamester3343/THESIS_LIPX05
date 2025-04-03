using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private double currentZoom = 1;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SolveClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            string NullMSG = "Unknown";
            MessageBox.Show($"{menuItem?.Tag ?? NullMSG}", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow().Show();

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            currentZoom = e.NewValue;
            if (GanttCanvas != null) GanttCanvas.LayoutTransform = new ScaleTransform(currentZoom, currentZoom);
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {

        }

        private void LoadFromDB_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Save2DB_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DrawGraph_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}