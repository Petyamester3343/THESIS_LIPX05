using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using static Thesis_LIPX05.Util.LogManager;
using static Thesis_LIPX05.Util.SGraph;

using NodeList = System.Collections.Generic.List<Thesis_LIPX05.Util.SGraph.Node>;
using EdgeList = System.Collections.Generic.List<Thesis_LIPX05.Util.SGraph.Edge>;
using GanttList = System.Collections.Generic.List<Thesis_LIPX05.Util.Gantt.GanttItem>;

namespace Thesis_LIPX05.Util
{
    internal class Gantt
    {
        // A nested helper class to represent a Gantt chart item
        public class GanttItem
        {
            public string ID { get; set; } = string.Empty; // unique ID of the task
            public string Desc { get; set; } = string.Empty; // description of the task
            public double Start { get; set; } // start time of the task in minutes
            public double Duration { get; set; } // duration of the task in minutes
            public required string ResourceID { get; set; } // ID of the resource (e.g., machine) assigned to the task
        }

        // Builds a Gantt chart from an S-Graph
        public static GanttList BuildChartFromPath(NodeList path, EdgeList edges)
        {
            Dictionary<string, double> EFT = [];
            GanttList ganttItems = [];

            foreach (Node n in path) EFT[n.ID] = 0.0;

            foreach (Node curr in path)
            {
                if (curr.ID.StartsWith('P')) continue;

                double dur = curr.TimeM1 > 0 ? curr.TimeM1 : curr.TimeM2;

                bool isTask = !(curr.ID.StartsWith('P') || dur <= 0);
                if (!isTask) dur = 0.0;

                double est = 0.0; // earliest start time
                string decidingPredID = "None";

                EdgeList pred = [.. edges.Where(e => e.To.ID == curr.ID)];

                foreach (Edge e in pred)
                {
                    if (EFT.TryGetValue(e.From.ID, out double predEFT))
                    {                        
                        double reqStart = predEFT;

                        if (reqStart > est)
                        {
                            est = reqStart;
                            decidingPredID = e.From.ID;
                        }
                    }
                }

                if (decidingPredID is not "None")
                    LogGeneralActivity(LogSeverity.INFO,
                        $"Node {curr.ID} EST is determined by {decidingPredID} (EST: {est:F2}).", GeneralLogContext.GANTT);
                else
                    LogGeneralActivity(LogSeverity.INFO,
                        $"Node {curr.ID} EST is 0 (no predecessors).", GeneralLogContext.GANTT);
                
                EFT[curr.ID] = est + dur;
                
                if (isTask)
                {
                    ganttItems.Add(new()
                    {
                        ID = curr.ID.Replace("_M1", "").Replace("_M2", ""),
                        Desc = curr.Desc,
                        Start = est,
                        Duration = dur,
                        ResourceID = curr.ID.EndsWith("M1") ? "M1" : "M2"
                    });
                }
            }

            LogGeneralActivity(LogSeverity.INFO,
                $"Gantt chart built with {ganttItems.Count} items from path with {path.Count} nodes.", GeneralLogContext.GANTT);
            return [.. from ganttItem in ganttItems orderby ganttItem.ResourceID, ganttItem.Start select ganttItem];
            
            // analogous with the following:
            // return [.. ganttItems.OrderBy(i => i.ResourceID).ThenBy(i => i.Start)];
        }

