using Microsoft.Win32;

using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool isFileLoaded = false;
        private XElement masterRecipe = new("MasterRecipe");
        private DataTable masterTable = new("Master Recipe Table");

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
                isFileLoaded = true;
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Save functionality not implemented yet.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);

        private void DrawGraph_Click(object sender, RoutedEventArgs e)
        {
            if (isFileLoaded)
            {
                SGraphCanvas.Children.Clear();
                try
                {
                    if (MainTab.Items[0] is TabItem) BuildSGraphFromXml();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex}", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("No BatchML File loaded! Drawing example graph...", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                DrawExampleGraph();
            }
        }

        private void ClearUp()
        {
            SGraphCanvas.Children.Clear();
            GetNodes().Clear();
            GetEdges().Clear();
        }

        private void DrawExampleGraph()
        {
            ClearUp();

            int
                i = 0,
                j = 0,
                prodID = 1;

            for (int eqID = 1; eqID <= 9; eqID++) // Example nodes
            {
                AddNode($"Eq{eqID}", $"Eq{eqID}", new(i, j));
                i += 50;
                if (eqID % 3 == 0)
                {
                    AddNode($"Prod{prodID++}", $"Prod{prodID}", new(i + 200, j += 50));
                    i = 0;
                }
            }

            var rnd = new Random();
            for (int x = 1; x < GetNodes().Count; x++) // Example edges
                AddEdge($"Eq{x}", (x % 3 != 0) ? $"Eq{x + 1}" : $"Prod{x / 3}", rnd.Next(5, 45));

            Render(SGraphCanvas, 4);
        }

        protected void BuildSGraphFromXml()
        {
            ClearUp();

            var batchML = XNamespace.Get("http://www.wbf.org/xml/BatchML-V02")
                ?? throw new Exception("BatchML namespace not found.");

            var customNS = XNamespace.Get("http://lipx05.y0kai.com/batchml/custom")
                ?? throw new Exception("Custom namespace not found.");

            var steps = masterRecipe.Descendants(batchML + "Step")
                                    .Select(x => x.Element(batchML + "ID")?.Value.Trim().ToLower())
                                    .Where(x => !string.IsNullOrEmpty(x))
                                    .Distinct()
                                    .ToList();

            var stepDescs = masterRecipe.Descendants(batchML + "Step")
                .Select(x => x.Element(batchML + "RecipeElementID")?.Value)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList();

            int i = 0, j = 0;

            for (int a = 0; a < steps.Count && a < stepDescs.Count; a++)
            {
                var node = new Node
                {
                    ID = steps[a]!,
                    Desc = stepDescs[a]!,
                    Position = new(j + 50, i + 50)
                };

                GetNodes().Add(node.ID, node);

                i += 100;
                if (i % 300 == 0) j += 100;
            }

            var links = masterRecipe.Descendants(batchML + "Link")
                .Select(x =>
                {
                    var fromID = x.Element(batchML + "FromID")?.Element(batchML + "FromIDValue")?.Value.Trim();
                    var toID = x.Element(batchML + "ToID")?.Element(batchML + "ToIDValue")?.Value.Trim();
                    var durEl = x.Element(batchML + "Extension")?.Descendants(customNS + "Duration").FirstOrDefault()?.Value;

                    double cost = 0;
                    if (durEl != null)
                    {
                        try
                        {
                            cost = XmlConvert.ToTimeSpan(durEl).TotalMinutes; // ISO 8601 -> minutes
                        }
                        catch
                        {
                            cost = double.NaN; // Invalid duration, set cost to NaN
                        }
                    }

                    return new { fromID, toID, cost };
                })
                .Where(x => !string.IsNullOrEmpty(x.fromID) && !string.IsNullOrEmpty(x.toID) && !double.IsNaN(x.cost))
                .ToList();

            foreach (var link in links)
            {
                bool fromExists = GetNodes().TryGetValue(link.fromID!, out Node? fromNode);
                bool toExists = GetNodes().TryGetValue(link.toID!, out Node? toNode);
                MessageBox.Show($"From Node: {fromExists}, ID: {link.fromID}");
                MessageBox.Show($"To Node: {toExists}, ID: {link.toID}");
                GetEdges().Add(new Edge
                {
                    From = fromNode!,
                    To = toNode!,
                    Cost = link.cost
                });
            }

            Render(SGraphCanvas, 15);
        }

        private void LoadBatchML(string path)
        {
            try
            {
                var batchML = XNamespace.Get("http://www.wbf.org/xml/BatchML-V02")
                    ?? throw new Exception("BatchML namespace not found.");

                var doc = XDocument.Load(path);

                masterRecipe = doc.Descendants(batchML + "MasterRecipe").FirstOrDefault()
                    ?? throw new Exception("Master element not found in BatchML file.");

                var id = masterRecipe.Element(batchML + "ID")?.Value;
                var ver = masterRecipe.Element(batchML + "Version")?.Value;
                var desc = masterRecipe.Elements(batchML + "Description").LastOrDefault()?.Value;

                var header = masterRecipe.Element(batchML + "Header")
                    ?? throw new Exception("Header element not found in BatchML file.");

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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading BatchML file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SGraphCanvas.Children.Clear();
                masterTable.Clear();

                for (int i = MainTab.Items.Count - 1; i >= 0; i--)
                {
                    if (MainTab.Items[i] is TabItem tab && tab.Header.ToString() == "Master Recipe")
                    {
                        MainTab.Items.RemoveAt(i);
                        break;
                    }
                }

                isFileLoaded = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error closing BatchML: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void ExportSGraph_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement S-Graph export functionality (targetting the JPEG format)
        }

        private void ExportGantt_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement Gantt chart export functionality (targetting the JPEG format)
        }
    }
}