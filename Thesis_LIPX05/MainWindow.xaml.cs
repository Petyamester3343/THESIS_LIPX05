using Microsoft.Win32;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
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
        private bool isFileLoaded = false;
        private XElement masterRecipe = new("MasterRecipe");

        private readonly DataTable
            masterTable = new("Master Recipe Table"),
            recipeElementTable = new("Recipe Element Table"),
            stepTable = new("Steps Table"),
            linkTable = new("Links Table");

        private List<Gantt.GanttItem> ganttData = [];
        private double zoom = 1.0;
        private const double baseTimeScale = 10.0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void SolveClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                if (menuItem.Tag.ToString() == "Heuristic")
                {
                    if (SGraphCanvas.Children.Count == 0)
                    {
                        MessageBox.Show("No S-Graph to solve!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var optimizer = new HeuristicOptimizer(GetNodes(), GetEdges());
                    var path = optimizer.Optimize();

                    ganttData = Gantt.BuildChartFromPath(path, GetEdges());
                    double totalTime = ganttData.Max(x => x.Start + x.Duration);
                    int rowC = ganttData.Count;
                    double tScale = baseTimeScale * zoom;

                    Gantt.Render(GanttCanvas, ganttData, tScale);
                    Gantt.DrawRuler(RulerCanvas, GanttCanvas, totalTime, tScale);
                }
                else if (menuItem.Tag.ToString() == "BnB")
                {
                    if (SGraphCanvas.Children.Count == 0)
                    {
                        MessageBox.Show("No S-Graph to solve!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var optimizer = new BnBOptimizer(GetNodes(), GetEdges());
                    var path = optimizer.Optimize();

                    ganttData = Gantt.BuildChartFromPath(path, GetEdges());
                    double totalTime = ganttData.Max(x => x.Start + x.Duration);
                    int rowC = ganttData.Count;
                    double tScale = baseTimeScale * zoom;

                    Gantt.Render(GanttCanvas, ganttData, tScale);
                    Gantt.DrawRuler(RulerCanvas, GanttCanvas, totalTime, tScale);
                }
                else MessageBox.Show("Unknown solve method selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow().Show();

        private void ZoomSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Slider slider) return;

            if (e.OriginalSource is FrameworkElement element && element.TemplatedParent is not Thumb)
            {
                Point pos = e.GetPosition(slider);
                double ratio = pos.X / slider.ActualWidth;
                double newValue = slider.Minimum + (slider.Maximum - slider.Minimum) * ratio;
                slider.Value = newValue;
                e.Handled = true; // Prevents slider from moving
            }
        }
        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ganttData == null || ganttData.Count == 0) return;

            zoom = Math.Pow(2, e.NewValue);
            double scale = baseTimeScale * zoom;

            double totalTime = ganttData.Max(x => x.Start + x.Duration);
            int rowC = ganttData.Count;

            GanttCanvas.LayoutTransform = Transform.Identity;

            GanttCanvas.Width = totalTime * scale + 100; // +100 for padding
            RulerCanvas.Width = GanttCanvas.Width;

            Gantt.Render(GanttCanvas, ganttData, scale);
            Gantt.DrawRuler(RulerCanvas, GanttCanvas, totalTime, scale);
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
            GanttCanvas.Children.Clear();
            RulerCanvas.Children.Clear();
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
                /*
                MessageBox.Show($"From Node: {fromExists}, ID: {link.fromID}");
                MessageBox.Show($"To Node: {toExists}, ID: {link.toID}");
                */
                GetEdges().Add(new Edge
                {
                    From = fromNode!,
                    To = toNode!,
                    Cost = link.cost
                });
            }

            Render(SGraphCanvas, 3);
        }

        private void LoadBatchML(string path)
        {
            try
            {
                var batchML = XNamespace.Get("http://www.wbf.org/xml/BatchML-V02")
                    ?? throw new Exception("BatchML namespace not found.");

                var customNS = XNamespace.Get("http://lipx05.y0kai.com/batchml/custom")
                    ?? throw new Exception("Custom namespace not found.");

                var doc = XDocument.Load(path);

                masterRecipe = doc.Descendants(batchML + "MasterRecipe").FirstOrDefault()
                    ?? throw new Exception("Master element not found in BatchML file.");

                // Master Recipe + Header datatable
                DisplayMasterTable(masterTable, batchML);

                // Recipe Element datatable
                DisplayRecipeElementTable(recipeElementTable, batchML);

                // Steps datatable
                DisplayStepTable(stepTable, batchML);

                // Links datatable
                DisplayLinkTable(linkTable, batchML, customNS);
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
                ClearUp();
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

        private void DisplayDataTable(DataTable dataTable, string tag)
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
                Content = container,
                Tag = tag
            };
            MainTab.Items.Insert(0, tab);
            MainTab.SelectedIndex = 0;
        }

        private void ExportSGraph_Click(object sender, RoutedEventArgs e)
        {
            var selectedTab = MainTab.SelectedItem as TabItem;

            if (selectedTab?.Tag?.ToString() == "SGraph")
            {
                if (SGraphCanvas.Children.Count == 0)
                {
                    MessageBox.Show("No S-Graph to export!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else JPEGExporter.ExportCanvas(SGraphCanvas, "S-Graph");
            }
            else MessageBox.Show("Please select the S-Graph tab to export.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ExportGantt_Click(object sender, RoutedEventArgs e)
        {
            var selectedTab = MainTab.SelectedItem as TabItem;

            if (selectedTab?.Tag?.ToString() == "Gantt")
            {
                if (GanttCanvas.Children.Count == 0)
                {
                    MessageBox.Show("No Gantt chart to export!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else JPEGExporter.ExportCanvas(GanttCanvas, "Gantt Chart");
            }
            else MessageBox.Show("Please select the Gantt chart tab to export.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void DisplayMasterTable(DataTable mdt, XNamespace batchML)
        {
            var id = masterRecipe.Element(batchML + "ID")?.Value;
            var ver = masterRecipe.Element(batchML + "Version")?.Value;
            var desc = masterRecipe.Elements(batchML + "Description").LastOrDefault()?.Value;

            var header = masterRecipe.Element(batchML + "Header")
                ?? throw new Exception("Header element not found in BatchML file.");

            var prodID = header.Element(batchML + "ProductID")?.Value
                ?? throw new Exception("ProductID element not found in BatchML file.");
            var prodName = header.Element(batchML + "ProductName")?.Value
                ?? throw new Exception("ProductName element not found in BatchML file.");
            var batchSize = header?.Element(batchML + "BatchSize")
                ?? throw new Exception("BatchSize element not found in BatchML file.");

            var nominal = batchSize.Element(batchML + "Nominal")?.Value;
            var min = batchSize.Element(batchML + "Min")?.Value;
            var max = batchSize.Element(batchML + "Max")?.Value;
            var unit = batchSize.Element(batchML + "UnitOfMeasure")?.Value;

            mdt.Columns.Add("RecipeID");
            mdt.Columns.Add("Version");
            mdt.Columns.Add("Description");
            mdt.Columns.Add("ProductName");
            mdt.Columns.Add("ProductID");
            mdt.Columns.Add("NominalBatchSize");
            mdt.Columns.Add("MinBatchSize");
            mdt.Columns.Add("MaxBatchSize");
            mdt.Columns.Add("UnitOfMeasure");

            mdt.Rows.Add(
                id, ver, desc, prodName, prodID, nominal, min, max, unit
            );

            DisplayDataTable(mdt, "MasterRecipe");
        }

        private void DisplayRecipeElementTable(DataTable redt, XNamespace batchML)
        {
            var recipeElements = masterRecipe.Descendants(batchML + "RecipeElement")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    Desc = x.Element(batchML + "Description")?.Value.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.ID) && !string.IsNullOrEmpty(x.Desc))
                .ToList();

            redt.Columns.Add("ID");
            redt.Columns.Add("Description");

            foreach (var re in recipeElements)
            {
                var dr = redt.NewRow();
                dr["ID"] = re.ID;
                dr["Description"] = re.Desc;
                redt.Rows.Add(dr);
            }

            DisplayDataTable(redt, "RecipeElements");
        }

        private void DisplayStepTable(DataTable sdt, XNamespace batchML)
        {
            var procLogic = masterRecipe.Element(batchML + "ProcedureLogic")
                    ?? throw new Exception("ProcedureLogic element not found in BatchML file.");

            var steps = procLogic.Descendants(batchML + "Step")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    REID = x.Element(batchML + "RecipeElementID")?.Value.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.ID) && !string.IsNullOrEmpty(x.REID))
                .ToList();

            sdt.Columns.Add("ID");
            sdt.Columns.Add("RecipeElementID");

            foreach (var step in steps)
            {
                var dr = sdt.NewRow();
                dr["ID"] = step.ID;
                dr["RecipeElementID"] = step.REID;
                sdt.Rows.Add(dr);
            }

            DisplayDataTable(sdt, "Steps");
        }

        private void DisplayLinkTable(DataTable ldt, XNamespace batchML, XNamespace customNS)
        {
            var procLogic = masterRecipe.Element(batchML + "ProcedureLogic")
                    ?? throw new Exception("ProcedureLogic element not found in BatchML file.");

            var links = procLogic.Descendants(batchML + "Link")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    FromID = x.Element(batchML + "FromID")?.Element(batchML + "FromIDValue")?.Value.Trim(),
                    ToID = x.Element(batchML + "ToID")?.Element(batchML + "ToIDValue")?.Value.Trim(),
                    Duration = x.Element(batchML + "Extension")?.Descendants(customNS + "Duration").FirstOrDefault()?.Value
                })
                .Where(x => !string.IsNullOrEmpty(x.ID)
                && !string.IsNullOrEmpty(x.FromID)
                && !string.IsNullOrEmpty(x.ToID)
                && !string.IsNullOrEmpty(x.Duration))
                .ToList();

            ldt.Columns.Add("ID");
            ldt.Columns.Add("FromID");
            ldt.Columns.Add("ToID");
            ldt.Columns.Add("Duration");

            foreach (var link in links)
            {
                var dr = ldt.NewRow();
                dr["ID"] = link.ID;
                dr["FromID"] = link.FromID;
                dr["ToID"] = link.ToID;
                dr["Duration"] = (link.Duration != null) ? $"{XmlConvert.ToTimeSpan(link.Duration).TotalMinutes} min" : "N/A"; // ISO 8601 to minutes to string via its operator if not null
                ldt.Rows.Add(dr);
            }
            DisplayDataTable(ldt, "Links");
        }
    }
}