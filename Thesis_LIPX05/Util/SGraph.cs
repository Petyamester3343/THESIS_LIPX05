using System.Drawing;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Thesis_LIPX05.Util
{
    internal class SGraph
    {
        // TODO: define, build, and render to a canvas an S-graph
        // with task nodes and edges as unidirectional transitions from one node to another
        public class Node
        {
            public required string ID { get; set; } // unique ID of the node
            public Point Position { get; set; } // position of the node in the graph
        }

        public class Edge // controlled edge between two or more nodes
        {
            public required Node From { get; set; }
            public required Node To { get; set; }
        }

        private readonly Dictionary<string, Node> nodes = [];
        private readonly List<Edge> edges = [];

        public void AddNode(string id, Point position)
        {
            if (!nodes.ContainsKey(id))
            {
                nodes[id] = new() { ID = id, Position = position };
            }
        }

        public void AddEdge(string fromID, string toID)
        {
            if (nodes.TryGetValue(fromID, out var from) && nodes.TryGetValue(toID, out var to))
            {
                edges.Add(new() { From = from, To = to });
            }
        }

        public void Render(Canvas cv)
        {
            cv.Children.Clear();
            double
                spacing = 100,
                radius = 30,
                startX = 50,
                startY = 50;

            var sortedNodes = nodes.Values.ToList();
            for (int i = 0; i < sortedNodes.Count; i++)
            {
                var node = sortedNodes[i];
                node.Position = new()
                {
                    X = (int)startX,
                    Y = (int)(startY + (i * spacing))
                };

                // node
                var ellipse = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = Brushes.LightBlue,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(ellipse, node.Position.X);
                Canvas.SetTop(ellipse, node.Position.Y);
                cv.Children.Add(ellipse);

                // label
                var textBlock = new TextBlock
                {
                    Text = node.ID,
                    Foreground = Brushes.White,
                    FontSize = 14
                };
                Canvas.SetLeft(textBlock, node.Position.X + radius + 5);
                Canvas.SetTop(textBlock, node.Position.Y + radius / 2);
                cv.Children.Add(textBlock);
            }

            foreach (var edge in edges)
            {
                var line = new Line
                {
                    X1 = edge.From.Position.X + radius,
                    Y1 = edge.From.Position.Y + radius,
                    X2 = edge.To.Position.X + radius,
                    Y2 = edge.To.Position.Y + radius,
                    Stroke = Brushes.DarkSlateBlue,
                    StrokeThickness = 2
                };
                cv.Children.Add(line);
            }
        }
    }
}