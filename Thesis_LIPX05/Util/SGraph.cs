using System.Windows;
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

        private readonly static Dictionary<string, Node> nodes = [];
        private readonly List<Edge> edges = [];

        public static Dictionary<string, Node> GetNodes() => nodes;

        public static void AddNode(string id, Point position)
        {
            if (!nodes.ContainsKey(id))
                nodes[id] = new() { ID = id, Position = position };
        }

        public void AddEdge(string fromID, string toID)
        {
            if (nodes.TryGetValue(fromID, out var from) && nodes.TryGetValue(toID, out var to) && from != null && to != null)
                edges.Add(new() { From = from, To = to });
        }

        public void Render(Canvas cv, int RowLimit)
        {
            cv.Children.Clear();
            double
                spacing = 150,
                radius = 35,
                startX = 0,
                startY = 0;

            // nodes
            var sortedNodes = nodes.Values.ToList();
            for (int i = 0; i < sortedNodes.Count; i++)
            {
                var node = sortedNodes[i];
                node.Position = new()
                {
                    X = (int)(startX + i % RowLimit * spacing + radius),
                    Y = (int)(startY + i / RowLimit * spacing + radius)
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
                    Foreground = Brushes.Black,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Width = radius * 2,
                    Height = radius / 2
                };
                Canvas.SetLeft(textBlock, node.Position.X - (radius / 2) + 18);
                Canvas.SetTop(textBlock, node.Position.Y + (radius / 1.333));
                cv.Children.Add(textBlock);
            }

            // edges
            foreach (var edge in edges)
                DrawArrow(cv, edge.From.Position, edge.To.Position, Brushes.DarkBlue);

            double
                maxHorSize = nodes.Values.Max(nHor => nHor.Position.X),
                maxVertSize = nodes.Values.Max(nVert => nVert.Position.Y);

            cv.Width = maxHorSize + 100;
            cv.Height = maxVertSize + 100;
        }

        private static void DrawArrow(Canvas cv, Point from, Point to, Brush color, double thickness = 2)
        {
            double radius = 35;

            var line = new Line
            {
                X1 = from.X + 2.0 * radius,
                Y1 = from.Y + radius,
                X2 = to.X,
                Y2 = to.Y + radius,
                Stroke = color,
                StrokeThickness = thickness
            };
            cv.Children.Add(line);

            // unidirectional arrow's head
            var dir = from - to;
            dir.Normalize();
            Vector normal = new(-dir.Y, dir.X);

            double
                length = 10,
                width = 5;

            Point
                base1 = to + dir * length + normal * width,
                base2 = to + dir * length - normal * width;

            var head = new Polygon
            {
                Points = [to, base1, base2],
                Fill = color,
                RenderTransform = new TranslateTransform(0, radius)
            };
            cv.Children.Add(head);
        }
    }
}