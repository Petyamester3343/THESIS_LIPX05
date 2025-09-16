using Microsoft.Win32;
using Microsoft.VisualBasic;

using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

using Thesis_LIPX05.Util;

using static Thesis_LIPX05.Util.SGraph;
using static Thesis_LIPX05.Util.Gantt;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool isFileLoaded = false;

        private XElement masterRecipe;

        private readonly XNamespace
            batchML,
            customNS;

        private readonly DataTable
            masterTable,
            recipeElementTable,
            stepTable,
            linkTable;

        private List<GanttItem> ganttData;
        private List<DataTable> solutionsList;

        private readonly List<CustomSolver> customSolvers;

        private readonly Dictionary<string, Action> solvers;

        private readonly string customSolverPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Y0KAI_TaskScheduler", "custom_solvers.json");

        private double zoom;
        private const double baseTimeScale = 10.0;

        private static readonly JsonSerializerOptions CachedOptions = new() { WriteIndented = true };

        public MainWindow()
        {
            solvers = new()
            {
                {  "Heuristic", SolveHeuristically },
                { "BnB", SolveWithBnB },
                { "Genetic", SolveWithGenetic }
            };
            masterRecipe = new("MasterRecipe");
            batchML = XNamespace.Get("http://www.wbf.org/xml/BatchML-V02");
            customNS = XNamespace.Get("http://lipx05.y0kai.com/batchml/custom");
            masterTable = new("Master Recipe Table");
            recipeElementTable = new("Recipe Element Table");
            stepTable = new("Steps Table");
            linkTable = new("Links Table");
            ganttData = [];
            zoom = 1;
            InitializeComponent();

            customSolvers = [];
            LoadCustomSolvers();
            BuildSolverMenu(SolverMenu);

            solutionsList = [];
        }

        // dynamicaly building the solver menu
        private void BuildSolverMenu(MenuItem solverMenu)
        {
            SolverMenu.Items.Clear();
            foreach (var solver in solvers)
            {
                var item = new MenuItem
                {
                    Header = solver.Key,
                    Tag = solver.Key
                };
                item.Click += (sender, e) => solver.Value();
                solverMenu.Items.Add(item);
            }

            solverMenu.Items.Add(new Separator());

            foreach (var cs in customSolvers) AddCustomSolverMenuItem(cs);

            solverMenu.Items.Add(new Separator());

            var addSolverItem = new MenuItem
            {
                Header = "Add Custom Solver...",
            };
            addSolverItem.Click += AddCustomSolver_Click;
            solverMenu.Items.Add(addSolverItem);
        }

        // Event handler for the MainWindow closing event
        // Checks for unsaved modifications in the data tables
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (masterTable.GetChanges() is not null ||
                recipeElementTable.GetChanges() is not null ||
                stepTable.GetChanges() is not null ||
                linkTable.GetChanges() is not null)
            {
                var res = MessageBox.Show("Do you wish to save changes before exiting?", "Confirm Exit", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                switch (res)
                {
                    case MessageBoxResult.Yes:
                        SaveFile_Click(sender, new());
                        var res2 = MessageBox.Show("Do you wish to save your custom solvers?", "Confirm Save", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                        if (res2 is MessageBoxResult.Yes) SaveCustomSolvers();
                        Application.Current.Shutdown();
                        break;
                    case MessageBoxResult.No:
                        Application.Current.Shutdown();
                        break;
                    case MessageBoxResult.Cancel:
                        e.Cancel = true; // Cancel the closing event
                        break;
                }
            }
            else Application.Current.Shutdown();
        }

        // Event handler for the Exit menu item click event
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            switch (masterTable.GetChanges() is not null || recipeElementTable.GetChanges() is not null || stepTable.GetChanges() is not null || linkTable.GetChanges() is not null)
            {
                case true: MainWindow_Closing(sender, new()); break;
                case false: Application.Current.Shutdown(); break;
            }
        }

        // Calls the heursitic solver
        private void SolveHeuristically()
        {
            var heurOpt = new HeuristicOptimizer(GetNodes(), GetEdges());
            var heurPath = heurOpt.Optimize();

            ganttData = BuildChartFromPath(heurPath, GetEdges());
            double heurTotalTime = ganttData.Max(x => x.Start + x.Duration);
            int heurRowC = ganttData.Count;
            double heurTimeScale = baseTimeScale * zoom;

            Render(GanttCanvas, ganttData, heurTimeScale);
            DrawRuler(RulerCanvas, GanttCanvas, heurTotalTime, heurTimeScale);
        }

        // Calls the Branch and Bound solver
        private void SolveWithBnB()
        {
            var bnbOpt = new BnBOptimizer(GetNodes(), GetEdges());
            var bnbPath = bnbOpt.Optimize();

            ganttData = BuildChartFromPath(bnbPath, GetEdges());
            double bnbTotalTime = ganttData.Max(x => x.Start + x.Duration);
            int bnbRowC = ganttData.Count;
            double bnbTimeScale = baseTimeScale * zoom;

            Render(GanttCanvas, ganttData, bnbTimeScale);
            DrawRuler(RulerCanvas, GanttCanvas, bnbTotalTime, bnbTimeScale);
        }

        // Calls the Genetic Algorithm solver
        private void SolveWithGenetic()
        {
            var gaOpt = new GeneticOptimizer(GetNodes(), GetEdges());
            var gaPath = gaOpt.Optimize();

            ganttData = BuildChartFromPath(gaPath, GetEdges());
            double gaTotalTime = ganttData.Max(x => x.Start + x.Duration);
            int gaRowC = ganttData.Count;
            double gaTimeScale = baseTimeScale * zoom;

            Render(GanttCanvas, ganttData, gaTimeScale);
            DrawRuler(RulerCanvas, GanttCanvas, gaTotalTime, gaTimeScale);
        }

        // Event handler for the Solve menu item click event
        // This method checks which solver was selected and calls the appropriate method
        private void SolveClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                switch (menuItem.Tag.ToString())
                {
                    case "Heuristic":
                        {
                            if (SGraphCanvas.Children.Count == 0)
                            {
                                MessageBox.Show("No S-Graph to solve!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            SolveHeuristically();
                            GanttCanvas.Tag = menuItem.Tag;
                            break;
                        }
                    case "BnB":
                        {
                            if (SGraphCanvas.Children.Count == 0)
                            {
                                MessageBox.Show("No S-Graph to solve!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            SolveWithBnB();
                            GanttCanvas.Tag = menuItem.Tag;
                            break;
                        }
                    case "Genetic":
                        {
                            if (SGraphCanvas.Children.Count == 0)
                            {
                                MessageBox.Show("No S-Graph to solve!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            SolveWithGenetic();
                            GanttCanvas.Tag = menuItem.Tag;
                            break;
                        }
                    default:
                        {
                            MessageBox.Show("Unknown solve method selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            GanttCanvas.Tag = "";
                            break;
                        }
                }
            }
        }

        // Event handler for the About menu item click event (only a static, disposable window)
        private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow().Show();

        // Event handler for the zoom slider's PreviewMouseDown event
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

        // Event handler for the zoom slider's ValueChanged event
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

            Render(GanttCanvas, ganttData, scale);
            DrawRuler(RulerCanvas, GanttCanvas, totalTime, scale);
        }

        // Event handler for opening a file
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                DefaultExt = ".xml",
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                LoadBatchML(dlg.FileName);
                isFileLoaded = true;
            }
        }

        // Event handler for saving a file
        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (!isFileLoaded || masterRecipe is null)
            {
                MessageBox.Show("No BatchML file loaded to save!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var menuItem = sender as MenuItem;

            if (menuItem?.Tag.ToString() == "Exit" || menuItem?.Tag.ToString() == "Close")
            {
                MessageBox.Show("Please save changes before exporting.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var saveDlg = new SaveFileDialog
            {
                DefaultExt = ".xml",
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*"
            };

            if (saveDlg.ShowDialog() == true)
            {
                try
                {
                    masterRecipe.Document?.Save(saveDlg.FileName);
                    MessageBox.Show($"BatchML file saved as {saveDlg.FileName}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving BatchML file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Event handler for the S-Graph renderer button click event
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

        // A helper method to flush the containers, clean the canvases and data tables in one go
        private void Purge()
        {
            SGraphCanvas.Children.Clear();
            GanttCanvas.Children.Clear();
            RulerCanvas.Children.Clear();

            GetNodes().Clear();
            GetEdges().Clear();

            masterTable.Clear();
            recipeElementTable.Clear();
            stepTable.Clear();
            linkTable.Clear();

            foreach (DataTable dt in solutionsList) dt.Clear();

            for (int i = MainTab.Items.Count - 1; i >= 0; i--)
            {
                TabItem? tab = MainTab.Items[i] as TabItem;
                if (tab?.Tag?.ToString()?.Equals("MasterRecipe") is true)
                {
                    MainTab.Items.RemoveAt(i);
                    continue;
                }
                if (tab?.Tag?.ToString()?.Equals("RecipeElements") is true)
                {
                    MainTab.Items.RemoveAt(i);
                    continue;
                }
                if (tab?.Tag?.ToString()?.Equals("Steps") is true)
                {
                    MainTab.Items.RemoveAt(i);
                    continue;
                }
                if (tab?.Tag?.ToString()?.Equals("Links") is true)
                {
                    MainTab.Items.RemoveAt(i);
                    continue;
                }
                if (tab?.Tag?.ToString()?.Contains("Solution") is true)
                {
                    MainTab.Items.RemoveAt(i);
                    continue;
                }
            }
        }

        // Draws an example S-Graph with 9 equipment nodes and 3 product nodes (in case no file is loaded)
        private void DrawExampleGraph()
        {
            Purge();

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

            for (int x = 1; x < GetNodes().Count; x++) // Example edges
                AddEdge($"Eq{x}", (x % 3 != 0) ? $"Eq{x + 1}" : $"Prod{x / 3}", new Random().Next(5, 45));

            Render(SGraphCanvas, 4);
        }

        // Builds the S-Graph from the loaded BatchML file
        protected void BuildSGraphFromXml()
        {
            Purge();

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
                    if (durEl is not null)
                    {
                        try
                        {
                            cost = XmlConvert.ToTimeSpan(durEl).TotalMinutes; // ISO 8601 -> minutes
                        }
                        catch
                        {
                            cost = double.PositiveInfinity; // Invalid duration, set cost to positive infinity
                        }
                    }

                    return new { fromID, toID, cost };
                })
                .Where(x => !string.IsNullOrEmpty(x.fromID) && !string.IsNullOrEmpty(x.toID) && !double.IsNaN(x.cost))
                .ToList();

            foreach (var link in links)
            {

                bool fromExists = GetNodes().TryGetValue(link.fromID!, out var fromNode);
                bool toExists = GetNodes().TryGetValue(link.toID!, out var toNode);
                // For debugging purposes
                /*
                MessageBox.Show($"From Node: {fromExists}, ID: {link.fromID}");
                MessageBox.Show($"To Node: {toExists}, ID: {link.toID}");
                */
                GetEdges().Add(new()
                {
                    From = fromNode!,
                    To = toNode!,
                    Cost = link.cost
                });
            }

            Render(SGraphCanvas, 3);
        }

        // Loads the BatchML file and populates the data tables
        private void LoadBatchML(string path)
        {
            try
            {
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

        // Handler event for closing a BatchML file
        private void CloseFile_Click(object sender, RoutedEventArgs e)
        {
            if (masterTable.GetChanges() is not null || recipeElementTable.GetChanges() is not null || stepTable.GetChanges() is not null || linkTable.GetChanges() is not null) SaveFile_Click(sender, e);
            else
            {
                try
                {
                    Purge();
                    isFileLoaded = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error closing BatchML: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Creates and displays the data tables
        private void DisplayDataTable(DataTable dt, string tag)
        {
            var grid = new DataGrid
            {
                ItemsSource = dt.DefaultView,
                AutoGenerateColumns = true,
                IsReadOnly = false,
                CanUserAddRows = true
            };

            var container = new Grid();
            container.Children.Add(grid);

            var tab = new TabItem
            {
                Header = dt.TableName,
                Content = container,
                Tag = tag
            };
            MainTab.Items.Insert(0, tab);
            MainTab.SelectedIndex = 0;
        }

        // Exports an S-Graph to a JPEG file
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
                else JPEGExporter.ExportOneCanvas(SGraphCanvas, "S-Graph");
            }
            else MessageBox.Show("Please select the \"S-Graph\" tab to export the S-Graph!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Exports the whole Gantt chart to a JPEG file
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
                else JPEGExporter.ExportMultipleCanvases(RulerCanvas, GanttCanvas, "Gantt Chart");
            }
            else MessageBox.Show("Please select the \"Gantt chart\" tab to export the Gantt chart!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Displays the master recipe table
        private void DisplayMasterTable(DataTable mdt, XNamespace batchML)
        {
            var id = masterRecipe.Element(batchML + "ID")?.Value;
            var ver = masterRecipe.Element(batchML + "Version")?.Value;
            var desc = masterRecipe.Elements(batchML + "Description").LastOrDefault()?.Value;

            var header = masterRecipe?.Element(batchML + "Header");

            var prodID = header?.Element(batchML + "ProductID")?.Value;
            var prodName = header?.Element(batchML + "ProductName")?.Value;
            var batchSize = header?.Element(batchML + "BatchSize");

            var nominal = batchSize?.Element(batchML + "Nominal")?.Value;
            var min = batchSize?.Element(batchML + "Min")?.Value;
            var max = batchSize?.Element(batchML + "Max")?.Value;
            var unit = batchSize?.Element(batchML + "UnitOfMeasure")?.Value;

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

            mdt.RowChanged += MasterTable_RowChanged;

            DisplayDataTable(mdt, "MasterRecipe");
        }

        // Event handler in case of changes in the master recipe table
        private void MasterTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            masterRecipe.SetElementValue(batchML + "ID", e.Row["RecipeID"]);
            masterRecipe.SetElementValue(batchML + "Version", e.Row["Version"]);

            var desc = masterRecipe.Elements(batchML + "Description").LastOrDefault();
            if (desc != null) desc.Value = e.Row["Description"].ToString() ?? string.Empty;

            var header = masterRecipe.Element(batchML + "Header");
            if (header != null)
            {
                header.SetElementValue(batchML + "ProductID", e.Row["ProductID"]);
                header.SetElementValue(batchML + "ProductName", e.Row["ProductName"]);
                var batchSize = header.Element(batchML + "BatchSize");
                if (batchSize != null)
                {
                    batchSize.SetElementValue(batchML + "Nominal", e.Row["NominalBatchSize"]);
                    batchSize.SetElementValue(batchML + "Min", e.Row["MinBatchSize"]);
                    batchSize.SetElementValue(batchML + "Max", e.Row["MaxBatchSize"]);
                    batchSize.SetElementValue(batchML + "UnitOfMeasure", e.Row["UnitOfMeasure"]);
                }
            }
        }

        // Displays the recipe element table
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


            if (recipeElements != null)
            {
                foreach (var re in recipeElements)
                {
                    var dr = redt.NewRow();
                    dr["ID"] = re.ID;
                    dr["Description"] = re.Desc;
                    redt.Rows.Add(dr);
                }
                DisplayDataTable(redt, "RecipeElements");
            }
            else
            {
                MessageBox.Show("No Recipe Elements found in the BatchML file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            redt.RowChanged += RecipeElementTable_RowChanged;
            redt.RowDeleted += RecipeElementTable_RowDeleted;
        }

        // Event handler for deletions in the recipe element table
        private void RecipeElementTable_RowDeleted(object sender, DataRowChangeEventArgs e)
        {
            var reEl = masterRecipe.Descendants(batchML + "RecipeElement")
                .FirstOrDefault(x => x.Element(batchML + "ID")?.Value.Trim() == e.Row["ID"]
                .ToString());
            reEl?.Remove();
        }

        // Event handler for changes in the recipe element table
        private void RecipeElementTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            switch (e.Row.RowState)
            {
                case DataRowState.Added:
                    var newRE = new XElement(batchML + "RecipeElement",
                    new XElement(batchML + "ID", e.Row["ID"]),
                    new XElement(batchML + "Description", e.Row["Description"]));
                    masterRecipe.Add(newRE);
                    break;
                case DataRowState.Modified:
                    var reEl = masterRecipe.Descendants(batchML + "RecipeElement")
                    .FirstOrDefault(x => x.Element(batchML + "ID")?.Value.Trim() == e.Row["ID"]
                    .ToString());
                    reEl?.SetElementValue(batchML + "Description", e.Row["Description"]);
                    break;
            }
        }

        // Displays the steps table
        private void DisplayStepTable(DataTable sdt, XNamespace batchML)
        {
            var steps = masterRecipe.Element(batchML + "ProcedureLogic")?.Descendants(batchML + "Step")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    REID = x.Element(batchML + "RecipeElementID")?.Value.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.ID) && !string.IsNullOrEmpty(x.REID))
                .ToList();

            sdt.Columns.Add("ID");
            sdt.Columns.Add("RecipeElementID");

            if (steps != null)
            {
                foreach (var step in steps)
                {
                    var dr = sdt.NewRow();
                    dr["ID"] = step.ID;
                    dr["RecipeElementID"] = step.REID;
                    sdt.Rows.Add(dr);
                }
                DisplayDataTable(sdt, "Steps");
            }
            else
            {
                MessageBox.Show("No Steps found in the BatchML file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            sdt.RowChanged += StepTable_RowChanged;
            sdt.RowDeleted += StepTable_RowDeleted;
        }

        // Event handler for deletions in the steps table
        private void StepTable_RowDeleted(object sender, DataRowChangeEventArgs e)
        {
            var stepEL = masterRecipe.Descendants(batchML + "Step")
                .FirstOrDefault(x => x.Element(batchML + "ID")?.Value.Trim() == e.Row["ID"]
                .ToString());
            stepEL?.Remove();
        }

        // Event handler for changes in the steps table
        private void StepTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            switch (e.Row.RowState)
            {
                case DataRowState.Added:
                    var newStep = new XElement(batchML + "Step",
                    new XElement(batchML + "ID", e.Row["ID"]),
                    new XElement(batchML + "RecipeElementID", e.Row["RecipeElementID"]));
                    masterRecipe.Add(newStep);
                    break;
                case DataRowState.Modified:
                    var stepEl = masterRecipe.Descendants(batchML + "Step")
                    .FirstOrDefault(x => x.Element(batchML + "ID")?.Value.Trim() == e.Row["ID"]
                    .ToString());
                    stepEl?.SetElementValue(batchML + "RecipeElementID", e.Row["RecipeElementID"]);
                    break;
            }
        }

        // Event handler for adding a custom solver
        private void AddCustomSolver_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Applications (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Select Custom Solver Executable"
            };

            if (dlg.ShowDialog() is true)
            {
                string solverPath = dlg.FileName;

                var input = Interaction.InputBox(
                    "Enter a name for the custom solver:",
                    "Custom Solver Name",
                    Path.GetFileNameWithoutExtension(solverPath)
                    );

                if (!string.IsNullOrWhiteSpace(input))
                {
                    if (customSolvers.Any(s => s.Name.Equals(input, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show("A solver with this name already exists!", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var solver = new CustomSolver
                    {
                        Name = input.Trim(),
                        Path = solverPath
                    };
                    customSolvers.Add(solver);
                    SaveCustomSolvers();

                    AddCustomSolverMenuItem(solver);
                }
            }
        }

        // Saves custom solvers to a JSON file
        private void SaveCustomSolvers()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(customSolverPath)!);
                string json = JsonSerializer.Serialize(customSolvers, CachedOptions);
                File.WriteAllText(customSolverPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving custom solvers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Loads custom solvers from a JSON file
        private void LoadCustomSolvers()
        {
            try
            {
                if (File.Exists(customSolverPath))
                {
                    string json = File.ReadAllText(customSolverPath);
                    var loaded = JsonSerializer.Deserialize<List<CustomSolver>>(json);
                    if (loaded != null)
                    {
                        customSolvers.Clear();
                        customSolvers.AddRange(loaded);
                        foreach (var solver in customSolvers) AddCustomSolverMenuItem(solver);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading custom solvers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Adds a custom solver to the Solver menu
        private void AddCustomSolverMenuItem(CustomSolver solver)
        {
            var item = new MenuItem
            {
                Header = solver.Name,
                Tag = solver.Name
            };
            item.Click += (sender, e) => RunExtSolver(solver.Path);
            SolverMenu.Items.Add(item);
        }

        // Runs the external solver executable
        private static void RunExtSolver(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running external solver:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateSolutionTableBtn_Click(object sender, RoutedEventArgs e)
        {
            if (GanttCanvas.Children.Count != 0) DisplaySolutionAsTable(ganttData);
            else MessageBox.Show("No Gantt chart to create solution table from!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void DisplaySolutionAsTable(List<GanttItem> data)
        {
            var execSolver = GanttCanvas.Tag?.ToString() ?? "";
            var sdt = new DataTable
            {
                TableName = $"Solution {execSolver}"
            };


            sdt.Columns.Add("TaskID");
            sdt.Columns.Add("StartTime (min)");
            sdt.Columns.Add("Duration (min)");
            sdt.Columns.Add("EndTime (min)");

            foreach (var item in data)
            {
                var dr = sdt.NewRow();
                dr["TaskID"] = item.ID;
                dr["StartTime (min)"] = item.Start;
                dr["Duration (min)"] = item.Duration;
                dr["EndTime (min)"] = item.Start + item.Duration;
                sdt.Rows.Add(dr);
            }

            solutionsList.Add(sdt);
            DisplayDataTable(sdt, sdt.TableName);
        }

        // Displays the links table
        private void DisplayLinkTable(DataTable ldt, XNamespace batchML, XNamespace customNS)
        {
            var links = masterRecipe.Element(batchML + "ProcedureLogic")?.Descendants(batchML + "Link")
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

            if (links != null)
            {
                foreach (var link in links)
                {
                    var dr = ldt.NewRow();
                    dr["ID"] = link.ID;
                    dr["FromID"] = link.FromID;
                    dr["ToID"] = link.ToID;

                    // ISO 8601 to minutes to string via interpolation if not null, otherwise "N/A"
                    dr["Duration"] = (link.Duration != null)
                        ? $"{XmlConvert.ToTimeSpan(link.Duration).TotalMinutes} min"
                        : "N/A";

                    ldt.Rows.Add(dr);
                }
                DisplayDataTable(ldt, "Links");
            }
            else
            {
                MessageBox.Show("No Links found in the BatchML file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ldt.RowChanged += LinkTable_RowChanged;
            ldt.RowDeleted += LinkTable_RowDeleted;
        }

        // Event handler for deletions in the links table
        private void LinkTable_RowDeleted(object sender, DataRowChangeEventArgs e)
        {
            var linkEL = masterRecipe.Descendants(batchML + "Link")
                .FirstOrDefault(x =>
                    x.Element(batchML + "FromID")?.Element(batchML + "FromIDValue")?.Value ==
                        e.Row["FromID", DataRowVersion.Original]?.ToString() &&
                    x.Element(batchML + "ToID")?.Element(batchML + "ToIDValue")?.Value ==
                        e.Row["ToID", DataRowVersion.Original]?.ToString());
            linkEL?.Remove();
        }

        // Event handler for changes in the links table
        private void LinkTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            string rawDur = e.Row["Duration"]?.ToString() ?? "0";

            switch (e.Row.RowState)
            {
                case DataRowState.Added:
                    var newLINK = new XElement(batchML + "Link",
                    new XElement(batchML + "ID", e.Row["ID"]),
                    new XElement(batchML + "FromID",
                        new XElement(batchML + "FromIDValue", e.Row["FromID"])),
                    new XElement(batchML + "ToID",
                        new XElement(batchML + "ToIDValue", e.Row["ToID"])),
                    new XElement(batchML + "Extension",
                        new XElement(customNS + "Duration", ConvertToISO8601(rawDur))));
                    masterRecipe.Add(newLINK);
                    break;
                case DataRowState.Modified:
                    var linkEL = masterRecipe.Descendants(batchML + "Link")
                    .FirstOrDefault(x =>
                        x.Element(batchML + "FromID")?.Element(batchML + "FromIDValue")?.Value == e.Row["FromID"].ToString() &&
                        x.Element(batchML + "ToID")?.Element(batchML + "ToIDValue")?.Value == e.Row["ToID"].ToString());
                    linkEL?.Element(batchML + "Extension")?
                            .SetElementValue(customNS + "Duration", ConvertToISO8601(rawDur));
                    break;
            }
        }

        // A helper method, which converts a raw duration string to ISO 8601 (PT) format
        private static string ConvertToISO8601(string raw)
        {
            // if it already looks like an ISO 8601 duration, return it as is (ideally, that is)
            if (raw.StartsWith("PT", StringComparison.OrdinalIgnoreCase) &&
                (raw.Contains('Y', StringComparison.OrdinalIgnoreCase) ||
                raw.Contains('M', StringComparison.OrdinalIgnoreCase) ||
                raw.Contains('W', StringComparison.OrdinalIgnoreCase) ||
                raw.Contains('D', StringComparison.OrdinalIgnoreCase) ||
                raw.Contains('H', StringComparison.OrdinalIgnoreCase) ||
                raw.Contains('M', StringComparison.OrdinalIgnoreCase) ||
                raw.Contains('S', StringComparison.OrdinalIgnoreCase)))
                return raw;

            // try parse as HH:MM
            if (TimeSpan.TryParse(raw, out TimeSpan ts)) return $"PT{(ts.Hours > 0 ? $"{ts.Hours}H" : "")}{(ts.Minutes > 0 ? $"{ts.Minutes}M" : "")}";

            // try parse as total minutes
            if (double.TryParse(raw, out double minutes))
            {
                int hrs = (int)(minutes / 60);
                int mins = (int)(minutes % 60);

                return $"PT{(hrs > 0 ? $"{hrs}H" : "")}{(mins > 0 ? $"{mins}M" : "")}";
            }

            // default return value if parsing fails
            return "PT0M";
        }
    }
}