using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

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
                var from = path[i];
                var to = path[i + 1];

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

            return ganttItems;
        }

        // Draws the ruler on the Gantt chart canvas and the scroll view
        public static void DrawRuler(Canvas gcv, Canvas scv, double totalTime, double scale)
        {
            gcv.SnapsToDevicePixels = true;
            scv.SnapsToDevicePixels = true;
            gcv.UseLayoutRounding = true;
            scv.UseLayoutRounding = true;

            gcv.Children.Clear();
            int tickCount = (int)Math.Ceiling(totalTime);

            double lblCentY = 15;

            for (int i = 0; i <= tickCount; i++)
            {
                double x = i * scale;

                // vertical tick line across both canvases
                var tick = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = Math.Max(gcv.ActualHeight, 200), // extended below the ruler to the end of GanttCanvas
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1,
                    SnapsToDevicePixels = true,
                };
                RenderOptions.SetEdgeMode(tick, EdgeMode.Aliased);
                gcv.Children.Add(tick);

                int lblInterval = (scale < 15) ? 2 : 1;
                TextBlock label;

                if (i % lblInterval == 0)
                {
                    label = new TextBlock
                    {
                        Text = $"{i}",
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.Black,
                        RenderTransform = new RotateTransform(-90),
                        RenderTransformOrigin = new Point(0.5, 0.5),
                        SnapsToDevicePixels = true,
                        UseLayoutRounding = true,
                    };

                    TextOptions.SetTextFormattingMode(label, TextFormattingMode.Display);

                    RenderOptions.SetBitmapScalingMode(label, BitmapScalingMode.HighQuality);
                    RenderOptions.SetEdgeMode(label, EdgeMode.Aliased);

                    label.Measure(availableSize: new(double.PositiveInfinity, double.PositiveInfinity));
                    var measured = label.DesiredSize;

                    double
                        left = Math.Round(x - (measured.Width / 2.0) + 5),
                        top = Math.Round(lblCentY - (measured.Height / 2.0));

                    Canvas.SetLeft(label, left);
                    Canvas.SetTop(label, top);
                    gcv.Children.Add(label);
                }
            }

            double cvW = totalTime * scale + 100;
            gcv.Width = cvW;
            scv.Width = cvW;
        }

        // Draws the Gantt chart on the provided canvas
        public static void Render(Canvas cv, List<GanttItem> items, double scale)
        {
            cv.Children.Clear();
            double rowH = 30;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                double
                    x = item.Start * scale,
                    w = item.Duration * scale,
                    y = i * rowH;

                var r = new Rectangle
                {
                    Width = (int)w,
                    Height = (int)(rowH - 5),
                    Fill = Brushes.LightGreen,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                };

                Canvas.SetLeft(r, x);
                Canvas.SetTop(r, y);
                cv.Children.Add(r);

                var tooltip = new ToolTip
                {
                    Content = new TextBlock
                    {
                        Text = $"{item.Desc}\nStart: {item.Start} min\nDuration: {item.Duration} min",
                        FontSize = 12,
                        Foreground = Brushes.Black
                    },
                    Width = 200,
                    Height = 60
                };

                var label = new TextBlock
                {
                    Text = $"{item.ID}",
                    ToolTip = tooltip,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                };

                Canvas.SetLeft(label, x + 4);
                Canvas.SetTop(label, y + 5);
                cv.Children.Add(label);
            }

            cv.Width = items.Max(i => i.Start + i.Duration) * scale + 100; // adjust width based on max time
            cv.Height = items.Count * rowH + 50; // adjust height based on number of items
        }
    }
}
