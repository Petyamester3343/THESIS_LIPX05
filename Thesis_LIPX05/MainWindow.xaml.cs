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
using System.Text;
using System.Text.RegularExpressions;

using Thesis_LIPX05.Util;

using static Thesis_LIPX05.Util.Gantt;
using static Thesis_LIPX05.Util.LogManager;
using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly XNamespace batchML, customNS;
        private readonly DataTable masterTable, recipeElementTable, stepTable, linkTable;
        private readonly string customSolverPath = Path.Combine
            (
                Environment.GetFolderPath
                (
                    Environment.SpecialFolder.ApplicationData
                ),
                "Y0KAI_TaskScheduler",
                "custom_solvers.json"
            );

        private readonly List<DataTable> solutionsList;
        private readonly List<CustomSolver> customSolvers;
        private readonly List<string> solvers;
        private readonly Dictionary<string, TableMapper> mappings;

        private XElement masterRecipe;
        private double zoom;
        private bool isFileLoaded = false, SGraphExists, isFileModified = false;
        private string currentFilePath = string.Empty;

        private List<GanttItem> ganttData;

        private const double BaseTimeScale = 10.0;
        private const string
            IntegratedHeurSolver = "Heuristic",
            IntegratedBnBSolver = "Branch and Bound",
            IntegratedGenSolver = "Genetic";

        [GeneratedRegex(@"^PT(?:(\d+)H)?(?:(\d|[1-5]\d)M)?(?:([0-5]\d)S)?$", RegexOptions.IgnoreCase, "hu-HU")]
        private static partial Regex ISO8601Format();
        private static readonly JsonSerializerOptions CachedOptions = new() { WriteIndented = true };

        private static LogManager? logger;

        public MainWindow()
        {
            logger = new();

            masterRecipe = new("MasterRecipe");
            batchML = XNamespace.Get("http://www.wbf.org/xml/BatchML-V02");
            customNS = XNamespace.Get("http://lipx05.y0kai.com/batchml/custom");

            masterTable = new("Master Recipe Table");
            recipeElementTable = new("Recipe Element Table");
            stepTable = new("Steps Table");
            linkTable = new("Links Table");

            ganttData = [];
            zoom = 1;

            mappings = InitMappings();

            solvers = [IntegratedHeurSolver, IntegratedBnBSolver, IntegratedGenSolver];
            customSolvers = [];
            solutionsList = [];

            InitializeComponent();

            LoadCustomSolversFromJSON();
            BuildSolverMenu(SolverMenu);

            ManageFileHandlers();

            logger.Log(LogSeverity.INFO, "Initialization complete!");
        }

        // Enables or disables the saving and closing by checking is a file is loaded
        private void ManageFileHandlers()
        {
            foreach (var mi in new[] { SaveFileMenuItem, CloseFileMenuItem })
            {
                mi.IsEnabled = isFileLoaded;
            }
        }

        public static LogManager GetLogger()
        {
            try
            {
                if (logger is not null) return logger;
                else throw new Exception("Logger not initialized!");
            }
            catch
            {
                logger?.Log(LogSeverity.WARNING, "Logger not initialized! Creating new one...");
                return new();
            }
        }

        // Initializes the mappings for XML elements to DataTable columns
        private static Dictionary<string, TableMapper> InitMappings() => new()
            {
                {
                    "Links", new()
                    {
                        ParentElement = "Link",
                        KeyCols = ["ID"],
                        Col2El = new()
                        {
                            { "ID", "ID" },
                            { "From", "FromIDValue" },
                            { "To", "ToIDValue" },
                            { "Duration", "Duration" }
                        }
                    }
                },
                {
                    "Steps", new()
                    {
                        ParentElement = "Step",
                        KeyCols = ["ID"],
                        Col2El = new()
                        {
                            { "ID", "ID" },
                            { "RecipeElementID", "RecipeElementID" },
                        }
                    }
                },
                {
                    "RecipeElements", new()
                    {
                        ParentElement = "RecipeElement",
                        KeyCols = ["ID"],
                        Col2El = new()
                        {
                            { "ID", "ID" },
                            { "Description", "Description" },
                        }
                    }
                }
            };

        private static void AddSeparator(MenuItem solverMenu) => solverMenu.Items.Add(new Separator());

        // dynamicaly building the solver menu
        private void BuildSolverMenu(MenuItem solverMenu)
        {
            try
            {
                SolverMenu.Items.Clear();
                foreach (var solver in solvers)
                {
                    var item = new MenuItem
                    {
                        Header = solver,
                        Tag = solver,
                        IsEnabled = SGraphExists
                    };
                    item.Click += SolveClick;
                    solverMenu.Items.Add(item);
                    logger?.Log(LogSeverity.INFO, $"{item.Tag} solver added!");
                }
                AddSeparator(solverMenu);

                foreach (var cs in customSolvers)
                {
                    AddCustomSolverMenuItem(cs);
                    logger?.Log(LogSeverity.INFO, $"{cs.Name} ({cs.TypeID} type) solver added!");
                }
                AddSeparator(solverMenu);

                var addSolverItem = new MenuItem
                {
                    Header = "Add Custom Solver...",
                    IsEnabled = true
                };
                addSolverItem.Click += AddCustomSolver_Click;
                solverMenu.Items.Add(addSolverItem);

                logger?.Log(LogSeverity.INFO, "Solver menu successfully built!");
            }
            catch (Exception ex)
            {
                logger?.Log(LogSeverity.INFO, $"Error building solver menu: {ex.Message}");
                MessageBox.Show($"Error building solver menu: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Event handler for the MainWindow closing event
        // Checks for unsaved modifications in the data tables
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (isFileModified && (masterTable.GetChanges() is not null ||
                recipeElementTable.GetChanges() is not null ||
                stepTable.GetChanges() is not null ||
                linkTable.GetChanges() is not null))
            {
                logger?.Log(LogSeverity.WARNING, "App is about to be shut down with unsaved changes! Prompting user to save before exiting!");
                var res = MessageBox.Show("Do you wish to save changes before exiting?", "Confirm Exit", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                switch (res)
                {
                    case MessageBoxResult.Yes:
                        {
                            logger?.Log(LogSeverity.INFO, "User opted to save changes in the datatables to the BatchML file before exiting!");
                            SaveFile_Click(sender, new());
                            var res2 = MessageBox.Show("Do you wish to save your custom solvers?", "Confirm Save", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                            if (res2 is MessageBoxResult.Yes)
                            {
                                SaveCustomSolvers2JSON();
                                logger?.Log(LogSeverity.INFO, "Shutting down after saving custom solvers and the changes made to the datatable...");
                            }
                            else logger?.Log(LogSeverity.INFO, "Shutting down after saving the changes made to the datatable, without saving custom solvers...");
                            Application.Current.Shutdown();
                            break;
                        }
                    case MessageBoxResult.No:
                        {
                            logger?.Log(LogSeverity.WARNING, "User opted to exit without saving changes in the datatables to the BatchML file before exiting!");
                            var res3 = MessageBox.Show("Do you wish to save your custom solvers?", "Confirm Save", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                            if (res3 is MessageBoxResult.Yes)
                            {
                                SaveCustomSolvers2JSON();
                                logger?.Log(LogSeverity.WARNING, "Shutting down with saving only the custom solvers...");
                            }
                            else logger?.Log(LogSeverity.WARNING, "Shutting down without saving anything...");
                            Application.Current.Shutdown();
                            break;
                        }
                    case MessageBoxResult.Cancel:
                        e.Cancel = true; // Cancel the closing event
                        logger?.Log(LogSeverity.INFO, $"Application exit cancelled by user!");
                        break;
                }
            }
            else
            {
                logger?.Log(LogSeverity.INFO, "No changes were made, exiting app...");
                Application.Current.Shutdown();
            }
        }

        // Event handler for the Exit menu item click event
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            switch (isFileModified &&
                (masterTable.GetChanges() is not null ||
                recipeElementTable.GetChanges() is not null ||
                stepTable.GetChanges() is not null ||
                linkTable.GetChanges() is not null))
            {
                case true:
                    logger?.Log(LogSeverity.WARNING, "Unsaved changes detected, prompting user to save before exiting!");
                    MainWindow_Closing(sender, new(false));
                    break;
                case false:
                    logger?.Log(LogSeverity.INFO, "No changes were made, shutting down...");
                    Application.Current.Shutdown();
                    break;
            }
        }

        // Calls the heursitic solver
        private void SolveHeuristically()
        {
            var heurOpt = new HeuristicOptimizer(GetNodes(), GetEdges());
            var heurPath = heurOpt.Optimize();

            ganttData = BuildChartFromPath(heurPath, GetEdges());
            logger?.Log(LogSeverity.INFO, $"Gantt data built with {ganttData.Count} items!");
            double heurTotalTime = ganttData.Max(x => x.Start + x.Duration);
            int heurRowC = ganttData.Count;
            double heurTimeScale = BaseTimeScale * zoom;

            Render(GanttCanvas, ganttData, heurTimeScale);
            DrawRuler(RulerCanvas, GanttCanvas, heurTotalTime, heurTimeScale);
            logger?.Log(LogSeverity.INFO, "Heuristic solver finished!");
        }

        // Calls the Branch and Bound solver
        private void SolveWithBnB()
        {
            var bnbOpt = new BnBOptimizer(GetNodes(), GetEdges());
            var bnbPath = bnbOpt.Optimize();

            ganttData = BuildChartFromPath(bnbPath, GetEdges());
            logger?.Log(LogSeverity.INFO, $"Gantt data built with {ganttData.Count} items!");
            double bnbTotalTime = ganttData.Max(x => x.Start + x.Duration);
            int bnbRowC = ganttData.Count;
            double bnbTimeScale = BaseTimeScale * zoom;

            Render(GanttCanvas, ganttData, bnbTimeScale);
            DrawRuler(RulerCanvas, GanttCanvas, bnbTotalTime, bnbTimeScale);
            logger?.Log(LogSeverity.INFO, "Branch & Bound solver finished!");
        }

        // Calls the Genetic Algorithm solver
        private void SolveWithGenetic()
        {
            var gaOpt = new GeneticOptimizer(GetNodes(), GetEdges());
            var gaPath = gaOpt.Optimize();

            ganttData = BuildChartFromPath(gaPath, GetEdges());
            logger?.Log(LogSeverity.INFO, $"Gantt data built with {ganttData.Count} items!");
            double gaTotalTime = ganttData.Max(x => x.Start + x.Duration);
            int gaRowC = ganttData.Count;
            double gaTimeScale = BaseTimeScale * zoom;

            Render(GanttCanvas, ganttData, gaTimeScale);
            DrawRuler(RulerCanvas, GanttCanvas, gaTotalTime, gaTimeScale);
            logger?.Log(LogSeverity.INFO, "Genetic Algorithm solver finished!");
        }

        // Event handler for the Solve menu item click event
        // This method checks which solver was selected and calls the appropriate method
        private void SolveClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                switch (menuItem?.Tag.ToString())
                {
                    case IntegratedHeurSolver:
                        {
                            logger?.Log(LogSeverity.INFO, "Heuristic solver selected!");
                            SolveHeuristically();
                            GanttCanvas.Tag = menuItem?.Tag;
                            break;
                        }
                    case IntegratedBnBSolver:
                        {
                            logger?.Log(LogSeverity.INFO, "Branch & Bound solver selected!");
                            SolveWithBnB();
                            GanttCanvas.Tag = menuItem?.Tag;
                            break;
                        }
                    case IntegratedGenSolver:
                        {
                            logger?.Log(LogSeverity.INFO, "Genetic Algorithm solver selected!");
                            SolveWithGenetic();
                            GanttCanvas.Tag = menuItem?.Tag;
                            break;
                        }
                    default:
                        {
                            var customSolver = customSolvers.FirstOrDefault(cs => cs.Name == menuItem?.Tag.ToString());
                            if (customSolver is null)
                            {
                                logger?.Log(LogSeverity.ERROR, "Custom solver not found!");
                                MessageBox.Show("Custom solver not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            logger?.Log(LogSeverity.INFO, $"{customSolver.Name} custom solver selected!");

                            List<string> launchArgs = [];

                            if (customSolver.TypeID.Equals("Metaheuristic", StringComparison.OrdinalIgnoreCase))
                            {
                                logger?.Log(LogSeverity.INFO,
                                    $"Gathering parameters for {customSolver.Name}");
                                GatherArgs4Metaheur(launchArgs);
                            }
                            else if (customSolver.TypeID.Equals("Deterministic", StringComparison.OrdinalIgnoreCase))
                                logger?.Log(LogSeverity.INFO,
                                    "Deterministic solver selected; skipping numeric parameter gathering...");

                            MessageBoxResult silentRes = MessageBox.Show(
                                "Run solver in silent mode (-s or --silent)? This suppresses detailed logging and improves speed.",
                                "Solver Configuration",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (silentRes == MessageBoxResult.Yes) launchArgs.Add("-s");

                            try
                            {
                                customSolver.Arguments.Clear();
                                customSolver.Arguments.AddRange(launchArgs);

                                StringBuilder argsBuilder = new();
                                foreach (string arg in customSolver.Arguments) argsBuilder.Append(arg).Append(' ');

                                Process proc = new()
                                {
                                    StartInfo = new()
                                    {
                                        FileName = customSolver.Path,
                                        Arguments = argsBuilder.ToString().Trim(),
                                        RedirectStandardInput = true,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        UseShellExecute = false,
                                        CreateNoWindow = argsBuilder.ToString().Contains("-s")
                                    }
                                };
                                proc.Start();

                                {
                                    using StreamWriter writer = proc.StandardInput;
                                    if (writer.BaseStream.CanWrite)
                                    {
                                        foreach (Node node in GetNodes().Values) writer.WriteLine($"NODE {node.ID} {node.Desc}");
                                        foreach (Edge edge in GetEdges()) writer.WriteLine($"EDGE {edge.From.ID} {edge.To.ID} {edge.Cost}");
                                    }
                                    else
                                    {
                                        logger?.Log(LogSeverity.ERROR,
                                            "Cannot write to custom solver's standard input!");
                                        MessageBox.Show("Cannot write to custom solver's standard input!",
                                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                        return;
                                    }
                                }

                                List<string> output = [];

                                string
                                    stdOut = proc.StandardOutput.ReadToEnd(),
                                    stdErr = proc.StandardError.ReadToEnd();

                                proc.WaitForExit();

                                output.AddRange(stdOut.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries));

                                if (proc.ExitCode != 0)
                                {
                                    if (!string.IsNullOrEmpty(stdErr))
                                    {
                                        logger?.Log(LogSeverity.ERROR, $"Custom solver exited with code {proc.ExitCode}: {stdErr}");
                                        MessageBox.Show($"Custom solver exited with code {proc.ExitCode}:\n{stdErr}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                    return;
                                }

                                List<Node> path = [];
                                foreach (var line in output)
                                {
                                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length == 2 && parts[0].Equals("NODE"))
                                    {
                                        string nodeId = parts[1];
                                        if (GetNodes().TryGetValue(nodeId, out var node)) path.Add(node);
                                    }
                                }
                                if (path.Count == 0)
                                {
                                    logger?.Log(LogSeverity.ERROR, "Custom solver returned an empty path!");
                                    MessageBox.Show("Custom solver returned an empty path!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }

                                ganttData = BuildChartFromPath(path, GetEdges());
                                logger?.Log(LogSeverity.INFO, $"Gantt data built with {ganttData.Count} items!");
                                double totalTime = ganttData.Max(x => x.Start + x.Duration);
                                int rowC = ganttData.Count;
                                double timeScale = BaseTimeScale * zoom;
                                Render(GanttCanvas, ganttData, timeScale);
                                DrawRuler(RulerCanvas, GanttCanvas, totalTime, timeScale);
                                logger?.Log(LogSeverity.INFO, $"{customSolver.Name} custom solver finished!");
                                GanttCanvas.Tag = customSolver.Name;
                            }
                            catch (Exception ex)
                            {
                                logger?.Log(LogSeverity.ERROR, $"Error executing custom solver: {ex.Message}");
                                MessageBox.Show($"Error executing custom solver: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            break;
                        }
                }
            }
        }

        private static void GatherArgs4Metaheur(List<string> launchArgs)
        {
            string inTemp = Interaction.InputBox(
                "Enter Initial Temperature (e.g., 1000.0):",
                "Solver Parameter",
                "1000.0");

            if (double.TryParse(inTemp, out _)) launchArgs.Add(inTemp);
            else return;

            string inCool = Interaction.InputBox(
                "Enter Initial Cooling Rate (e.g., 0.995)",
                "Solver Paramter",
                "0.995");

            if (double.TryParse(inCool, out _)) launchArgs.Add(inCool);
            else return;

            string inIter = Interaction.InputBox(
                "Enter Initial Iteration Rate (e.g., 5000)",
                "Solver Parameter",
                "5000");

            if (int.TryParse(inIter, out _)) launchArgs.Add(inIter);
            else return;
        }

        // Event handler for the About menu item click event (only a static, disposable window)
        private void About_Click(object sender, RoutedEventArgs e)
        {
            new AboutWindow().Show();
            logger?.Log(LogSeverity.INFO, "\"About\" window opened!");
        }

        // Event handler for the zoom slider's PreviewMouseDown event
        private void ZoomSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Slider slider)
            {
                logger?.Log(LogSeverity.ERROR, "Zoom slider sender is not a Slider!");
                return;
            }

            if (e.OriginalSource is FrameworkElement element && element.TemplatedParent is not Thumb)
            {
                Point pos = e.GetPosition(slider);
                double ratio = pos.X / slider.ActualWidth;
                double newValue = slider.Minimum + (slider.Maximum - slider.Minimum) * ratio;
                slider.Value = newValue;
                e.Handled = true; // Prevents slider from moving
                logger?.Log(LogSeverity.INFO, $"Zoom slider clicked at position {pos.X}, setting value to {newValue}!");
            }
        }

        // Event handler for the zoom slider's ValueChanged event
        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ganttData is null || ganttData.Count == 0) return;

            zoom = Math.Pow(2, e.NewValue);
            double scale = BaseTimeScale * zoom;

            double totalTime = ganttData.Max(x => x.Start + x.Duration);
            int rowC = ganttData.Count;

            GanttCanvas.LayoutTransform = Transform.Identity;

            GanttCanvas.Width = totalTime * scale + 100; // +100 for padding
            RulerCanvas.Width = GanttCanvas.Width;

            Render(GanttCanvas, ganttData, scale);
            DrawRuler(RulerCanvas, GanttCanvas, totalTime, scale);

            logger?.Log(LogSeverity.INFO, $"Zoom level changed to {e.NewValue}, scale set to {scale}!");
        }

        private bool HasUnsavedChanges() =>
            isFileLoaded &&
            (masterTable.GetChanges() is not null ||
            recipeElementTable.GetChanges() is not null ||
            stepTable.GetChanges() is not null ||
            linkTable.GetChanges() is not null);

        // Event handler for opening a file
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (isFileLoaded)
            {
                if (HasUnsavedChanges())
                {
                    logger?.Log(LogSeverity.WARNING,
                        "Attempting to open new file with unsaved changes present in the actual one; prompting user!");

                    MessageBoxResult res = MessageBox.Show(
                        "The current BatchML file has unsaved changes. Do you wish to save before loading a new file?",
                        "Unsaved changes detected",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Warning
                        );

                    if (res == MessageBoxResult.Yes) SaveFile_Click(sender, new());
                    else if (res == MessageBoxResult.Cancel)
                    {
                        logger?.Log(LogSeverity.INFO,
                            "New file opening cancelled by user (unsaved changes prompt).");
                        return;
                    }
                }

                Purge();
                PurgeDataTables();
            }

            logger?.Log(LogSeverity.INFO, "Open file dialog initiated...");
            OpenFileDialog dlg = new()
            {
                DefaultExt = ".xml",
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                currentFilePath = dlg.FileName;
                LoadBatchML(currentFilePath);

                isFileLoaded = true;
                isFileModified = false;
                ManageFileHandlers();

                logger?.Log(LogSeverity.INFO,
                    $"BatchML file loaded from {currentFilePath}!");
            }
            else logger?.Log(LogSeverity.INFO,
                "Open file dialog cancelled by user.");
        }

        // Event handler for saving a file
        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (!isFileLoaded || masterRecipe is null)
            {
                logger?.Log(LogSeverity.WARNING, "No BatchML file loaded to save!");
                MessageBox.Show("No BatchML file loaded to save!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (isFileModified)
            {
                var menuItem = sender as MenuItem;

                if (menuItem?.Tag.ToString() == "Exit" || menuItem?.Tag.ToString() == "Close File")
                {
                    logger?.Log(LogSeverity.WARNING, "Prompting user to save changes before closing either the app or the file!");
                    MessageBox.Show("Please save changes before exiting!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        isFileModified = false;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving BatchML file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        logger?.Log(LogSeverity.ERROR, $"Error saving BatchML file: {ex.Message}");
                    }
                }
            }
            else return;
        }

        // Event handler for the S-Graph renderer button click event
        private void DrawGraph_Click(object sender, RoutedEventArgs e)
        {
            if (isFileLoaded)
            {
                SGraphCanvas.Children.Clear();
                try
                {
                    BuildSGraphFromXml();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex}", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    logger?.Log(LogSeverity.ERROR, $"Error building S-Graph from XML: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("No BatchML File loaded! Drawing example graph...", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                logger?.Log(LogSeverity.WARNING, "No BatchML file loaded, drawing example S-Graph!");
                DrawExampleGraph();
            }
        }

        // Clears all data tables
        private void PurgeDataTables()
        {
            masterTable.Clear();
            masterTable.Rows.Clear();
            masterTable.Columns.Clear();
            logger?.Log(LogSeverity.INFO, $"{masterTable.TableName} datatable purged!");

            recipeElementTable.Clear();
            recipeElementTable.Rows.Clear();
            recipeElementTable.Columns.Clear();
            logger?.Log(LogSeverity.INFO, $"{recipeElementTable.TableName} datatable purged!");

            stepTable.Clear();
            stepTable.Rows.Clear();
            stepTable.Columns.Clear();
            logger?.Log(LogSeverity.INFO, $"{stepTable.TableName} datatable purged!");

            linkTable.Clear();
            linkTable.Rows.Clear();
            linkTable.Columns.Clear();
            logger?.Log(LogSeverity.INFO, $"{linkTable.TableName} datatable purged!");

            foreach (DataTable dt in solutionsList)
            {
                dt.Clear();
                dt.Rows.Clear();
                dt.Columns.Clear();
                logger?.Log(LogSeverity.INFO, $"{dt.TableName} datatable purged!");
            }

            for (int i = MainTab.Items.Count - 1; i >= 0; i--)
            {
                if (CheckPresentTabs((MainTab.Items[i] as TabItem)!))
                {
                    MainTab.Items.RemoveAt(i);
                    logger?.Log(LogSeverity.INFO, $"{(MainTab.Items[i] as TabItem)?.Header} tab removed!");
                    continue;
                }
            }
        }

        // A helper method to check if a tab is one of the main tabs (to be removed when purging)
        private static bool CheckPresentTabs(TabItem tab) =>
            tab.Tag.ToString()?.Equals("MasterRecipe") is true ||
            tab.Tag.ToString()?.Equals("RecipeElements") is true ||
            tab.Tag.ToString()?.Equals("Steps") is true ||
            tab.Tag.ToString()?.Equals("Links") is true ||
            tab.Tag.ToString()?.Contains("Solution") is true;

        // A helper method to flush the containers ad clean the canvases in one go
        private void Purge()
        {
            foreach (Canvas cv in new[] { SGraphCanvas, GanttCanvas, RulerCanvas })
            {
                cv.Children.Clear();
                logger?.Log(LogSeverity.INFO, $"{cv.Name} has been cleared!");
            }

            GetNodes().Clear();
            logger?.Log(LogSeverity.INFO, "S-Graph nodes cleared!");

            GetEdges().Clear();
            logger?.Log(LogSeverity.INFO, "S-Graph edges cleared!");
        }

        // A helper method to enable solvers after the initial S-Graph is rendered
        private void EnableSolvers()
        {
            SGraphExists = true;

            foreach (var x in SolverMenu.Items)
            {
                if (x is MenuItem menuItem && !menuItem.Name.Contains("Add new"))
                {
                    menuItem.IsEnabled = SGraphExists;
                    logger?.Log(LogSeverity.INFO, $"{menuItem.Tag} solver enabled!");
                }
                else continue;
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
                AddNode($"Eq{eqID}", $"Eq{eqID}", new(i += 50, j));
                if (eqID % 3 == 0)
                {
                    AddNode($"Prod{prodID++}", $"Prod{prodID}", new(i + 200, j += 50));
                    i = 0;
                }
            }

            // Example edges
            for (int x = 1; x < GetNodes().Count; x++) AddEdge($"Eq{x}", (x % 3 != 0) ? $"Eq{x + 1}" : $"Prod{x / 3}", new Random().Next(5, 45));

            Render(SGraphCanvas, 4);

            logger?.Log(LogSeverity.INFO, "Example S-Graph drawn!");

            EnableSolvers();
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

            int
                i = 0,
                j = 0;

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
                j += (i % 300 is 0) ? 100 : 0;
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

            logger?.Log(LogSeverity.INFO, "S-Graph built from BatchML file!");

            EnableSolvers();
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

                logger?.Log(LogSeverity.INFO, "BatchML file loaded and data tables populated!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading BatchML file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger?.Log(LogSeverity.ERROR, $"Error loading BatchML file: {ex.Message}");
            }
        }

        // Handler event for closing a BatchML file
        private void CloseFile_Click(object sender, RoutedEventArgs e)
        {
            if (!isFileLoaded)
            {
                logger?.Log(LogSeverity.WARNING, "No BatchML file loaded to close!");
                MessageBox.Show("No BatchML file loaded to close!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (HasUnsavedChanges())
            {
                logger?.Log(LogSeverity.WARNING,
                    "Attempting to close file with unsaved changes; prompting user!");

                MessageBoxResult res = MessageBox.Show(
                    "The current BatchML file has unsaved changes. Do you wish to save before closing the file?",
                    "Unsaved changes detected",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    masterTable.AcceptChanges();
                    recipeElementTable.AcceptChanges();
                    stepTable.AcceptChanges();
                    linkTable.AcceptChanges();
                    SaveFile_Click(sender, e);
                }
                else if (res == MessageBoxResult.Cancel)
                {
                    logger?.Log(LogSeverity.INFO,
                        "File closing cancelled by user.");
                    return;
                }
            }

            try
            {
                Purge();
                PurgeDataTables();
                isFileLoaded = false;
                ManageFileHandlers();
                logger?.Log(LogSeverity.INFO, "BatchML file closed and all data cleared!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error closing BatchML: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger?.Log(LogSeverity.ERROR, $"Error closing BatchML: {ex.Message}");
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

            logger?.Log(LogSeverity.INFO, $"{dt.TableName} datatable displayed in new tab!");
        }

        // Exports an S-Graph to a JPEG file
        private void ExportCanvas_Click(object sender, RoutedEventArgs e)
        {
            switch ((MainTab.SelectedItem as TabItem)?.Tag?.ToString())
            {
                default:
                    MessageBox.Show("No content to export as JPEG file!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning); break;
                case "SGraph":
                    {
                        if (SGraphCanvas.Children.Count == 0)
                        {
                            MessageBox.Show("No S-Graph to export!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        else JPEGExporter.ExportOneCanvas(SGraphCanvas, "S-Graph");
                        logger?.Log(LogSeverity.INFO, "S-Graph exported as JPEG file!");
                        break;
                    }
                case "Gantt":
                    {
                        if (GanttCanvas.Children.Count == 0)
                        {
                            MessageBox.Show("No Gantt chart to export!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        else JPEGExporter.ExportMultipleCanvases(RulerCanvas, GanttCanvas, "Gantt Chart");
                        logger?.Log(LogSeverity.INFO, "Gantt chart exported as JPEG file!");
                        break;
                    }
            }
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

            mdt.Rows.Add(id, ver, desc, prodName, prodID, nominal, min, max, unit);

            mdt.RowChanged += MasterTable_RowChanged;

            DisplayDataTable(mdt, "MasterRecipe");
        }

        // Event handler for possible changes made inside the master recipe data table
        private void MasterTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            masterRecipe.SetElementValue(batchML + "ID", e.Row["RecipeID"]);
            masterRecipe.SetElementValue(batchML + "Version", e.Row["Version"]);

            var desc = masterRecipe.Elements(batchML + "Description").LastOrDefault();
            if (desc is not null) desc.Value = e.Row["Description"].ToString() ?? string.Empty;

            var header = masterRecipe.Element(batchML + "Header");
            if (header is not null)
            {
                header.SetElementValue(batchML + "ProductID", e.Row["ProductID"]);
                header.SetElementValue(batchML + "ProductName", e.Row["ProductName"]);
                var batchSize = header.Element(batchML + "BatchSize");
                if (batchSize is not null)
                {
                    batchSize.SetElementValue(batchML + "Nominal", e.Row["NominalBatchSize"]);
                    batchSize.SetElementValue(batchML + "Min", e.Row["MinBatchSize"]);
                    batchSize.SetElementValue(batchML + "Max", e.Row["MaxBatchSize"]);
                    batchSize.SetElementValue(batchML + "UnitOfMeasure", e.Row["UnitOfMeasure"]);
                }
            }

            logger?.Log(LogSeverity.INFO, "Changes made to Master Recipe table synced to XML!");
            isFileModified = true;
        }

        // Displays the recipe element table
        private void DisplayRecipeElementTable(DataTable redt, XNamespace batchML)
        {
            var recipeElements =

            redt.Columns.Add("ID");
            redt.Columns.Add("Description");


            if (masterRecipe.Descendants(batchML + "RecipeElement")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    Desc = x.Element(batchML + "Description")?.Value.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.ID) && !string.IsNullOrEmpty(x.Desc))
                .ToList() is not null)
            {
                foreach (var re in masterRecipe.Descendants(batchML + "RecipeElement")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    Desc = x.Element(batchML + "Description")?.Value.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.ID) && !string.IsNullOrEmpty(x.Desc))
                .ToList()!)
                {
                    DataRow dr = redt.NewRow();
                    dr["ID"] = re.ID;
                    dr["Description"] = re.Desc;
                    redt.Rows.Add(dr);
                }
                DisplayDataTable(redt, "RecipeElements");
            }
            else
            {
                logger?.Log(LogSeverity.WARNING, "No Recipe Elements found in the BatchML file.");
                MessageBox.Show("No Recipe Elements found in the BatchML file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            redt.RowChanged += (s, e) =>
            {
                if (e.Row.RowState is not DataRowState.Deleted)
                {
                    SyncRowToXml(e.Row, "RecipeElements");
                    isFileModified = true;
                }
            };
            redt.RowDeleted += (s, e) =>
            {
                SyncDeleteRowFromXml(e.Row, "RecipeElements");
                isFileModified = true;
            };
        }

        // Displays the steps table
        private void DisplayStepTable(DataTable sdt, XNamespace batchML)
        {
            var steps =

            sdt.Columns.Add("ID");
            sdt.Columns.Add("RecipeElementID");

            if (masterRecipe.Element(batchML + "ProcedureLogic")?.Descendants(batchML + "Step")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    REID = x.Element(batchML + "RecipeElementID")?.Value.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.ID) && !string.IsNullOrEmpty(x.REID))
                .ToList() is not null)
            {
                foreach (var step in masterRecipe.Element(batchML + "ProcedureLogic")?.Descendants(batchML + "Step")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    REID = x.Element(batchML + "RecipeElementID")?.Value.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.ID) && !string.IsNullOrEmpty(x.REID))
                .ToList()!)
                {
                    DataRow dr = sdt.NewRow();
                    dr["ID"] = step.ID;
                    dr["RecipeElementID"] = step.REID;
                    sdt.Rows.Add(dr);
                }
                DisplayDataTable(sdt, "Steps");
            }
            else
            {
                logger?.Log(LogSeverity.WARNING, "No Steps found in the BatchML file.");
                MessageBox.Show("No Steps found in the BatchML file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            sdt.RowChanged += (s, e) =>
            {
                if (e.Row.RowState is not DataRowState.Deleted)
                {
                    SyncRowToXml(e.Row, "Steps");
                    isFileModified = true;
                }
            };
            sdt.RowDeleted += (s, e) =>
            {
                SyncDeleteRowFromXml(e.Row, "Steps");
                isFileModified = true;
            };
        }

        // Displays the links table
        private void DisplayLinkTable(DataTable ldt, XNamespace batchML, XNamespace customNS)
        {

            ldt.Columns.Add("ID");
            ldt.Columns.Add("From");
            ldt.Columns.Add("To");
            ldt.Columns.Add("Duration");

            if ((masterRecipe.Element(batchML + "ProcedureLogic")?.Descendants(batchML + "Link")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    FromID = x.Element(batchML + "FromID")?.Element(batchML + "FromIDValue")?.Value.Trim(),
                    ToID = x.Element(batchML + "ToID")?.Element(batchML + "ToIDValue")?.Value.Trim(),
                    Duration = x.Element(batchML + "Extension")?.Descendants(customNS + "Duration").FirstOrDefault()?.Value.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.ID)
                    && !string.IsNullOrEmpty(x.FromID)
                    && !string.IsNullOrEmpty(x.ToID)
                    && !string.IsNullOrEmpty(x.Duration))
                .ToList()) is not null)
            {
                foreach (var link in masterRecipe.Element(batchML + "ProcedureLogic")?.Descendants(batchML + "Link")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    FromID = x.Element(batchML + "FromID")?.Element(batchML + "FromIDValue")?.Value.Trim(),
                    ToID = x.Element(batchML + "ToID")?.Element(batchML + "ToIDValue")?.Value.Trim(),
                    Duration = x.Element(batchML + "Extension")?.Descendants(customNS + "Duration").FirstOrDefault()?.Value.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.ID)
                    && !string.IsNullOrEmpty(x.FromID)
                    && !string.IsNullOrEmpty(x.ToID)
                    && !string.IsNullOrEmpty(x.Duration))
                .ToList()!)
                {
                    DataRow dr = ldt.NewRow();
                    dr["ID"] = link.ID;
                    dr["From"] = link.FromID;
                    dr["To"] = link.ToID;
                    dr["Duration"] = link.Duration;

                    ldt.Rows.Add(dr);
                }
                DisplayDataTable(ldt, "Links");
            }
            else
            {
                logger?.Log(LogSeverity.WARNING, "No Links found in the BatchML file.");
                MessageBox.Show("No Links found in the BatchML file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ldt.RowChanged += (s, e) =>
            {
                if (e.Row.RowState is not DataRowState.Deleted)
                {
                    SyncRowToXml(e.Row, "Links");
                    isFileModified = true;
                }
            };
            ldt.RowDeleted += (s, e) =>
            {
                SyncDeleteRowFromXml(e.Row, "Links");
                isFileModified = true;
            };
        }

        // Displays the solution as a data table
        private void DisplaySolutionAsTable(List<GanttItem> data)
        {
            DataTable sdt = new($"Solution ({GanttCanvas.Tag.ToString() ?? string.Empty})");

            sdt.Columns.Add("TaskID");
            sdt.Columns.Add("StartTime (min)");
            sdt.Columns.Add("Duration (min)");
            sdt.Columns.Add("EndTime (min)");

            foreach (var item in data)
            {
                DataRow dr = sdt.NewRow();
                dr["TaskID"] = item.ID;
                dr["StartTime (min)"] = item.Start;
                dr["Duration (min)"] = item.Duration;
                dr["EndTime (min)"] = item.Start + item.Duration;
                sdt.Rows.Add(dr);
            }

            solutionsList.Add(sdt);
            DisplayDataTable(sdt, sdt.TableName);
            logger?.Log(LogSeverity.INFO, $"Solution table for \"{GanttCanvas.Tag.ToString() ?? string.Empty}\" displayed in new tab!");
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

                string input = Interaction.InputBox("Enter a name for the custom solver:", "Custom Solver Name", Path.GetFileNameWithoutExtension(solverPath));

                if (!string.IsNullOrWhiteSpace(input))
                {
                    if (customSolvers.Any(s => s.Name.Equals(input, StringComparison.OrdinalIgnoreCase)))
                    {
                        logger?.Log(LogSeverity.WARNING, "A solver with this name already exists! Aborting assignment!");
                        MessageBox.Show("A solver with this name already exists!", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    MessageBoxResult typeRes = MessageBox.Show(
                        "Is this an Iterative/Metaheuristic Solver (e.g., SA, GA) that requires numeric parameters?",
                        "Define Solver Type",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    string typeID = typeRes == MessageBoxResult.Yes ? "metaheuristic" : "deterministic";

                    CustomSolver newSolver = new()
                    {
                        Name = input.Trim(),
                        TypeID = typeID,
                        Path = solverPath,
                        Arguments = []
                    };
                    customSolvers.Add(newSolver);
                    logger?.Log(LogSeverity.INFO,
                        $"Custom {newSolver.TypeID} solver \"{newSolver.Name}\" added with path: {newSolver.Path}");

                    SaveCustomSolvers2JSON();
                    logger?.Log(LogSeverity.INFO,
                        "Custom solvers saved to JSON file!");

                    AddCustomSolverMenuItem(newSolver);
                    BuildSolverMenu(SolverMenu);
                }
            }
        }

        // Saves custom solvers to a specific JSON file
        private void SaveCustomSolvers2JSON()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(customSolverPath)!);
                string json = JsonSerializer.Serialize(customSolvers, CachedOptions);
                File.WriteAllText(customSolverPath, json);
                logger?.Log(LogSeverity.INFO, "Custom solvers saved to JSON file!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving custom solvers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger?.Log(LogSeverity.ERROR, $"Error saving custom solvers: {ex.Message}");
            }
        }

        // Loads custom solvers from a specific JSON file
        private void LoadCustomSolversFromJSON()
        {
            try
            {
                if (File.Exists(customSolverPath))
                {
                    string json = File.ReadAllText(customSolverPath);
                    List<CustomSolver> loaded = JsonSerializer.Deserialize<List<CustomSolver>>(json)!;
                    if (loaded != null)
                    {
                        customSolvers.Clear();
                        customSolvers.AddRange(loaded);
                        foreach (var cs in customSolvers) AddCustomSolverMenuItem(cs);
                    }
                    logger?.Log(LogSeverity.INFO, "Custom solvers loaded from JSON file!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading custom solvers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger?.Log(LogSeverity.ERROR, $"Error loading custom solvers: {ex.Message}");
            }
        }

        private void Focus4Export_Click(object sender, RoutedEventArgs e)
        {
            string tag = (MainTab.SelectedItem as TabItem)?.Tag?.ToString() ?? string.Empty;
            ExportCanvasMenuItem.IsEnabled = tag.Equals("Gantt") || tag.Equals("SGraph");
        }

        // Adds a custom solver to the Solver menu
        private void AddCustomSolverMenuItem(CustomSolver solver)
        {
            var item = new MenuItem
            {
                Header = $"{solver.Name} ({solver.TypeID})",
                Tag = solver.Name,
                IsEnabled = SGraphExists
            };
            item.Click += SolveClick;
            SolverMenu.Items.Add(item);
            logger?.Log(LogSeverity.INFO, $"Custom solver menu item \"{solver.Name} ({solver.TypeID})\" addedto the menu!");
        }

        // Event handler for creating a solution table from the Gantt chart
        private void CreateSolutionTableBtn_Click(object sender, RoutedEventArgs e)
        {
            if (GanttCanvas.Children.Count != 0) DisplaySolutionAsTable(ganttData);
            else
            {
                logger?.Log(LogSeverity.WARNING, "No Gantt chart was present to create solution table from!");
                MessageBox.Show("No Gantt chart to create solution table from!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // A helper method, which converts a raw duration string to ISO 8601 (PT duration) format
        private static string ConvertToISO8601(string? raw)
        {
            // A special pattern to match ISO 8601 duration format (PTnHnM)
            Regex regex = ISO8601Format();

            // if it already looks like an ISO 8601 duration, return it as is (ideally, that is)
            logger?.Log(LogSeverity.INFO, $"Converting raw duration \"{raw}\" to ISO 8601 format...");
            if (regex.IsMatch(raw!))
            {
                logger?.Log(LogSeverity.INFO, $"Raw duration \"{raw}\" is already in ISO 8601 format.");
                return raw!;
            }

            // try to parse it as a TimeSpan first
            logger?.Log(LogSeverity.INFO, $"Raw duration \"{raw}\" is not in ISO 8601 format, trying to parse as TimeSpan...");
            if (TimeSpan.TryParse(raw, out var ts))
            {
                var sb = new StringBuilder("PT");
                if (ts.Hours > 0) sb.Append($"{ts.Hours}H");
                if (ts.Minutes > 0) sb.Append($"{ts.Minutes}M");
                if (ts.Seconds > 0) sb.Append($"{ts.Seconds}S");
                if (sb.ToString().Equals("PT")) sb.Append("0M"); // zero duration case
                logger?.Log(LogSeverity.INFO, $"Raw duration \"{raw}\" parsed as TimeSpan and converted to ISO 8601 format: {sb}");
                return sb.ToString();
            }

            // if raw is just a number, assume it's in minutes and convert accordingly
            logger?.Log(LogSeverity.INFO, $"Raw duration \"{raw}\" is not a valid TimeSpan either, trying to parse as integer minutes...");
            if (int.TryParse(raw, out int mins))
            {
                string dur = (mins < 0) ? $"PT{mins / 60}H{mins % 60}M" : "PT0M"; // no negative durations are allowed
                logger?.Log(LogSeverity.INFO, $"Raw duration \"{raw}\" parsed as Int32, about to return as ISO 8601: {dur}");
                return dur;
            }

            // default return value if parsing fails
            logger?.Log(LogSeverity.WARNING, $"Raw duration \"{raw}\" could not be parsed, defaulting to zero duration (PT0M)!");
            return "PT0M";
        }

        // A helper method to find an existing XML element based on key columns in the DataRow
        private XElement? FindExistingElement(string tableName, DataRow drow)
        {
            if (masterRecipe is null || !mappings.TryGetValue(tableName, out var map))
            {
                logger?.Log(LogSeverity.ERROR, $"No mapping found for table \"{tableName}\" or master recipe is null!");
                return null;
            }

            foreach (var el in masterRecipe.Descendants(batchML + map.ParentElement))
            {
                bool allMatch = true;
                foreach (var col in map.KeyCols)
                {
                    string expected = (drow.RowState == DataRowState.Deleted ?
                        (drow[col, DataRowVersion.Original]?.ToString() ?? string.Empty) :
                        (drow[col]?.ToString() ?? string.Empty)).Trim();

                    if (string.IsNullOrEmpty(expected))
                    {
                        logger?.Log(LogSeverity.WARNING, $"Key column \"{col}\" in table \"{tableName}\" is empty, cannot match!");
                        allMatch = false;
                        break;
                    }

                    string? actual;
                    if (col.Equals("Duration", StringComparison.OrdinalIgnoreCase))
                    {
                        var dur = el.Element(batchML + "Extension")?.Element(customNS + "Duration")?.Value?.Trim() ?? string.Empty;
                        actual = ConvertToISO8601(dur);
                        expected = ConvertToISO8601(expected);
                    }
                    else if (map.ParentElement.Equals("Link") &&
                        (col.Equals("From", StringComparison.OrdinalIgnoreCase) ||
                        col.Equals("To", StringComparison.OrdinalIgnoreCase)))
                    {
                        actual = el.Element(batchML + $"{col}ID")?
                                .Element(batchML + map.Col2El[col])?.Value?.Trim();
                    }
                    else
                    {
                        var xmlName = map.Col2El[col];
                        actual = el.Element(batchML + xmlName)?.Value?.Trim();
                    }

                    if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.Log(LogSeverity.INFO, $"No match for key column \"{col}\": expected \"{expected}\", actual \"{actual}\"");
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch)
                {
                    logger?.Log(LogSeverity.INFO, $"Found existing XML element for table \"{tableName}\" matching the DataRow keys.");
                    return el;
                }
            }

            logger?.Log(LogSeverity.INFO, $"No existing XML element found for table \"{tableName}\" matching the DataRow keys.");
            return null;
        }

        // Custom event handler for syncing a row from the DataTable to the XML when it's changed or added
        private void SyncRowToXml(DataRow row, string tableName)
        {
            if (masterRecipe is null || !mappings.TryGetValue(tableName, out var map))
            {
                logger?.Log(LogSeverity.ERROR, $"No mapping found for table \"{tableName}\" or master recipe is null!");
                return;
            }

            if (row.RowState is DataRowState.Added)
            {
                bool hasValidKeys = true;
                foreach (var col in map.KeyCols)
                {
                    if (string.IsNullOrWhiteSpace(row[col]?.ToString()))
                    {
                        hasValidKeys = !hasValidKeys;
                        break;
                    }
                }

                if (!hasValidKeys)
                {
                    logger?.Log(LogSeverity.INFO,
                        $"Aborting sync for new row in '{tableName}': Missing requid primary key data.");
                    return;
                }
            }

            var existing = FindExistingElement(tableName, row);

            if (existing is not null)
            {
                foreach (var col in map.Col2El.Keys)
                {
                    var val = row[col]?.ToString() ?? string.Empty;

                    if (string.Equals(col, "Duration", StringComparison.OrdinalIgnoreCase))
                    {
                        XElement? ext = existing.Element(batchML + "Extension");
                        if (ext is null)
                        {
                            ext = new(batchML + "Extension");
                            existing.Add(ext);
                        }

                        XElement durEl = ext.Element(customNS + "Duration") ?? new XElement(customNS + "Duration");
                        durEl.Value = ConvertToISO8601(val);
                        if (durEl.Parent is null) ext?.Add(durEl);
                    }
                    else if (map.ParentElement.Equals("Link") &&
                        (col.Equals("From", StringComparison.OrdinalIgnoreCase) ||
                        col.Equals("To", StringComparison.OrdinalIgnoreCase)))
                    {
                        XElement outer = existing.Element(batchML + $"{col}ID")
                            ?? new XElement(batchML + $"{col}ID");
                        if (outer.Parent is null) existing.Add(outer);

                        XElement inner = outer.Element(batchML + map.Col2El[col])
                            ?? new XElement(batchML + map.Col2El[col]);
                        inner.Value = val;
                        if (inner.Parent is null) outer.Add(inner);

                        XElement typeEl = outer.Element(batchML + $"{col}Type")
                            ?? new XElement(batchML + $"{col}Type", "Step");
                        if (typeEl.Parent is null) outer.Add(typeEl);
                    }
                    else
                    {
                        var child = existing.Element(batchML + map.Col2El[col]) ?? new XElement(batchML + map.Col2El[col]);
                        child.Value = val;
                        if (child.Parent is null) existing.Add(child);
                    }
                }
            }
            else
            {
                var newEl = new XElement(batchML + map.ParentElement);

                foreach (var col in map.Col2El.Keys)
                {
                    string val = row[col]?.ToString() ?? string.Empty;

                    if (string.Equals(col, "Duration", StringComparison.OrdinalIgnoreCase))
                    {
                        newEl.Add(new XElement(batchML + "Extension",
                            new XElement(customNS + "Duration", ConvertToISO8601(val))));
                    }
                    else if (map.ParentElement.Equals("Link") &&
                        (col.Equals("From", StringComparison.OrdinalIgnoreCase) ||
                        col.Equals("To", StringComparison.OrdinalIgnoreCase)))
                    {
                        newEl.Add(new XElement(batchML + $"{col}ID",
                            new XElement(batchML + $"{map.Col2El[col]}", val),
                            new XElement(batchML + $"{col}Type", "Step")));
                    }
                    else newEl.Add(new XElement(batchML + map.Col2El[col], val));
                }

                XElement insertionPoint;
                if (map.ParentElement.Equals("Step") || map.ParentElement.Equals("Link"))
                {
                    insertionPoint = masterRecipe.Element(batchML + "ProcedureLogic")!;
                    if (insertionPoint is null)
                    {
                        insertionPoint = new(batchML + "ProcedureLogic");
                        masterRecipe.Add(insertionPoint);

                        logger?.Log(LogSeverity.WARNING,
                            "Missing <ProcedureLogic> element added to the document!");
                    }
                }
                else insertionPoint = masterRecipe;

                var last = insertionPoint.Elements(batchML + map.ParentElement).LastOrDefault();
                if (last is not null) last.AddAfterSelf(newEl);
                else insertionPoint.Add(newEl);
            }

            logger?.Log(LogSeverity.INFO, $"Synchronized DataRow to XML for table \"{tableName}\".");
        }

        // Custom event handler for deleting a row from the XML when it's deleted from the DataTable
        private void SyncDeleteRowFromXml(DataRow row, string tableName)
        {
            if (masterRecipe is null || !mappings.TryGetValue(tableName, out TableMapper? _)) return;

            var tempRow = row.Table.NewRow();

            foreach (var col in mappings[tableName].KeyCols)
            {
                if (row.Table.Columns.Contains(col))
                    tempRow[col] = row[col, DataRowVersion.Original];
            }

            var existing = FindExistingElement(tableName, tempRow);
            existing?.Remove();
            logger?.Log(LogSeverity.INFO, $"Deleted XML element for table \"{tableName}\" corresponding to the deleted DataRow.");
        }
    }

    // A helper class to map DataTable columns to XML elements
    public class TableMapper
    {
        public string ParentElement { get; set; } = string.Empty;
        public string[] KeyCols { get; set; } = [];
        public Dictionary<string, string> Col2El { get; set; } = [];
    }
}