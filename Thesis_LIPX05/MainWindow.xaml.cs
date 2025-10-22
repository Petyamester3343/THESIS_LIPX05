using Microsoft.VisualBasic;
using Microsoft.Win32;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

using Thesis_LIPX05.Util;
using Thesis_LIPX05.Util.Optimizers;

using static Thesis_LIPX05.Util.Gantt;
using static Thesis_LIPX05.Util.LogManager;
using static Thesis_LIPX05.Util.SGraph;

using FlowShopKVP = System.Collections.Generic.KeyValuePair<string, (int, int, string)>;
using FilePath = System.IO.Path;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Private read-only fields
        private readonly DataTable masterTable, recipeElementTable, stepTable;
        private readonly XNamespace batchML, customNS;
        private readonly string customSolverPath = FilePath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Y0KAI_TaskScheduler", "custom_solvers.json");

        // Private read-only collections
        private readonly List<CustomSolver> customSolvers;
        private readonly List<DataTable> solutionsList;
        private readonly List<string> solvers = ["Johnson's Rule", "List Scheduling"];
        private readonly Dictionary<string, TableMapper> mappings;

        // Private overwritable fields
        private XElement masterRecipe;
        private double zoom;
        private bool isFileLoaded = false, SGraphExists, GanttExists = false, isFileModified = false;
        private string currentFilePath = string.Empty;

        // Private overwritable collections
        private List<GanttItem> GanttData;

        // Private constants
        private const double BaseTimeScale = 10.0;
        private const string
            MasterRecipeTableName = "Master Recipe Table",
            RecipeElementsTableName = "Recipe Elements Table",
            StepsTableName = "Steps Table";

        // Private static fields
        private static readonly JsonSerializerOptions CachedOptions = new() { WriteIndented = true };

        // Constructor of the main window
        public MainWindow()
        {
            LogGeneralActivity(LogSeverity.INFO,
                "Beginning initialization...", GeneralLogContext.INITIALIZATION);

            masterRecipe = new("MasterRecipe");
            batchML = XNamespace.Get("http://www.wbf.org/xml/BatchML-V02");
            customNS = XNamespace.Get("http://lipx05.y0kai.com/batchml/custom");

            masterTable = new(MasterRecipeTableName);
            recipeElementTable = new(RecipeElementsTableName);
            stepTable = new(StepsTableName);

            GanttData = [];
            zoom = 1;

            mappings = InitMappings();

            customSolvers = [];
            solutionsList = [];

            InitializeComponent();

            LoadCustomSolversFromJSON();
            BuildSolverMenu(SolverMenu);

            ManageFileHandlers();
            ManageSolutionDataTableCreator();

            LogGeneralActivity(LogSeverity.INFO,
                "Initialization complete!", GeneralLogContext.INITIALIZATION);
        }

        // Enables or disables the saving and closing by checking if a file is loaded
        private void ManageFileHandlers()
        {
            foreach (var mi in new[] { SaveFileMenuItem, CloseFileMenuItem }) mi.IsEnabled = isFileLoaded;

            ExportCanvasMenuItem.IsEnabled = GanttCanvas.Children.Count is not 0 || SGraphCanvas.Children.Count is not 0;
            LoadSolutionMenuItem.IsEnabled = SGraphExists;

            LogGeneralActivity(LogSeverity.INFO,
                "Saving and closing are disabled for as long as there are no files opened.", GeneralLogContext.CONSTRAINT);
        }

        private void ManageSolutionDataTableCreator() => CreateSolutionTableMenuItem.IsEnabled = GanttExists;

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
                        KeyCols = ["InternalKey"],
                        Col2El = new()
                        {
                            { "ID", "ID" },
                            { "Type", "RecipeElementType" },
                            { "Description", "Description" }
                        }
                    }
                }
            };

        // Helper expression-body method to add a separator to the solver menu
        private static void AddSeparator(MenuItem solverMenu) => solverMenu.Items.Add(new Separator());

        // dynamicaly building the solver menu
        private void BuildSolverMenu(MenuItem solverMenu)
        {
            try
            {
                SolverMenu.Items.Clear();
                foreach (var solver in solvers)
                {
                    MenuItem item = new()
                    {
                        Header = solver,
                        Tag = solver,
                        IsEnabled = SGraphExists
                    };
                    item.Click += SolveClick;
                    solverMenu.Items.Add(item);
                    LogGeneralActivity(LogSeverity.INFO,
                        $"{item.Tag} solver added!", GeneralLogContext.INITIALIZATION);
                }
                AddSeparator(solverMenu);

                foreach (var cs in customSolvers)
                {
                    AddCustomSolverMenuItem(cs);
                    LogGeneralActivity(LogSeverity.INFO,
                        "{cs.Name} ({cs.TypeID} type) solver added!", GeneralLogContext.INITIALIZATION);
                }
                AddSeparator(solverMenu);

                MenuItem addSolverItem = new()
                {
                    Header = "Add Custom Solver...",
                    IsEnabled = true
                };
                addSolverItem.Click += AddCustomSolver_Click;
                solverMenu.Items.Add(addSolverItem);
                AddSeparator(solverMenu);

                LogGeneralActivity(LogSeverity.INFO,
                    "Solver menu successfully built!", GeneralLogContext.INITIALIZATION);
            }
            catch (Exception ex)
            {
                LogGeneralActivity(LogSeverity.INFO,
                    $"Error building solver menu: {ex.Message}", GeneralLogContext.INITIALIZATION);
                MessageBox.Show($"Error building solver menu: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Event handler for loading a solution from a text file and drawing it as a Gantt chart on GanttCanvas
        private void LoadSolutionItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new()
            {
                DefaultExt = ".txt",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Load Solution from Text File"
            };

            if (dlg.ShowDialog() is true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(dlg.FileName);
                    List<Node> path = [];
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length is 2 && parts[0].Contains("NODE")) // expecting lines like "NODE <nodeId>"
                        {
                            string nodeId = parts[1];
                            if (GetNodes().TryGetValue(nodeId, out Node? node))
                            {
                                path.Add(node);
                                LogGeneralActivity(LogSeverity.INFO,
                                    $"Node {nodeId} added to solution path from file.", GeneralLogContext.EXTERN_SOLVER);
                            }
                            else LogGeneralActivity(LogSeverity.WARNING,
                                $"Node {nodeId} not found in current S-Graph, skipping...", GeneralLogContext.EXTERN_SOLVER);
                        }
                    }
                    if (path.Count is 0)
                    {
                        LogGeneralActivity(LogSeverity.ERROR,
                            "Loaded solution file contains no valid nodes!", GeneralLogContext.EXTERN_SOLVER);
                        MessageBox.Show("Loaded solution file contains no valid nodes!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    GanttData = BuildChartFromPath(path, GetEdges());
                    LogGeneralActivity(LogSeverity.INFO,
                        $"Gantt data built with {GanttData.Count} items from loaded solution!", GeneralLogContext.GANTT);
                    double totalTime = GanttData.Max(x => x.Start + x.Duration);
                    int rowC = GanttData.Count;
                    double timeScale = BaseTimeScale * zoom;
                    RenderGanttChart(GanttCanvas, GanttData, timeScale);
                    DrawGanttRuler(RulerCanvas, GanttCanvas, totalTime, timeScale);
                    LogGeneralActivity(LogSeverity.INFO,
                        "Loaded solution rendered successfully!", GeneralLogContext.GANTT);
                    GanttExists = true;
                }
                catch (Exception ex)
                {
                    LogGeneralActivity(LogSeverity.ERROR,
                        $"Error loading solution from file: {ex.Message}", GeneralLogContext.EXTERN_SOLVER);
                    MessageBox.Show($"Error loading solution from file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Event handler for the MainWindow closing event, which checks for unsaved modifications in the data tables
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (isFileModified && (masterTable.GetChanges() is not null ||
                recipeElementTable.GetChanges() is not null ||
                stepTable.GetChanges() is not null))
            {
                LogGeneralActivity(LogSeverity.WARNING,
                    "App is about to be shut down with unsaved changes! Prompting user to save before exiting!", GeneralLogContext.EXITUS);
                MessageBoxResult res = MessageBox.Show("Do you wish to save changes before exiting?",
                    "Confirm Exit", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                switch (res)
                {
                    case MessageBoxResult.Yes:
                        {
                            LogGeneralActivity(LogSeverity.INFO,
                                "User opted to save changes in the datatables to the BatchML file before exiting!", GeneralLogContext.SAVE);
                            SaveFile_Click(sender, new());
                            MessageBoxResult res2 = MessageBox.Show("Do you wish to save your custom solvers?",
                                "Confirm Save", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                            if (res2 is MessageBoxResult.Yes)
                            {
                                SaveCustomSolvers2JSON();
                                LogGeneralActivity(LogSeverity.INFO,
                                    "Shutting down after saving custom solvers and the changes made to the datatable...", GeneralLogContext.EXITUS);
                            }
                            else LogGeneralActivity(LogSeverity.INFO,
                                "Shutting down after saving the changes made to the datatable, without saving custom solvers...", GeneralLogContext.EXITUS);
                            CloseLog();
                            break;
                        }
                    case MessageBoxResult.No:
                        {
                            LogGeneralActivity(LogSeverity.WARNING,
                                "User opted to exit without saving changes in the datatables to the BatchML file before exiting!", GeneralLogContext.EXITUS);
                            MessageBoxResult res3 = MessageBox.Show("Do you wish to save your custom solvers?",
                                "Confirm Save", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                            if (res3 is MessageBoxResult.Yes)
                            {
                                SaveCustomSolvers2JSON();
                                LogGeneralActivity(LogSeverity.WARNING,
                                    "Shutting down with saving only the custom solvers...", GeneralLogContext.EXITUS);
                            }
                            else LogGeneralActivity(LogSeverity.WARNING,
                                "Shutting down without saving anything...", GeneralLogContext.EXITUS);
                            CloseLog();
                            break;
                        }
                    case MessageBoxResult.Cancel:
                        e.Cancel = true; // Cancel the closing event
                        LogGeneralActivity(LogSeverity.INFO, $"Application exit cancelled by user!", GeneralLogContext.EXITUS);
                        break;
                }
            }
            else LogGeneralActivity(LogSeverity.INFO, "No changes were made, exiting app...", GeneralLogContext.EXITUS);
        }

        // Event handler for the Exit menu item click event
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (isFileModified && HasUnsavedChanges())
            {
                LogGeneralActivity(LogSeverity.WARNING,
                    "Unsaved changes detected, prompting user to save before exiting!", GeneralLogContext.EXITUS);
                MainWindow_Closing(sender, new(false));
            }
            else
            {
                LogGeneralActivity(LogSeverity.INFO,
                    "No changes were made, shutting down...", GeneralLogContext.EXITUS);
                CloseLog();
                Application.Current.Shutdown();
            }
        }

        // Event handler for the Solve menu item click event
        // This method checks which solver was selected and calls the appropriate method
        private void SolveClick(object sender, RoutedEventArgs e)
        {
            GanttData.Clear();

            if (sender is not MenuItem menuItem) return;
            string? solverTag = menuItem.Tag.ToString();

            if (solverTag == "Johnson's Rule")
            {
                LogGeneralActivity(LogSeverity.INFO, "Johnson's Rule solver selected!", GeneralLogContext.INTEG_SOLVER);
                SolveWithJohnson();
                GanttCanvas.Tag = solverTag;
                return;
            }
            if (solverTag == "List Scheduling")
            {
                LogGeneralActivity(LogSeverity.INFO, "List Scheduling solver selected!", GeneralLogContext.INTEG_SOLVER);
                SolveWithLS();
                GanttCanvas.Tag = solverTag;
                return;
            }
            // reserved for external solvers reading from XML and writing to TXT files
            if (customSolvers.Any(cs => cs.Name == solverTag)) InitExtSolver(menuItem);
        }

        private void RenderSchedule(List<Node> optPath, string method)
        {
            GanttData.Clear();
            GanttCanvas.Children.Clear();
            RulerCanvas.Children.Clear();

            if (optPath.Count is 0)
            {
                LogGeneralActivity(LogSeverity.ERROR, "Optimized path is empty, cannot render schedule!", GeneralLogContext.GANTT);
                MessageBox.Show("Optimized path is empty, cannot render schedule!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            RenderSGraph(SGraphCanvas);
            LogGeneralActivity(LogSeverity.INFO,
                $"S-graph re-rendered with {method} sequence.", GeneralLogContext.S_GRAPH);

            GanttData = BuildChartFromPath(optPath, GetEdges());
            LogGeneralActivity(LogSeverity.INFO,
                $"Gantt data built with {GanttData.Count} items from optimized path using {method}!", GeneralLogContext.GANTT);

            double totalTime = GanttData.Max(x => x.Start + x.Duration);
            double timeScale = BaseTimeScale * zoom;

            DrawGanttRuler(RulerCanvas, GanttCanvas, totalTime, timeScale);
            RenderGanttChart(GanttCanvas, GanttData, timeScale);
            LogGeneralActivity(LogSeverity.INFO, $"{method} schedule rendered successfully!", GeneralLogContext.GANTT);

            GanttExists = true;
            ManageSolutionDataTableCreator();
        }

        private void SolveWithJohnson()
        {
            LogGeneralActivity(LogSeverity.INFO, "Starting Johnson's Rule solver...", GeneralLogContext.INTEG_SOLVER);

            if (!GetNodes().Any(n => n.Value.TimeM1 > 0 || n.Value.TimeM2 > 0))
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    "Nodes lack TimeM1/TimeM2 data required for flow shop optimization!", GeneralLogContext.INTEG_SOLVER);
                MessageBox.Show("Nodes lack TimeM1/TimeM2 data required for flow shop optimization!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            JohnsonOptimizer opt = new(GetNodes(), GetEdges());
            List<Node> optPath = opt.Optimize();
            RenderSchedule(optPath, "Johnson's Rule");
        }

        private void SolveWithLS()
        {
            LogGeneralActivity(LogSeverity.INFO,
                "Starting List Scheduling solver...", GeneralLogContext.INTEG_SOLVER);
            if (!GetNodes().Any(n => n.Value.TimeM1 > 0 || n.Value.TimeM2 > 0))
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    "Nodes lack TimeM1/TimeM2 data required for flow shop optimization!", GeneralLogContext.INTEG_SOLVER);
                MessageBox.Show("Nodes lack TimeM1/TimeM2 data required for flow shop optimization!",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ListSchedulingOptimizer opt = new(GetNodes(), GetEdges());
            List<Node> optPath = opt.Optimize();
            RenderSchedule(optPath, "List Scheduling");
        }

        // Initializes and runs an external custom solver
        private void InitExtSolver(MenuItem menuItem)
        {
            // commencing inquiries and initializating the solver's logger
            string
                tempDir = FilePath.Combine(Environment.CurrentDirectory, "SolverTemp"),
                tempTxtPath = FilePath.Combine(tempDir, $"SimulatedAnnealing_Solution.txt"),
                tempXmlPath = string.Empty; // initializing out variable beyond the try-catch-finally scope

            CustomSolver? customSolver = customSolvers.FirstOrDefault(cs => cs.Name == menuItem?.Tag.ToString());
            if (customSolver is null)
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    "Custom solver not found!", GeneralLogContext.EXTERN_SOLVER);
                MessageBox.Show("Custom solver not found!",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            LogGeneralActivity(LogSeverity.INFO,
                $"{customSolver.Name} custom solver selected!", GeneralLogContext.EXTERN_SOLVER);

            try
            {
                WriteSGraph2XML(tempDir, out tempXmlPath);
                List<string> launchArgs = [];

                if (customSolver.TypeID.Equals("Metaheuristic", StringComparison.OrdinalIgnoreCase))
                {
                    LogSolverActivity(LogSeverity.INFO,
                        $"Gathering parameters for {customSolver.Name}", customSolver.Name);
                    GatherArgs4Metaheur(launchArgs);
                    LogSolverActivity(LogSeverity.INFO,
                        $"Parameters gathered: {string.Join(", ", launchArgs)}", customSolver.Name);
                }
                else if (customSolver.TypeID.Equals("Deterministic", StringComparison.OrdinalIgnoreCase))
                    LogSolverActivity(LogSeverity.INFO,
                        "Deterministic solver selected; skipping numeric parameter gathering...", customSolver.Name);

                launchArgs.AddRange(tempXmlPath, tempTxtPath);

                if (MessageBox.Show(
                    "Run solver in silent mode (-s or --silent)? This suppresses detailed logging and improves speed.",
                    "Solver Configuration",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) is MessageBoxResult.Yes)
                    launchArgs.Add("-s");

                customSolver.Arguments.Clear();
                customSolver.Arguments.AddRange(launchArgs);

                Process p = new()
                {
                    StartInfo = new()
                    {
                        FileName = customSolver.Path,
                        Arguments = string.Join(' ', customSolver.Arguments),
                        RedirectStandardInput = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = customSolver.Arguments.Contains("-s")
                    }
                };
                p.Start();
                if (p.ExitCode is not 0)
                {
                    LogSolverActivity(LogSeverity.ERROR,
                        $"An error occurred with the external solver (exit code: {p.ExitCode})!", customSolver.Name);
                    MessageBox.Show($"A problem occurred with the external solver! (Code {p.ExitCode})",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    GanttCanvas.Tag = customSolver.Name;
                    List<Node> path = LoadSolFromTxt(tempTxtPath);
                    RenderSGraph(SGraphCanvas);
                    RenderSchedule(path, customSolver.Name);
                }
            }
            catch (Exception ex)
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    $"Error while running custom solver: {ex.Message}", GeneralLogContext.EXTERN_SOLVER);
                MessageBox.Show($"Error running custom solver: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // Commented for debugging
            /*finally
            {
                if (File.Exists(tempXmlPath)) File.Delete(tempXmlPath);
            }*/
        }

        private static List<Node> LoadSolFromTxt(string filePath)
        {
            LogGeneralActivity(LogSeverity.INFO,
                $"Loading solution from {filePath}...", GeneralLogContext.LOAD);
            List<Node> path = [];

            if (!File.Exists(filePath))
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    $"Solution file {filePath} does not exist!", GeneralLogContext.LOAD);
                MessageBox.Show($"Solution file {filePath} does not exist!",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return path;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath);

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("NODE ", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = trimmedLine.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

                        if (parts.Length is 2)
                        {
                            string
                                baseJobId = parts[1];

                            if (baseJobId.StartsWith("Prod", StringComparison.OrdinalIgnoreCase)) continue;

                            string
                                idM1 = $"{baseJobId}_M1",
                                idM2 = $"{baseJobId}_M2";

                            if (GetNodes().TryGetValue(idM1, out Node? nodeM1))
                            {
                                path.Add(nodeM1);
                                LogGeneralActivity(LogSeverity.INFO,
                                    $"Node {idM1} added to solution path from file.", GeneralLogContext.LOAD);
                            }
                            else LogGeneralActivity(LogSeverity.WARNING,
                                $"Node {idM1} not found in current S-Graph, skipping...", GeneralLogContext.LOAD);

                            if (GetNodes().TryGetValue(idM2, out Node? nodeM2))
                            {
                                path.Add(nodeM2);
                                LogGeneralActivity(LogSeverity.INFO,
                                    $"Node {idM2} added to solution path from file.", GeneralLogContext.LOAD);
                            }
                            else LogGeneralActivity(LogSeverity.WARNING,
                                $"Node {idM2} not found in current S-Graph, skipping...", GeneralLogContext.LOAD);
                        }
                    }
                }

                if (GetNodes().TryGetValue("Prod_M1", out Node? prodM1Node))
                {
                    path.Add(prodM1Node);
                    LogGeneralActivity(LogSeverity.INFO,
                        "Node Prod_M1 appended to solution path.", GeneralLogContext.LOAD);
                }

                if (GetNodes().TryGetValue("Prod_M2", out Node? prodM2Node))
                {
                    path.Add(prodM2Node);
                    LogGeneralActivity(LogSeverity.INFO,
                        "Node Prod_M2 appended to solution path.", GeneralLogContext.LOAD);
                }

                LogGeneralActivity(LogSeverity.INFO,
                    $"Total of {path.Count} nodes loaded from solution file.", GeneralLogContext.LOAD);

                return path;
            }
            catch (Exception ex)
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    $"Error loading solution from file: {ex.Message}", GeneralLogContext.LOAD);
                MessageBox.Show($"Error loading solution from file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return [];
            }
        }

        // Gathers arguments for metaheuristic solvers via input boxes
        private static void GatherArgs4Metaheur(List<string> launchArgs)
        {
            const string
                DefTempStr = "1000.0",
                DefCoolStr = "0.995",
                DefIterStr = "5000";

            string
                inTemp = Interaction.InputBox("Enter Initial Starting Value (e.g., 1000.0):", "Solver Parameter", DefTempStr),
                inCool = Interaction.InputBox("Enter Initial Decreasing Rate (e.g., 0.995)", "Solver Parameter", DefCoolStr),
                inIter = Interaction.InputBox("Enter Initial Iteration Rate (e.g., 5000)", "Solver Parameter", DefIterStr);

            bool success =
                CheckValParse(DefTempStr, DefCoolStr, DefIterStr, inTemp, inCool, inIter, out double temp, out double cool, out int iter);

            if (success)
                launchArgs.AddRange
                    (
                    temp.ToString(CultureInfo.InvariantCulture),
                    cool.ToString(CultureInfo.InvariantCulture),
                    iter.ToString(CultureInfo.InvariantCulture)
                    );
            else
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    "Could not parse default solver parameters.", GeneralLogContext.INITIALIZATION);
                MessageBox.Show("Fatal error: Could not parse default solver parameters.",
                    "Internal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool CheckValParse(string deftemp, string defcool, string defiter, string intemp, string incool, string initer, out double temp, out double cool, out int i)
        {
            cool = 0;
            i = 0;

            return
                (double.TryParse(deftemp, NumberStyles.Float, CultureInfo.InvariantCulture, out temp) ||
                 double.TryParse(intemp, NumberStyles.Float, CultureInfo.InvariantCulture, out temp)) &&
                (double.TryParse(defcool, NumberStyles.Float, CultureInfo.InvariantCulture, out cool) ||
                 double.TryParse(incool, NumberStyles.Float, CultureInfo.InvariantCulture, out cool)) &&
                (int.TryParse(defiter, NumberStyles.Integer, CultureInfo.InvariantCulture, out i) ||
                 int.TryParse(initer, NumberStyles.Integer, CultureInfo.InvariantCulture, out i));
        }

        // Event handler for the About menu item click event (only a static, disposable window)
        private void About_Click(object sender, RoutedEventArgs e)
        {
            new AboutWindow().Show();
            LogGeneralActivity(LogSeverity.INFO,
                "\"About\" window opened!", GeneralLogContext.INITIALIZATION);
        }

        // Event handler for the zoom slider's PreviewMouseDown event
        private void ZoomSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Slider slider)
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    $"Zoom slider sender is not of the Slider class ({sender.GetType()})!", GeneralLogContext.MODIFY);
                return;
            }

            if (e.OriginalSource is FrameworkElement element && element.TemplatedParent is not Thumb)
            {
                Point pos = e.GetPosition(slider);
                double ratio = pos.X / slider.ActualWidth;
                double newValue = slider.Minimum + (slider.Maximum - slider.Minimum) * ratio;
                slider.Value = newValue;
                e.Handled = true; // Prevents slider from moving
                LogGeneralActivity(LogSeverity.INFO,
                    $"Zoom slider clicked at position {pos.X}, setting value to {newValue}!", GeneralLogContext.MODIFY);
            }
        }

        // Event handler for the zoom slider's ValueChanged event
        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (GanttData is null || GanttData.Count is 0) return;

            zoom = Math.Pow(2, e.NewValue);
            double scale = BaseTimeScale * zoom;

            double totalTime = GanttData.Max(x => x.Start + x.Duration);
            int rowC = GanttData.Count;

            GanttCanvas.LayoutTransform = Transform.Identity;

            GanttCanvas.Width = totalTime * scale + 100; // +100 for padding
            RulerCanvas.Width = GanttCanvas.Width;

            RenderGanttChart(GanttCanvas, GanttData, scale);
            DrawGanttRuler(RulerCanvas, GanttCanvas, totalTime, scale);

            LogGeneralActivity(LogSeverity.INFO,
                $"Zoom level changed to {e.NewValue}, scale set to {scale}!", GeneralLogContext.MODIFY);
        }

        // Helper expression-body method to check for unsaved changes in any of the data tables (dirty flag)
        private bool HasUnsavedChanges() =>
            isFileLoaded &&
            (masterTable.GetChanges() is not null ||
            recipeElementTable.GetChanges() is not null ||
            stepTable.GetChanges() is not null);

        // Event handler for opening a file
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (isFileLoaded)
            {
                if (HasUnsavedChanges())
                {
                    LogGeneralActivity(LogSeverity.WARNING,
                        "Attempting to open new file with unsaved changes present in the actual one; prompting user!", GeneralLogContext.LOAD);

                    MessageBoxResult res = MessageBox.Show("The current BatchML file has unsaved changes. Do you wish to save before loading a new file?",
                        "Unsaved changes detected", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                    if (res is MessageBoxResult.Yes) SaveFile_Click(sender, new());
                    else if (res is MessageBoxResult.Cancel)
                    {
                        LogGeneralActivity(LogSeverity.INFO, "New file opening cancelled by user (unsaved changes prompt).", GeneralLogContext.LOAD);
                        return;
                    }
                }

                Purge();
                PurgeDataTables();
            }

            LogGeneralActivity(LogSeverity.INFO,
                "Open file dialog initiated...", GeneralLogContext.LOAD);
            OpenFileDialog dlg = new()
            {
                DefaultExt = ".xml",
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() is true)
            {
                currentFilePath = dlg.FileName;
                LoadBatchML(currentFilePath);

                isFileLoaded = true;
                isFileModified = false;
                ManageFileHandlers();

                LogGeneralActivity(LogSeverity.INFO,
                    $"BatchML file loaded from {currentFilePath}!", GeneralLogContext.LOAD);
            }
            else LogGeneralActivity(LogSeverity.INFO,
                "Open file dialog cancelled by user.", GeneralLogContext.LOAD);
        }

        // Event handler for saving a file
        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (!isFileLoaded || masterRecipe is null)
            {
                LogGeneralActivity(LogSeverity.WARNING,
                    "No BatchML file loaded to save!", GeneralLogContext.SAVE);
                MessageBox.Show("No BatchML file loaded to save!",
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (isFileModified)
            {
                if ((sender as MenuItem)?.Tag.ToString()?.Equals("Exit") is true || (sender as MenuItem)?.Tag.ToString()?.Equals("Close File") is true)
                {
                    LogGeneralActivity(LogSeverity.WARNING,
                        "Prompting user to save changes before closing either the app or the file!", GeneralLogContext.SAVE);
                    MessageBox.Show("Please save changes before exiting!",
                        "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                var saveDlg = new SaveFileDialog
                {
                    DefaultExt = ".xml",
                    Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*"
                };

                if (saveDlg.ShowDialog() is true)
                {
                    try
                    {
                        masterRecipe.Document?.Save(saveDlg.FileName);
                        LogGeneralActivity(LogSeverity.INFO,
                            $"BatchML file successfully saved as {saveDlg.FileName}", GeneralLogContext.SAVE);
                        MessageBox.Show($"BatchML file saved as {saveDlg.FileName}",
                            "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                        isFileModified = false;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving BatchML file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        LogGeneralActivity(LogSeverity.ERROR,
                            $"Error saving BatchML file: {ex.Message}", GeneralLogContext.SAVE);
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
                    ManageFileHandlers();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}",
                        "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogGeneralActivity(LogSeverity.ERROR,
                        $"Error building S-Graph from XML: {ex.Message}", GeneralLogContext.S_GRAPH);
                }
            }
            else
            {
                MessageBox.Show("No BatchML File loaded! Drawing example graph...",
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                LogGeneralActivity(LogSeverity.WARNING,
                    "No BatchML file loaded, drawing example S-Graph!", GeneralLogContext.S_GRAPH);
                BuildExampleGraph();
                ManageFileHandlers();
            }
        }

        // A helper method to flush the containers and clean the canvases in one go
        private void Purge()
        {
            foreach (Canvas cv in new[] { SGraphCanvas, GanttCanvas, RulerCanvas })
            {
                cv.Children.Clear();
                LogGeneralActivity(LogSeverity.INFO,
                    $"{cv.Name} has been cleared!", GeneralLogContext.CLEAR);
            }

            GetNodes().Clear();
            LogGeneralActivity(LogSeverity.INFO,
                "S-Graph nodes cleared!", GeneralLogContext.CLEAR);

            GetEdges().Clear();
            LogGeneralActivity(LogSeverity.INFO,
                "S-Graph edges cleared!", GeneralLogContext.CLEAR);
        }

        // Clears all data tables
        private void PurgeDataTables()
        {
            masterTable.Columns.Clear();
            masterTable.Clear();
            LogGeneralActivity(LogSeverity.INFO,
                $"{masterTable.TableName} datatable purged!", GeneralLogContext.CLEAR);

            // removing the constraint before purging the entirety of the recipe element datatable
            if (recipeElementTable.PrimaryKey.Length > 0) recipeElementTable.PrimaryKey = [];
            recipeElementTable.Columns.Clear();
            recipeElementTable.Clear();
            LogGeneralActivity(LogSeverity.INFO,
                $"{recipeElementTable.TableName} datatable purged!", GeneralLogContext.CLEAR);

            stepTable.Columns.Clear();
            stepTable.Clear();
            LogGeneralActivity(LogSeverity.INFO,
                $"{stepTable.TableName} datatable purged!", GeneralLogContext.CLEAR);

            foreach (DataTable dt in solutionsList)
            {
                dt.Columns.Clear();
                dt.Clear();
                LogGeneralActivity(LogSeverity.INFO,
                    $"{dt.TableName} datatable purged!", GeneralLogContext.CLEAR);
            }

            for (int i = MainTab.Items.Count - 1; i >= 0; i--)
            {
                if (CheckPresentTabs((MainTab.Items[i] as TabItem)!))
                {
                    MainTab.Items.RemoveAt(i);
                    LogGeneralActivity(LogSeverity.INFO,
                        $"{(MainTab.Items[i] as TabItem)?.Header} tab removed!", GeneralLogContext.CLEAR);
                    continue;
                }
            }
        }

        // A helper expression-body method to check if a tab is one of the main tabs (to be removed when purging)
        private static bool CheckPresentTabs(TabItem tab) =>
            tab.Tag.ToString()?.Equals("MasterRecipe") is true ||
            tab.Tag.ToString()?.Equals("RecipeElements") is true ||
            tab.Tag.ToString()?.Equals("Steps") is true ||
            tab.Tag.ToString()?.Equals("Links") is true ||
            tab.Tag.ToString()?.Contains("Solution") is true;

        // A helper method to enable solvers after the initial S-Graph is rendered
        private void EnableSolvers()
        {
            SGraphExists = true;

            foreach (var x in SolverMenu.Items)
            {
                if (x is MenuItem menuItem && !menuItem.Name.Contains("Add new"))
                {
                    menuItem.IsEnabled = SGraphExists;
                    LogGeneralActivity(LogSeverity.INFO,
                        $"{menuItem.Tag} solver enabled!", GeneralLogContext.INITIALIZATION);
                }
                else continue;
            }
        }

        // Draws an example S-Graph with 9 equipment nodes and 3 product nodes (in case no BatchML file is loaded)
        private void BuildExampleGraph()
        {
            Purge();

            // demo graph's nodes
            Dictionary<string, (int T1, int T2, string Desc)> jobs = new()
            {
                {"J1", (3, 2, "Job 1")}, // M1 > M2 -> S2
                {"J2", (2, 4, "Job 2")}, // M1 <= M2 -> S1
                {"J3", (3, 2, "Job 3")}  // M1 > M2 -> S2
            };

            string[] optSeq = SortExampleJobs(jobs);

            double
                x_m1 = 100,
                x_m2 = x_m1 + 300,
                y = 70,
                vsp = 150;

            for (int i = 0; i < optSeq.Length; i++)
            {
                string jobID = optSeq[i];
                (int T1, int T2, string desc) = jobs[jobID];

                // M1 node
                AddNode($"{jobID}_M1", $"M1: {desc}", new(0, 0), T1, 0.0, false);

                // M2 node
                AddNode($"{jobID}_M2", $"M2: {desc}", new(0, 0), 0.0, T2, false);

                // technological precedence (J_i_M1 -> J_i_M2)
                AddEdge($"{jobID}_M1", $"{jobID}_M2");

                double currJobY = y + (i * vsp);

                if (GetNodes().TryGetValue($"{jobID}_M1", out Node? nodeM1)) nodeM1.Position = new(x_m1, currJobY);
                if (GetNodes().TryGetValue($"{jobID}_M2", out Node? nodeM2)) nodeM2.Position = new(x_m2, currJobY);
            }

            for (int i = 1; i <= 2; i++)
                AddNode($"Prod_M{i}", $"M{i}'s product", new(0, 0), 0.0, 0.0, true);

            if (GetNodes().TryGetValue("Prod_M1", out Node? pNodeM1)) pNodeM1.Position = new(x_m1, y + (optSeq.Length * vsp));
            if (GetNodes().TryGetValue("Prod_M2", out Node? pNodeM2)) pNodeM2.Position = new(x_m2, y + (optSeq.Length * vsp));

            RenderSGraph(SGraphCanvas);

            LogGeneralActivity(LogSeverity.INFO,
                "Example Flow Shop S-Graph drawn!", GeneralLogContext.S_GRAPH);
            EnableSolvers();
        }

        // Helper method for creating the demo S-graph
        private static string[] SortExampleJobs(Dictionary<string, (int T1, int T2, string Desc)> jobs)
        {
            List<string>
                s1 = [],
                s2 = [];

            var categJobs = jobs.Select(kvp => new
            {
                ID = kvp.Key,
                kvp.Value.T1,
                kvp.Value.T2,
                kvp.Value.Desc
            }).ToList();

            foreach (var job in categJobs)
            {
                if (job.T1 <= job.T2) s1.Add(job.ID);
                else s2.Add(job.ID);
            }

            s1.Sort((a, b) => jobs[a].T1.CompareTo(jobs[b].T1));
            s2.Sort((a, b) => jobs[b].T2.CompareTo(jobs[a].T2));

            return [.. s1, .. s2];
        }

        // Builds the S-Graph from the loaded BatchML file
        private void BuildSGraphFromXml()
        {
            Purge();

            var jobData = masterRecipe.Descendants(batchML + "Step")
                .Select(x =>
                {
                    string?
                        id = x.Element(batchML + "ID")?.Value.Trim(),
                        desc = x.Element(batchML + "RecipeElementID")?.Value.Trim(),
                        timeM1Str = x.Element(batchML + "Extension")?.Element(customNS + "TimeM1")?.Value,
                        timeM2Str = x.Element(batchML + "Extension")?.Element(customNS + "TimeM2")?.Value;

                    double
                        timeM1 = int.TryParse(timeM1Str, NumberStyles.Any, CultureInfo.InvariantCulture, out int t1) ? t1 : 0,
                        timeM2 = int.TryParse(timeM2Str, NumberStyles.Any, CultureInfo.InvariantCulture, out int t2) ? t2 : 0;

                    return new { id, desc, timeM1, timeM2 };
                })
                .Where(x => !string.IsNullOrEmpty(x.id) && !string.IsNullOrEmpty(x.desc))
                .DistinctBy(x => x.id)
                .ToList();

            if (jobData.Count is 0)
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    "No valid steps found in BatchML file to build S-Graph!", GeneralLogContext.S_GRAPH);
                return;
            }

            const double
                x_m1 = 100,
                x_m2 = 400,
                y = 70,
                vsp = 150;

            int jobI = 0;

            foreach (var job in jobData)
            {
                double currJobY = y + (jobI * vsp);
                
                // M1 Node
                AddNode($"{job.id}_M1", $"M1: {job.desc}", new(x_m1, currJobY), job.timeM1, 0, false);

                // M2 Node
                AddNode($"{job.id}_M2", $"M2: {job.desc}", new(x_m2, currJobY), 0, job.timeM2, false);

                LogGeneralActivity(LogSeverity.INFO,
                    $"Created nodes for {job.id} with times M1: {job.timeM1} and M2: {job.timeM2}", GeneralLogContext.S_GRAPH);

                // technological precedence edge (M1 -> M2 for the same job)
                // cost is M1's duration (ensuring M2 may not start J1 until M1 finishes with J1)
                AddEdge($"{job.id}_M1", $"{job.id}_M2", job.timeM1);
                LogGeneralActivity(LogSeverity.INFO,
                    $"Added technological edge {job.id}_M1 to {job.id}_M2 with cost {job.timeM1}.", GeneralLogContext.S_GRAPH);

                jobI++;
            }

            // Product nodes
            double finalY = y + (jobData.Count * vsp);
            AddNode($"Prod_M1", $"M1's product", new(x_m1, finalY), 0, 0, true);
            AddNode($"Prod_M2", $"M2's product", new(x_m2, finalY), 0, 0, true);

            // Render and enablement
            RenderSGraph(SGraphCanvas);
            LogGeneralActivity(LogSeverity.INFO,
                "Flow Shop S-Graph built from BatchML file!", GeneralLogContext.S_GRAPH);
            EnableSolvers();
        }

        // Loads the BatchML file and populates the data tables
        private void LoadBatchML(string path)
        {
            try
            {
                XDocument doc = XDocument.Load(path)
                    ?? throw new Exception("Error while loading file!");

                LogGeneralActivity(LogSeverity.INFO,
                    $"XML document loaded from {path}!", GeneralLogContext.LOAD);

                masterRecipe = doc.Descendants(batchML + "MasterRecipe").FirstOrDefault()
                    ?? throw new Exception("Master element not found in BatchML file.");

                LogGeneralActivity(LogSeverity.INFO,
                    "XML element established!", GeneralLogContext.LOAD);

                // Datatable of the master recipe's Header element
                DisplayMasterTable(masterTable, batchML);

                // Recipe Element datatable
                DisplayRecipeElementTable(recipeElementTable, batchML);

                // Steps datatable
                DisplayStepTable(stepTable, batchML);

                LogGeneralActivity(LogSeverity.INFO,
                    "BatchML file loaded and data tables populated!", GeneralLogContext.LOAD);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading BatchML file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogGeneralActivity(LogSeverity.ERROR,
                    $"Error loading BatchML file: {ex.Message}", GeneralLogContext.LOAD);
            }
        }

        // Handler event for closing an opened BatchML file
        private void CloseFile_Click(object sender, RoutedEventArgs e)
        {
            if (isFileModified && HasUnsavedChanges())
            {
                LogGeneralActivity(LogSeverity.WARNING,
                    "Attempting to close file with unsaved changes; prompting user!", GeneralLogContext.CLOSE);

                MessageBoxResult res = MessageBox.Show(
                    "The current BatchML file has unsaved changes. Do you wish to save before closing the file?",
                    "Unsaved changes detected",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (res is MessageBoxResult.Yes)
                {
                    masterTable.AcceptChanges();
                    recipeElementTable.AcceptChanges();
                    stepTable.AcceptChanges();
                    SaveFile_Click(sender, e);
                }
                else if (res is MessageBoxResult.No)
                    LogGeneralActivity(LogSeverity.INFO,
                        $"Closing file without saving (file modified: {isFileModified})", GeneralLogContext.CLOSE);
                else
                {
                    LogGeneralActivity(LogSeverity.INFO,
                        "Closing file cancelled by user.", GeneralLogContext.CLOSE);
                    return;
                }
            }

            try
            {
                Purge();
                PurgeDataTables();
                isFileLoaded = false;
                ManageFileHandlers();
                LogGeneralActivity(LogSeverity.INFO,
                    "BatchML file closed and all data cleared!", GeneralLogContext.CLOSE);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error closing BatchML: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogGeneralActivity(LogSeverity.ERROR,
                    $"Error closing BatchML: {ex.Message}", GeneralLogContext.CLOSE);
            }
        }

        // Creates and displays the data tables
        private void DisplayDataTable(DataTable dt, string tag, bool readOnly = false)
        {
            DataGrid grid = new()
            {
                ItemsSource = dt.DefaultView,
                AutoGenerateColumns = true,
                IsReadOnly = readOnly,
                CanUserAddRows = !readOnly
            };

            Grid container = new();
            container.Children.Add(grid);

            TabItem tab = new()
            {
                Header = dt.TableName,
                Content = container,
                Tag = tag
            };
            MainTab.Items.Insert(0, tab);
            MainTab.SelectedIndex = 0;

            LogGeneralActivity(LogSeverity.INFO,
                $"{dt.TableName} datatable displayed in new tab!", GeneralLogContext.DATATABLE);
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
                        if (SGraphCanvas.Children.Count is 0)
                        {
                            MessageBox.Show("No S-Graph to export!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        else JPEGExporter.ExportOneCanvas(SGraphCanvas, "S-Graph");
                        LogGeneralActivity(LogSeverity.INFO,
                            "S-Graph exported as JPEG file!", GeneralLogContext.EXPORT);
                        break;
                    }
                case "Gantt":
                    {
                        if (GanttCanvas.Children.Count is 0)
                        {
                            MessageBox.Show("No Gantt chart to export!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        else JPEGExporter.ExportMultipleCanvases(RulerCanvas, GanttCanvas, "Gantt Chart");
                        LogGeneralActivity(LogSeverity.INFO,
                            "Gantt chart exported as JPEG file!", GeneralLogContext.EXPORT);
                        break;
                    }
            }
        }

        // Displays the master recipe table
        private void DisplayMasterTable(DataTable mdt, XNamespace batchML)
        {
            XElement? header = masterRecipe.Element(batchML + "Header");

            string?
                id = header?.Element(batchML + "ID")?.Value,
                ver = header?.Element(batchML + "Version")?.Value,
                desc = header?.Elements(batchML + "Description").LastOrDefault()?.Value;

            mdt.Columns.Add("RecipeID");
            mdt.Columns.Add("Version");
            mdt.Columns.Add("Description");

            mdt.Rows.Add(id, ver, desc);

            mdt.RowChanged += MasterTable_RowChanged;

            DisplayDataTable(mdt, "MasterRecipe");
        }

        // Event handler for possible changes made inside the master recipe data table
        private void MasterTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            XElement header = masterRecipe.Element(batchML + "Header")
                ?? throw new InvalidOperationException("XML structure error: Header element not found!");

            header.SetElementValue(batchML + "ID", e.Row["RecipeID"]);
            header.SetElementValue(batchML + "Version", e.Row["Version"]);

            XElement? desc = header.Element(batchML + "Description");
            if (desc is null)
            {
                desc = new(batchML + "Description");
                header.Add(desc);
            }
            desc.Value = e.Row["Description"].ToString() ?? string.Empty;

            LogGeneralActivity(LogSeverity.INFO,
                "Changes made to Master Recipe table synced to XML!", GeneralLogContext.SYNC);
            isFileModified = true;
        }

        // Displays the recipe element table
        private void DisplayRecipeElementTable(DataTable redt, XNamespace batchML)
        {
            redt.Columns.Add("InternalKey", typeof(int));
            redt.Columns.Add("ID");
            redt.Columns.Add("Description");
            redt.Columns.Add("Type");

            int keyCounter = 0;

            var reList = masterRecipe.Descendants(batchML + "RecipeElement")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    Desc = x.Element(batchML + "Description")?.Value.Trim(),
                    Type = x.Element(batchML + "RecipeElementType")?.Value.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.ID) && !string.IsNullOrEmpty(x.Desc))
                .ToList();

            if (reList.Count > 0)
            {
                foreach (var re in reList)
                {
                    DataRow dr = redt.NewRow();
                    dr["ID"] = re.ID;
                    dr["Description"] = re.Desc;
                    dr["Type"] = re.Type;
                    dr["InternalKey"] = keyCounter++;
                    redt.Rows.Add(dr);
                }
                redt.PrimaryKey = [redt.Columns["InternalKey"]!];
            }
            redt.RowChanged += (s, e) =>
            {
                if (e.Row.RowState is not DataRowState.Deleted)
                {
                    SyncRow2XML(e.Row, "RecipeElements");
                    isFileModified = true;
                }
            };
            redt.RowDeleted += (s, e) =>
            {
                SyncRow2XML(e.Row, "RecipeElements");
                isFileModified = true;
            };


            if (reList.Count is 0)
                LogGeneralActivity(LogSeverity.WARNING,
                    "No Recipe Elements found in the BatchML file.", GeneralLogContext.DATATABLE);
            DisplayDataTable(redt, "RecipeElements");

            // Some DataGrid gymnastics to collapse the "InternalKey" column
            if (MainTab.SelectedItem is TabItem tab &&
                tab.Tag.ToString() is "RecipeElements" &&
                tab.Content is Grid Container &&
                Container.Children.OfType<DataGrid>().FirstOrDefault() is DataGrid dg)
                dg.Loaded += RecipElementTable_HideInternCol;
        }

        // Custom event handler to collapse the "InternalKey" column in the recipe elements datatable
        private void RecipElementTable_HideInternCol(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid final)
            {
                DataGridColumn? internalKeyCol = final.Columns.FirstOrDefault(c => c.Header.ToString() is "InternalKey");

                if (internalKeyCol is not null)
                {
                    internalKeyCol.Visibility = Visibility.Collapsed;
                    LogGeneralActivity(LogSeverity.INFO,
                        $"InternalKey column successfully hidden in '{recipeElementTable.TableName}.'", GeneralLogContext.DATATABLE);
                }
                else
                    LogGeneralActivity(LogSeverity.WARNING,
                        $"InternalKey column could not be found in DataGrid for {recipeElementTable.TableName} after loading!", GeneralLogContext.DATATABLE);
            }
        }

        // Displays the steps table
        private void DisplayStepTable(DataTable sdt, XNamespace batchML)
        {
            sdt.Columns.Add("ID");
            sdt.Columns.Add("RecipeElementID");
            sdt.Columns.Add("TimeM1", typeof(double));
            sdt.Columns.Add("TimeM2", typeof(double));

            var stepList = masterRecipe.Element(batchML + "ProcedureLogic")?.Descendants(batchML + "Step")
                .Select(x => new
                {
                    ID = x.Element(batchML + "ID")?.Value.Trim(),
                    REID = x.Element(batchML + "RecipeElementID")?.Value.Trim(),
                    TM1 = x.Element(batchML + "Extension")?.Descendants(customNS + "TimeM1").FirstOrDefault()?.Value,
                    TM2 = x.Element(batchML + "Extension")?.Descendants(customNS + "TimeM2").FirstOrDefault()?.Value
                })
                .Where(x => !string.IsNullOrEmpty(x.ID) && !string.IsNullOrEmpty(x.REID))
                .ToList();

            if (stepList?.Count > 0)
            {
                foreach (var step in stepList)
                {
                    DataRow dr = sdt.NewRow();
                    dr["ID"] = step.ID;
                    dr["RecipeElementID"] = step.REID;
                    dr["TimeM1"] = step.TM1;
                    dr["TimeM2"] = step.TM2;
                    sdt.Rows.Add(dr);
                }
                sdt.RowChanged += (s, e) =>
                {
                    if (e.Row.RowState is not DataRowState.Deleted)
                    {
                        SyncRow2XML(e.Row, "Steps");
                        isFileModified = true;
                    }
                };
                sdt.RowDeleted += (s, e) =>
                {
                    SyncRow2XML(e.Row, "Steps");
                    isFileModified = true;
                };
                DisplayDataTable(sdt, "Steps");
            }
            else
            {
                LogGeneralActivity(LogSeverity.WARNING,
                    "No Steps found in the BatchML file.", GeneralLogContext.DATATABLE);
                sdt.RowChanged += (s, e) =>
                {
                    if (e.Row.RowState is not DataRowState.Deleted)
                    {
                        SyncRow2XML(e.Row, "Steps");
                        isFileModified = true;
                    }
                };
                sdt.RowDeleted += (s, e) =>
                {
                    SyncRow2XML(e.Row, "Steps");
                    isFileModified = true;
                };
            }
        }

        // Displays the solution as a data table
        private void DisplaySolutionAsTable(List<GanttItem> ganttItems)
        {
            DataTable sdt = new($"Solution ({GanttCanvas.Tag.ToString() ?? string.Empty})");

            sdt.Columns.Add("TaskID");
            sdt.Columns.Add("StartTime (min)");
            sdt.Columns.Add("Duration (min)");
            sdt.Columns.Add("EndTime (min)");

            foreach (GanttItem item in ganttItems)
            {
                DataRow dr = sdt.NewRow();
                dr["TaskID"] = $"{item.ID}_{item.ResourceID}";
                dr["StartTime (min)"] = item.Start;
                dr["Duration (min)"] = item.Duration;
                dr["EndTime (min)"] = item.Start + item.Duration;
                sdt.Rows.Add(dr);
            }

            solutionsList.Add(sdt);
            DisplayDataTable(sdt, sdt.TableName, readOnly: true);
            LogGeneralActivity(LogSeverity.INFO,
                $"Solution table for \"{GanttCanvas.Tag ?? string.Empty}\" displayed in new tab!", GeneralLogContext.DATATABLE);
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

                string input = Interaction.InputBox("Enter a name for the custom solver:", "Custom Solver Name", FilePath.GetFileNameWithoutExtension(solverPath));

                if (!string.IsNullOrWhiteSpace(input))
                {
                    if (customSolvers.Any(s => s.Name.Equals(input, StringComparison.OrdinalIgnoreCase)))
                    {
                        LogGeneralActivity(LogSeverity.WARNING,
                            "A solver with this name already exists! Aborting assignment!", GeneralLogContext.EXTERN_SOLVER);
                        MessageBox.Show("A solver with this name already exists!", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    MessageBoxResult typeRes = MessageBox.Show(
                        "Is this an Iterative/Metaheuristic Solver (e.g., SA, GA) that requires numeric parameters?",
                        "Define Solver Type",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    string typeID = typeRes is MessageBoxResult.Yes ? "metaheuristic" : "deterministic";

                    CustomSolver newSolver = new()
                    {
                        Name = input.Trim(),
                        TypeID = typeID,
                        Path = solverPath,
                        Arguments = []
                    };
                    customSolvers.Add(newSolver);
                    LogGeneralActivity(LogSeverity.INFO,
                        $"Custom {newSolver.TypeID} solver \"{newSolver.Name}\" added with path: {newSolver.Path}", GeneralLogContext.EXTERN_SOLVER);

                    AddCustomSolverMenuItem(newSolver);
                    BuildSolverMenu(SolverMenu);
                }
            }
        }

        // Saves custom solvers to a JSON file within %AppData%
        private void SaveCustomSolvers2JSON()
        {
            try
            {
                Directory.CreateDirectory(FilePath.GetDirectoryName(customSolverPath)!);
                string json = JsonSerializer.Serialize(customSolvers, CachedOptions);
                File.WriteAllText(customSolverPath, json);
                LogGeneralActivity(LogSeverity.INFO, "Custom solvers saved to JSON file!", GeneralLogContext.SAVE);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving custom solvers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogGeneralActivity(LogSeverity.ERROR, $"Error saving custom solvers: {ex.Message}", GeneralLogContext.SAVE);
            }
        }

        // Loads custom solvers from a JSON file within %AppData%
        private void LoadCustomSolversFromJSON()
        {
            try
            {
                if (File.Exists(customSolverPath))
                {
                    string json = File.ReadAllText(customSolverPath);
                    List<CustomSolver> loaded = JsonSerializer.Deserialize<List<CustomSolver>>(json)!;
                    if (loaded is not null)
                    {
                        customSolvers.Clear();
                        customSolvers.AddRange(loaded);
                        foreach (var cs in customSolvers) AddCustomSolverMenuItem(cs);
                    }
                    LogGeneralActivity(LogSeverity.INFO,
                        "Custom solvers loaded from JSON file!", GeneralLogContext.SAVE);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading custom solvers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogGeneralActivity(LogSeverity.ERROR,
                    $"Error loading custom solvers: {ex.Message}", GeneralLogContext.SAVE);
            }
        }

        // Event handler to focus on the tab's content to be exported
        private void Focus4Export_Click(object sender, RoutedEventArgs e) =>
            ExportCanvasMenuItem.IsEnabled = (MainTab.SelectedItem as TabItem)?.Tag?.ToString() is "Gantt" or "SGraph";

        // Adds a custom solver to the Solver menu
        private void AddCustomSolverMenuItem(CustomSolver solver)
        {
            MenuItem item = new()
            {
                Header = $"{solver.Name} ({solver.TypeID})",
                Tag = solver.Name,
                IsEnabled = SGraphExists
            };
            item.Click += SolveClick;
            SolverMenu.Items.Add(item);
        }

        // Event handler for creating a solution table from the Gantt chart
        private void CreateSolutionTableMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (GanttCanvas.Children.Count is not 0) DisplaySolutionAsTable(GanttData);
            else
            {
                LogGeneralActivity(LogSeverity.WARNING,
                    "No Gantt chart was present to create solution table from!", GeneralLogContext.DATATABLE);
                MessageBox.Show("No Gantt chart to create solution table from!",
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // A helper method to find an existing XML element based on key columns in the DataRow
        private XElement? FindExistingElement(string tableName, DataRow dRow, DataRowVersion dVer)
        {
            bool mapExists = mappings.TryGetValue(tableName, out TableMapper? map);

            // if the master recipe doesn't exist
            if (masterRecipe is null || !mapExists)
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    $"Sync Error: No mapping found for table \"{tableName}\" or master recipe is null!", GeneralLogContext.SYNC);
                return null;
            }

            XElement? container = tableName is "Steps"
                ? masterRecipe.Element(batchML + "ProcedureLogic")
                : masterRecipe;

            if (container is null)
            {
                LogGeneralActivity(LogSeverity.WARNING,
                    $"Sync Warning: Could not find XML container for table \"{tableName}\".", GeneralLogContext.SYNC);
                return null;
            }

            string tgtID = dRow["ID", dVer]?.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(tgtID))
            {
                LogGeneralActivity(LogSeverity.WARNING,
                    $"Sync Warning: DataRow is missing a valid ID for table \"{tableName}\".", GeneralLogContext.SYNC);
                return null;
            }

            XElement? tgtEl = masterRecipe
                .Elements(batchML + map!.ParentElement)
                .FirstOrDefault(el =>
                el.Element(batchML + "ID")?.Value?.Trim().Equals(tgtID, StringComparison.OrdinalIgnoreCase) ?? false);

            if (tgtEl is not null)
            {
                LogGeneralActivity(LogSeverity.INFO,
                    $"Found ecipeElement by BatchML ID: {tgtID}", GeneralLogContext.SYNC);
                return tgtEl;
            }

            foreach (XElement el in container!.Elements(batchML + map!.ParentElement))
            {
                bool allMatch = true;
                foreach (string col in map.KeyCols)
                {
                    if (col is "InternalKey")
                        continue;

                    string
                        expected = dRow[col, dVer]?.ToString()?.Trim() ?? string.Empty,
                        xmlName = map.Col2El[col],
                        actual = el.Element(batchML + xmlName)?.Value?.Trim()!;

                    if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch)
                {
                    LogGeneralActivity(LogSeverity.INFO,
                        $"Found existing XML element for table \"{tableName}\" matching the DataRow keys.", GeneralLogContext.SYNC);
                    return el;
                }
            }

            LogGeneralActivity(LogSeverity.INFO, $"No existing XML element found for table \"{tableName}\" matching the DataRow keys.", GeneralLogContext.SYNC);
            return null;
        }

        // Custom non-delegate event handler for syncing a row from the DataTable to the XML when it's changed or added
        private void SyncRow2XML(DataRow dRow, string tableName)
        {
            if (masterRecipe is null || !mappings.TryGetValue(tableName, out var map))
            {
                LogGeneralActivity(LogSeverity.ERROR, $"No mapping found for table \"{tableName}\" or master recipe is null!", GeneralLogContext.SYNC);
                return;
            }

            // Determine which version of the row's data to use for the lookup
            // If the row was modified or deleted, it MUST use the original values to find it
            DataRowVersion ver2Use = (dRow.RowState is DataRowState.Modified or DataRowState.Deleted)
                ? DataRowVersion.Original
                : DataRowVersion.Current;

            bool missingKey = map.KeyCols.Any(col => dRow[col, ver2Use] == DBNull.Value || string.IsNullOrWhiteSpace(dRow[col, ver2Use]?.ToString()?.Trim()));

            if (missingKey)
            {
                string versionStr = ver2Use is DataRowVersion.Original ? "Original" : "Current";
                LogGeneralActivity(LogSeverity.WARNING,
                    $"Aborting sync for row in '{tableName}' (State: {dRow.RowState}): Missing required primary key data in {versionStr} version.", GeneralLogContext.SYNC);
                return;
            }

            // Call the updated FindExistingElement with the correct version
            XElement? targetElement = FindExistingElement(tableName, dRow, ver2Use);

            // Handle Deletion first
            if (dRow.RowState is DataRowState.Deleted)
            {
                if (targetElement is not null)
                {
                    targetElement.Remove();
                    LogGeneralActivity(LogSeverity.INFO, $"Deleted XML element from \"{tableName}\"", GeneralLogContext.SYNC);
                }
                return; // Stop processing after deletion
            }

            // If the element is non-existant, create it and add it to the document
            // This handles both 'Added' and 'Modified' rows where the key was changed
            if (targetElement is null)
            {
                // modification
                if (dRow.RowState is DataRowState.Modified)
                {
                    LogGeneralActivity(LogSeverity.ERROR,
                        $"Aborting sync for MODIFIED row in '{tableName}': Original XML element not found ->" +
                        $"{"structure is corrupted".ToUpper()}.",
                        GeneralLogContext.SYNC);
                    return;
                }

                // creation
                string newID = dRow["ID", DataRowVersion.Current]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(newID))
                {
                    LogGeneralActivity(LogSeverity.WARNING,
                        $"Aborting sync for row in '{tableName}': ID is null/empty.", GeneralLogContext.SYNC);
                    return;
                }

                targetElement = new XElement(batchML + map.ParentElement);
                XElement
                    insertionPoint = GetInsertionPoint(map.ParentElement),
                    last = insertionPoint.Elements(batchML + map.ParentElement).LastOrDefault()!;

                if (last is not null) last.AddAfterSelf(targetElement);
                else insertionPoint.Add(targetElement);
            }

            // --- UNIFIED UPDATE LOGIC ---
            // Update all the fields of "targetElement" with the CURRENT values from the DataRow.
            foreach (string col in map.Col2El.Keys)
            {
                string val = dRow[col, DataRowVersion.Current]?.ToString() ?? string.Empty;

                if (tableName.Equals("Steps") && (col.Equals("TimeM1") || col.Equals("TimeM2")))
                {
                    XElement ext = targetElement.Element(batchML + "Extension") ?? new(batchML + "Extension");
                    if (ext.Parent is null) targetElement.Add(ext);

                    XElement timeEl = ext.Element(customNS + col) ?? new(customNS + col);
                    timeEl.Value = val;
                    if (timeEl.Parent is null) ext.Add(timeEl);
                }
                else
                {
                    if (tableName is "RecipeElements" && col is "Type")
                        val = string.IsNullOrWhiteSpace(val) ? "Phase" : val;

                    XElement? child = targetElement.Element(batchML + map.Col2El[col]);

                    if (child is null)
                    {
                        child = new(batchML + map.Col2El[col]);

                        if (tableName.Equals("RecipeElements"))
                        {
                            if (col is "Description")
                            {
                                XElement? typeEl = targetElement.Element(batchML + "RecipeElementType");
                                if (typeEl is not null) typeEl.AddAfterSelf(child);
                                else targetElement.Add(child);
                            }
                            else if (col is "ID" or "Type") targetElement.AddBeforeSelf(child);
                            else targetElement.Add(child);
                        }
                        else targetElement.Add(child);
                    }

                    child.Value = val;
                }
            }

            LogGeneralActivity(LogSeverity.INFO, $"Synchronized DataRow to XML for table \"{tableName}\".", GeneralLogContext.SYNC);
        }

        private XElement GetInsertionPoint(string parentElName)
        {
            if (parentElName is "Step")
            {
                XElement? procLogic = masterRecipe.Element(batchML + "ProcedureLogic");
                if (procLogic is null)
                {
                    procLogic = new(batchML + "ProcedureLogic");
                    masterRecipe.Add(procLogic);
                    LogGeneralActivity(LogSeverity.WARNING,
                        "Missing <batchML:ProcedureLogic> element added to the master recipe!", GeneralLogContext.SYNC);
                }
                return procLogic;
            }
            return masterRecipe;
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