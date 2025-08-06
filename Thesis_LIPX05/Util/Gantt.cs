using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    internal class Gantt
    {
        // helper class to represent a Gantt chart item
        public class GanttItem
        {
            public string ID { get; set; } = string.Empty; // unique ID of the task
            public string Desc { get; set; } = string.Empty; // description of the task
            public double Start { get; set; } // start time of the task in minutes
            public double Duration { get; set; } // duration of the task in minutes
        }

        public static List<GanttItem> BuildChartFromPath(List<Node> path, List<Edge> edges)
        {
            var ganttItems = new List<GanttItem>();
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

        public static void DrawRuler(Canvas gcv, Canvas scv, double totalTime, double scale)
        {
            gcv.Children.Clear();
            int tickCount = (int)Math.Ceiling(totalTime);

            for (int i = 0; i <= tickCount; i++)
            {
                double x = i * scale;

                // vertical tick line across both canvases
                var tick = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = gcv.ActualHeight, // extended below the ruler to the end of GanttCanvas
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1
                };

                int lblInterval = (scale < 15) ? 2 : 1;
                TextBlock label;

                if (i % lblInterval == 0)
                {
                    label = new TextBlock
                    {
                        Text = $"{i}",
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.Black
                    };
                    Canvas.SetLeft(label, x + 2);
                    Canvas.SetTop(label, 0);
                    gcv.Children.Add(label);
                }

                gcv.Children.Add(tick);
            }

            double cvW = totalTime * scale + 100;

            gcv.Width = cvW;
            scv.Width = cvW;
        }

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
