using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using static Thesis_LIPX05.Util.LogManager;
using static Thesis_LIPX05.Util.SGraph;

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
        public static List<GanttItem> BuildChartFromPath(List<Node> path, List<Edge> edges)
        {
            Dictionary<string, double> earliestFinishTime = [];
            List<GanttItem> ganttItems = [];

            foreach (Node n in path) earliestFinishTime[n.ID] = 0.0;

            foreach (Node curr in path)
            {
                double dur = curr.TimeM1 > 0 ? curr.TimeM1 : curr.TimeM2;
                if (dur <= 0)
                {
                    LogGeneralActivity(LogSeverity.WARNING,
                        $"Node {curr.ID} has non-positive duration ({dur}), skipping...", GeneralLogContext.GANTT);
                    continue;
                }

                string resID = curr.ID.EndsWith("M1") ? "M1" : "M2";

                double est = 0.0; // earliest start time
                string decidingPredID = "None";

                List<Edge> pred = [.. edges.Where(e => e.To.ID == curr.ID)];

                foreach (Edge e in pred)
                {
                    if (earliestFinishTime.TryGetValue(e.From.ID, out double predEFT))
                    {
                        double reqStart = predEFT + e.Cost;
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
                        $"Node {curr.ID} EST is 0 (no predecessors.)", GeneralLogContext.GANTT);

                earliestFinishTime[curr.ID] = est + dur;

                ganttItems.Add(new()
                {
                    ID = curr.ID.Replace("_M1", "").Replace("_M2", ""),
                    Desc = curr.Desc,
                    Start = est,
                    Duration = dur,
                    ResourceID = resID
                });
            }

            LogGeneralActivity(LogSeverity.INFO,
                $"Gantt chart built with {ganttItems.Count} items from path with {path.Count} nodes.", GeneralLogContext.GANTT);
            return [.. ganttItems.OrderBy(i => i.ResourceID).ThenBy(i => i.Start)];
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

                // grid line on Gantt chart
                Line grid = new()
                {
                    X1 = x,
                    X2 = x,
                    Y1 = 0,
                    Y2 = cv2.Height,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1,
                    SnapsToDevicePixels = true,
                };
                RenderOptions.SetEdgeMode(grid, EdgeMode.Aliased);
                cv2.Children.Add(grid);

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

        // Draws the Gantt chart on the provided canvas
        public static void RenderGanttChart(Canvas cv, List<GanttItem> items, double scale)
        {
            cv.Children.Clear();
            double rowH = 30;

            List<IGrouping<string, GanttItem>> groupedItems = [.. items.GroupBy(i => i.ResourceID).OrderBy(g => g.Key)];

            Dictionary<string, int> resourceRow = groupedItems
                .Select((g, idx) => new { g.Key, Index = idx })
                .ToDictionary(x => x.Key, x => x.Index);

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

            for (int r = 0; r < groupedItems.Count; r++)
            {
                TextBlock rscLbl = new()
                {
                    Text = groupedItems[r].Key,
                    FontSize = 14,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = Brushes.DarkBlue
                };
                Canvas.SetLeft(rscLbl, -60);
                Canvas.SetTop(rscLbl, r * rowH + 5);
                cv.Children.Add(rscLbl);
            }

            LogGeneralActivity(LogSeverity.INFO,
                $"Canvas resized to {cv.Width}x{cv.Height} to fit all Gantt items.", GeneralLogContext.GANTT);
        }
    }
}
