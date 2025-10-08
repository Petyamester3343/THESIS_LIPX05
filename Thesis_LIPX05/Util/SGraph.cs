using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;

using FilePath = System.IO.Path;
using ShapePath = System.Windows.Shapes.Path;

using static System.Environment;
using static Thesis_LIPX05.Util.LogManager;

namespace Thesis_LIPX05.Util
{
    // The class for S-Graphs
    public class SGraph
    {
        // The nested class representing a node in the graph
        public class Node
        {
            public required string ID { get; set; } // unique ID of the node
            public required string Desc { get; set; } // description of the node
            public Point Position { get; set; } // position of the node on the canvas
            public static double Radius { get; set; } = 35; // radius of the node, default is 35 (for visual representation)
        }

        // The nested class representing a unidirectional edge between two nodes
        public class Edge // a unidirectional edge between two or more nodes
        {
            public required Node From { get; set; }
            public required Node To { get; set; }
            public required double Cost { get; set; }
        }

        private readonly static List<Edge> edges = [];
        private readonly static Dictionary<string, Node> nodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly static Dictionary<Point, int> edgeCountFromNode = [];

        // Getters for nodes and edges
        public static Dictionary<string, Node> GetNodes() => nodes;
        public static List<Edge> GetEdges() => edges;

        // Adds a node to the graph
        public static void AddNode(string id, string desc, Point position)
        {
            if (!nodes.ContainsKey(id)) nodes.Add(id, new() { ID = id, Desc = desc, Position = position });
        }

        // Adds an edge to the graph
        public static void AddEdge(string fromID, string toID, double cost)
        {
            if (nodes.TryGetValue(fromID, out var from)
                && nodes.TryGetValue(toID, out var to)
                && from != null
                && to != null
                && !double.IsNaN(cost)) edges.Add(new() { From = from, To = to, Cost = cost });
        }

        // Renders the graph on the given canvas
        public static void Render(Canvas cv, int rowLimit)
        {
            cv.Children.Clear();
            edgeCountFromNode.Clear();

            double
                spacing = 150,
                startX = 0,
                startY = 0;

            // nodes
            var sortedNodes = nodes.Values.ToList();
            for (int i = 0; i < sortedNodes.Count; i++)
            {
                Node node = sortedNodes[i];
                node.Position = new()
                {
                    X = (int)(startX + (i % rowLimit * spacing) + Node.Radius),
                    Y = (int)(startY + (i / rowLimit * spacing) + Node.Radius)
                };

                // one node with a tooltip
                Ellipse graphNode = new()
                {
                    Width = Node.Radius * 2,
                    Height = Node.Radius * 2,
                    Fill = Brushes.LightBlue,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    ToolTip = new ToolTip
                    {
                        Content = new TextBlock
                        {
                            Text = $"ID: {node.Desc}",
                            FontSize = 12,
                            Width = 300,
                            Foreground = Brushes.Black,
                            TextWrapping = TextWrapping.Wrap,
                        }
                    }
                };

                // and its label
                TextBlock txt = new()
                {
                    Text = node.ID,
                    Foreground = Brushes.Black,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Width = Node.Radius * 2,
                    Height = Node.Radius / 2,
                };

                Canvas.SetLeft(graphNode, node.Position.X);
                Canvas.SetTop(graphNode, node.Position.Y);
                cv.Children.Add(graphNode);
                MainWindow.GetLogger().Log(LogSeverity.INFO, $"Node {node.ID} at {node.Position.X};{node.Position.Y}");

                Canvas.SetLeft(txt, node.Position.X - (Node.Radius / 2) + 18);
                Canvas.SetTop(txt, node.Position.Y + (Node.Radius / 1.333));
                cv.Children.Add(txt);
                MainWindow.GetLogger().Log(LogSeverity.INFO, $"Label {node.ID} at {node.Position.X - (Node.Radius / 2) + 18};{node.Position.Y + (Node.Radius / 1.333)}");
            }

            // edges
            foreach (var edge in edges)
            {
                DrawEdge(cv, edge.From.Position, edge.To.Position, Brushes.DarkBlue, edge.Cost);
                MainWindow.GetLogger().Log(LogSeverity.INFO, $"Edge from {edge.From.ID} to {edge.To.ID} with cost {edge.Cost}");
            }

            double
                maxHorSize = nodes.Values.Max(nHor => nHor.Position.X),
                maxVertSize = nodes.Values.Max(nVert => nVert.Position.Y);

            // set the canvas size to fit all nodes and edges
            cv.Width = maxHorSize + 100;
            cv.Height = maxVertSize + 100;

            MainWindow.GetLogger().Log(LogSeverity.INFO, $"Canvas size set to {cv.Width}x{cv.Height}");
        }

