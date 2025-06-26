using Microsoft.Win32;

using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using Thesis_LIPX05.Util;

using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void SolveClick(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            string NullMSG = "Unknown";
            MessageBox.Show($"{menuItem?.Tag ?? NullMSG} is not yet implemented!", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow().Show();

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (GanttCanvas != null)
                GanttCanvas.LayoutTransform = new ScaleTransform(e.NewValue, 1)
                    ?? throw new Exception("GanttCanvas is null.");
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                DefaultExt = ".xml",
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*"
            };

            bool? res = dlg.ShowDialog();
            if (res == true)
            {
                LoadBatchML(dlg.FileName);
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Save functionality not implemented yet.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);

        private void DrawGraph_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Please load a BatchML file first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            var graph = new SGraph();

            int i = 0, j = 0, k = 1;
            for (int x = 1; x <= 9; x++) // Example nodes
            {
                AddNode($"Eq{x}", new Point(i, j));
                i += 50;
                if (x % 3 == 0)
                {
                    AddNode($"Prod{k++}", new Point(i + 200, j));
                    i = 0; j += 50;
                }
            }

            for (int x = 1; x <= GetNodes().Count - 1; x++) // Example edges
                graph.AddEdge($"Eq{x}", (x % 3 != 0) ? $"Eq{x + 1}" : $"Prod{x / 3}");

            graph.Render(SGraphCanvas, 4);
        }

        protected void BuildSGraphFromXml(XElement master)
        {
            SGraphCanvas.Children.Clear();
            var batchML = XNamespace.Get("http://www.wbf.org/xml/BatchML-V02")
                ?? throw new Exception("BatchML namespace not found.");
            var steps = master.Descendants(batchML + "Step")
                .Select(x => x.Element(batchML + "ID")?.Value)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct().ToList();

            var links = master.Descendants(batchML + "Link")
                .Select(x => new
                {
                    From = x.Element(batchML + "FromID")?.Element(batchML + "FromIDValue")?.Value,
                    To = x.Element(batchML + "ToID")?.Element(batchML + "ToIDValue")?.Value
                })
                .Where(x => !string.IsNullOrEmpty(x.From) && !string.IsNullOrEmpty(x.To))
                .ToList();

            var graph = new SGraph();
            int i = 0, j = 0;
            foreach (var step in steps)
            {
                AddNode(step, new Point(j + 50, i + 50));
                i += 100;
                if (i % 3 == 0) j += 100;
            }
            foreach (var link in links) graph.AddEdge(link.From, link.To);

            graph.Render(SGraphCanvas, 15);
        }

        private void LoadBatchML(string path)
        {
            try
            {
                var batchML = XNamespace.Get("http://www.wbf.org/xml/BatchML-V02")
                    ?? throw new Exception("BatchML namespace not found.");

                var doc = XDocument.Load(path);

                var master = doc.Descendants(batchML + "MasterRecipe").FirstOrDefault()
                    ?? throw new Exception("Master element not found in BatchML file.");

                var header = master.Element(batchML + "Header")
                    ?? throw new Exception("Header element not found in BatchML file.");

                var id = master.Element(batchML + "ID")?.Value;
                var ver = master.Element(batchML + "Version")?.Value;
                var desc = master.Elements(batchML + "Description").LastOrDefault()?.Value;
                var prodName = header.Element(batchML + "ProductName")?.Value;

                var batchSize = header?.Element(batchML + "BatchSize")
                    ?? throw new Exception("BatchSize element not found in BatchML file.");

                var nominal = batchSize.Element(batchML + "Nominal")?.Value;
                var min = batchSize.Element(batchML + "Min")?.Value;
                var max = batchSize.Element(batchML + "Max")?.Value;
                var unit = batchSize.Element(batchML + "UnitOfMeasure")?.Value;

                var masterTable = new DataTable()
                {
                    TableName = "Master Recipe"
                };
                masterTable.Columns.Add("RecipeID");
                masterTable.Columns.Add("Version");
                masterTable.Columns.Add("Description");
                masterTable.Columns.Add("ProductName");
                masterTable.Columns.Add("NominalBatchSize");
                masterTable.Columns.Add("MinBatchSize");
                masterTable.Columns.Add("MaxBatchSize");
                masterTable.Columns.Add("UnitOfMeasure");

                masterTable.Rows.Add(
                    id, ver, desc, prodName, nominal, min, max, unit
                );

                DisplayDataTable(masterTable);
                BuildSGraphFromXml(master);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading BatchML file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayDataTable(DataTable dataTable)
        {
            var grid = new DataGrid
            {
                ItemsSource = dataTable.DefaultView,
                AutoGenerateColumns = true,
                IsReadOnly = false
            };

            var container = new Grid();
            container.Children.Add(grid);

            var tab = new TabItem
            {
                Header = dataTable.TableName,
                Content = container
            };
            MainTab.Items.Insert(0, tab);
            MainTab.SelectedIndex = 0;
        }
    }
}