        // Draws the ruler on the Gantt chart canvas and the scroll view
        public static void DrawGanttRuler(Canvas cv1, Canvas cv2, double totalTime, double scale)
        {
            cv1.Children.Clear();

            foreach (Canvas cv in new[] { cv1, cv2 })
            {
                cv.SnapsToDevicePixels = true;
                cv.UseLayoutRounding = true;
            }

            LogGeneralActivity(LogSeverity.INFO,
                $"Canvases are ready to be used!", GeneralLogContext.GANTT);

            const double rowH = 30;
            const int numResources = 2;
            double expGanttHeight = numResources * rowH + 50;

            if (double.IsNaN(cv2.Height) || cv2.Height < expGanttHeight) cv2.Height = expGanttHeight;

            double lblCentY = 15;
            int
                tickCount = (int)Math.Ceiling(totalTime),
                ticksDrawn = 0,
                labelsDrawn = 0,
                lblInterval = (scale < 15) ? 2 : 1;

            for (int i = 0; i <= tickCount; i++)
            {
                double x = Math.Round(i * scale) + 0.5;

                if (i % lblInterval is 0)
                {
                    TextBlock label = new()
                    {
                        Text = $"{i}",
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.Black,
                        RenderTransform = new RotateTransform(-90),
                        RenderTransformOrigin = new(0.5, 0.5), // It's a Point
                        SnapsToDevicePixels = true,
                        UseLayoutRounding = true,
                    };

                    label.Measure(availableSize: new(double.PositiveInfinity, double.PositiveInfinity));
                    Size measured = label.DesiredSize;

                    double
                        left = Math.Round(x - (measured.Width / 2.0) + 5),
                        top = Math.Round(lblCentY - (measured.Height / 2.0));

                    Canvas.SetLeft(label, left);
                    Canvas.SetTop(label, top);
                    cv1.Children.Add(label);
                    labelsDrawn++;
                }

                // vertical tick line across both canvases
                Line tick = new()
                {
                    X1 = x,
                    X2 = x,
                    Y1 = cv1.ActualHeight - 8,
                    Y2 = cv1.ActualHeight, // extended below the ruler to the end of GanttCanvas
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1,
                    SnapsToDevicePixels = true,
                };
                RenderOptions.SetEdgeMode(tick, EdgeMode.Aliased);
                cv1.Children.Add(tick);
                ticksDrawn++;
            }

            LogGeneralActivity(LogSeverity.INFO,
                $"Ruler drawn: {ticksDrawn} ticks and {labelsDrawn} labels.", GeneralLogContext.GANTT);

            double cvW = totalTime * scale + 100;
            cv1.Width = cvW;
            cv2.Width = cvW;
        }

        private static void DrawGanttGridLines(Canvas cv, double totalT, double scale)
        {
            int tick = (int)Math.Ceiling(totalT);

            for (int t = 0; t <= tick; t++)
            {
                double x = Math.Round(t * scale) + 0.5;

                // grid line on Gantt chart
                Line grid = new()
                {
                    X1 = x,
                    X2 = x,
                    Y1 = 0,
                    Y2 = cv.Height,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1,
                    SnapsToDevicePixels = true,
                };
                RenderOptions.SetEdgeMode(grid, EdgeMode.Aliased);
                cv.Children.Add(grid);
            }
        }

        // Draws the Gantt chart on the provided canvas
        public static void RenderGanttChart(Canvas cv, GanttList items, double scale)
        {
            cv.Children.Clear();
            double rowH = 30;

            List<IGrouping<string, GanttItem>> groupedItems = [.. 
                items.GroupBy(i => i.ResourceID)
                .OrderBy(g => g.Key)];

            Dictionary<string, int> resourceRow = groupedItems
                .Select((g, idx) => new { g.Key, Index = idx })
                .ToDictionary(x => x.Key, x => x.Index);

            double totalTime = items.Count is not 0 ? items.Max(i => i.Start + i.Duration) : 0;
            
            DrawGanttGridLines(cv, totalTime, scale);

            int itemsDrawn = 0;

            foreach (GanttItem item in items)
            {
                int rIdx = resourceRow[item.ResourceID];

                double
                    x = item.Start * scale,
                    w = item.Duration * scale,
                    y = rIdx * rowH; // vertical position based on row index

                Rectangle ganttRect = new()
                {
                    Width = Math.Round(w),
                    Height = Math.Round(rowH - 5),
                    Fill = item.ResourceID == "M1" ? Brushes.LightCoral : Brushes.LightSteelBlue, // color differentiation for the two machines
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                Canvas.SetLeft(ganttRect, x);
                Canvas.SetTop(ganttRect, y);
                cv.Children.Add(ganttRect);

                TextBlock txtBlock = new()
                {
                    Text = $"{item.ID} ({item.ResourceID})",
                    ToolTip = new ToolTip
                    {
                        Content = new TextBlock
                        {
                            Text = $"{item.Desc}\nStart: {item.Start:F2} min\nDuration: {item.Duration:F2} min",
                            FontSize = 12,
                            Foreground = Brushes.Black
                        }
                    },
                    FontSize = 12,
                    FontWeight = FontWeights.Bold
                };

                Canvas.SetLeft(txtBlock, x + 4);
                Canvas.SetTop(txtBlock, y + 5);
                cv.Children.Add(txtBlock);
                itemsDrawn++;
            }

            LogGeneralActivity(LogSeverity.INFO,
                $"Gantt chart rendered with {itemsDrawn} items on canvas.", GeneralLogContext.GANTT);

            double maxTime = items.Count is not 0 ? items.Max(i => i.Start + i.Duration) : 0;
            cv.Width = maxTime * scale + 100;
            cv.Height = groupedItems.Count * rowH + 50; // +50 for padding

            LogGeneralActivity(LogSeverity.INFO,
                $"Canvas resized to {cv.Width}x{cv.Height} to fit all Gantt items.", GeneralLogContext.GANTT);
        }
    }
}
