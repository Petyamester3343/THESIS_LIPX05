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
        }

        // Builds a Gantt chart from an S-Graph
        public static List<GanttItem> BuildChartFromPath(List<Node> path, List<Edge> edges)
        {
            List<GanttItem> ganttItems = [];
            double currT = 0;

            for (int i = 0; i < path.Count - 1; i++)
            {
                Node
                    from = path[i],
                    to = path[i + 1];

                var edge = edges.FirstOrDefault(e => e.From == from && e.To == to);
                if (edge == null) continue;

                ganttItems.Add(new()
                {
                    ID = to.ID,
                    Desc = to.Desc,
                    Start = currT,
                    Duration = edge.Cost
                });

                currT += edge.Cost;
            }

            Log(LogSeverity.INFO, $"Gantt chart built with {ganttItems.Count} items.");
            return ganttItems;
        }

        // Draws the ruler on the Gantt chart canvas and the scroll view
        public static void DrawRuler(Canvas cv1, Canvas cv2, double totalTime, double scale)
        {
            cv1.Children.Clear();

            foreach (Canvas cv in new[] { cv1, cv2 })
            {
                cv.SnapsToDevicePixels = true;
                cv.UseLayoutRounding = true;
            }

            Log(LogSeverity.INFO,
                $"Canvases are ready to be used!");

            int tickCount = Convert.ToInt32(Math.Ceiling(totalTime));

            double lblCentY = 15;

            int
                ticksDrawn = 0,
                labelsDrawn = 0;

            for (int i = 0; i <= tickCount; i++)
            {
                double x = Math.Round(i * scale) + 0.5;

                // vertical tick line across both canvases
                Line tick = new()
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = cv1.ActualHeight + cv2.ActualHeight, // extended below the ruler to the end of GanttCanvas
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1,
                    SnapsToDevicePixels = true,
                };
                RenderOptions.SetEdgeMode(tick, EdgeMode.Aliased);
                cv1.Children.Add(tick);
                ticksDrawn++;

                int lblInterval = (scale < 15) ? 2 : 1;

                if (i % lblInterval == 0)
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
            }

            Log(LogSeverity.INFO,
                $"Ruler drawn: {ticksDrawn} ticks and {labelsDrawn} labels.");

            double cvW = totalTime * scale + 100;
            cv1.Width = cvW;
            cv2.Width = cvW;
        }

        // Draws the Gantt chart on the provided canvas
        public static void Render(Canvas cv, List<GanttItem> items, double scale)
        {
            cv.Children.Clear();
            double rowH = 30;

            int itemsDrawn = 0;

            for (int i = 0; i < items.Count; i++)
            {
                GanttItem item = items[i];

                double
                    x = item.Start * scale,
                    w = item.Duration * scale,
                    y = i * rowH;

                Rectangle rectangle = new()
                {
                    Width = Math.Round(w),
                    Height = Math.Round(rowH - 5),
                    Fill = Brushes.LightGreen,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                };

                Canvas.SetLeft(rectangle, x);
                Canvas.SetTop(rectangle, y);
                cv.Children.Add(rectangle);

                TextBlock label = new()
                {
                    Text = $"{item.ID}",
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
                    FontWeight = FontWeights.Bold,
                };

                Canvas.SetLeft(label, x + 4);
                Canvas.SetTop(label, y + 5);
                cv.Children.Add(label);
                itemsDrawn++;
            }

            Log(LogSeverity.INFO,
                $"Gantt chart rendering complete: {itemsDrawn} items was drawn.");

            double maxTime = items.Count != 0 ? items.Max(i => i.Start + i.Duration) : 0;
            cv.Width = maxTime * scale + 100; // adjust width based on max time
            cv.Height = items.Count * rowH + 50; // adjust height based on number of items
        }
    }
}