        // Draws a quadratic Bezier curve with a triangular polygon at its end between two nodes on the provided canvas
        private static void DrawEdge(Canvas cv, Point from, Point to, Brush color, double weight, double thickness = 2)
        {
            // Calculating node centers
            Point centerFrom = new(from.X + Node.Radius, from.Y + Node.Radius);
            Point centerTo = new(to.X + Node.Radius, to.Y + Node.Radius);

            // Calculating connection points
            Vector delta = centerTo - centerFrom;
            double dist = delta.Length;

            // If nodes are too close, skip drawing edges
            if (dist <= Node.Radius * 2 + 1)
            {
                MainWindow.GetLogger().Log(LogSeverity.WARNING,
                    "Nodes are too close or overlapping; skipping edge line!");
                return;
            }

            Vector dirTo = delta / dist; // normalized direction from A's center to B's center

            // Calculating connection points on the circumference
            Point
                start = centerFrom + dirTo * Node.Radius,
                end = centerTo - dirTo * Node.Radius; // subtract to move backward from centerTo

            // Handling multiple edges
            edgeCountFromNode[from] = edgeCountFromNode.TryGetValue(from, out int edgeCount) ? ++edgeCount : 1;

            // If there are multiple edges, curve the nth (where n > 1), otherwise do nothing
            double dCurve = (edgeCount > 1) ? 20 * (edgeCount - 1) : 0;

            // Calculating the Bezier curve's control point
            Vector normal = new(-dirTo.Y, dirTo.X); // 90 degree rotation
            Point
                mid = new((start.X + end.X) / 2, (start.Y + end.Y) / 2),
                control = mid + normal * dCurve;

            // Drawing the edge
            PathFigure figure = new() { StartPoint = start };
            QuadraticBezierSegment segment = new() { Point1 = control, Point2 = end };
            figure.Segments.Add(segment);

            PathGeometry geo = new();
            geo.Figures.Add(figure);
            ShapePath shapePath = new() { Stroke = color, StrokeThickness = thickness, Data = geo };
            cv.Children.Add(shapePath);
            MainWindow.GetLogger().Log(LogSeverity.INFO,
                $"Edge drawn from ({start.X:F1};{start.Y:F1}) to ({end.X:F1};{end.Y:F1})!");

            // Drawing the arrowhead
            // It relies on the geometrically correct 'end' point
            Vector arrowDir = control - end;
            arrowDir.Normalize();
            Vector arrowNorm = new(-arrowDir.Y, arrowDir.X);

            double
                headL = 10,
                headW = 5;

            Point
                b1 = end + arrowDir * headL + arrowNorm * headW,
                b2 = end + arrowDir * headL - arrowNorm * headW;

            Polygon arrowHead = new()
            {
                Points = [end, b1, b2],
                Fill = color
            };

            cv.Children.Add(arrowHead);

            // Drawing the weight label
            double t = 0.5;
            Point midCurve = new()
            {
                X = Math.Pow(1 - t, 2) * start.X + 2 * (1 - t) * t * control.X + Math.Pow(t, 2) * end.X,
                Y = Math.Pow(1 - t, 2) * start.Y + 2 * (1 - t) * t * control.Y + Math.Pow(t, 2) * end.Y
            };

            TextBlock weightTXT = new()
            {
                Text = Convert.ToInt32(weight).ToString(),
                Foreground = Brushes.Black,
                Background = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Width = 18,
                Height = 12
            };

            Canvas.SetLeft(weightTXT, midCurve.X - weightTXT.Width / 2);
            Canvas.SetTop(weightTXT, midCurve.Y - weightTXT.Height / 2);
            cv.Children.Add(weightTXT);
            MainWindow.GetLogger().Log(LogSeverity.INFO,
                $"Weight {weight} drawn at ({midCurve.X};{midCurve.Y})!");
        }
    }
}