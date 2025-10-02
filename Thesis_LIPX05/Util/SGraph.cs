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

        // Writes the S-graph's nodes and edges into an .xml file
        public static void WriteSGraphIntoFile()
        {
            string filePath = FilePath.Combine(GetFolderPath(SpecialFolder.Desktop), "sgraph.xml");

            XDocument doc = new(
                new XElement("SGraph",
                    new XElement("Nodes",
                        nodes.Select(n =>
                            new XElement("Node",
                                new XAttribute("ID", n.Value.ID),
                                new XAttribute("Desc", n.Value.Desc)
                            )
                        )
                    ),
                    new XElement("Edges",
                        edges.Select(e =>
                            new XElement("Edge",
                                new XAttribute("From", e.From.ID),
                                new XAttribute("To", e.To.ID),
                                new XAttribute("Cost", e.Cost)
                            )
                        )
                    )
                )
            );

            try
            {
                doc.Save(filePath);
                MainWindow.GetLogger().Log(LogSeverity.INFO, $"Document has been saved to {filePath}"!);
            }
            catch (IOException)
            {
                MainWindow.GetLogger().Log(LogSeverity.ERROR, "Save unsucessful!");
                MessageBox.Show("Save unsucessful!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
            WriteSGraphIntoFile();
        }

        // Draws a quadratic Bezier curve with a triangular polygon at its end between two nodes on the provided canvas
        private static void DrawEdge(Canvas cv, Point from, Point to, Brush color, double weight, double thickness = 2)
        {
            // offset starting point to the right edge of the source node
            Point start = new(from.X + 2 * Node.Radius, from.Y + Node.Radius);
            Point end = new(to.X, to.Y + Node.Radius);

            // how many edges are drawn from this starting point
            edgeCountFromNode[from] = !edgeCountFromNode.TryGetValue(from, out int edgeIndex) ? 1 : ++edgeIndex;

            // first is straight, rest are curved
            double curveOffset = (edgeIndex > 1) ? 20 * (edgeIndex - 1) : 0;

            // direction of the arrow
            Vector dir = end - start;
            dir.Normalize();
            Vector normal = new(-dir.Y, dir.X);

            // midpoint and curve control
            Point
                mid = new((start.X + end.X) / 2, (start.Y + end.Y) / 2),
                control = (edgeIndex > 1) ? mid + normal * curveOffset : mid;

            // section for the edge (a quadratic Bezier segment which can be curved in case of two edges overlapping)
            PathFigure figure = new() { StartPoint = start };
            QuadraticBezierSegment segment = new() { Point1 = control, Point2 = end };
            figure.Segments.Add(segment);
            PathGeometry geo = new();
            geo.Figures.Add(figure);
            ShapePath path = new() { Stroke = color, StrokeThickness = thickness, Data = geo };
            cv.Children.Add(path);
            MainWindow.GetLogger().Log(LogSeverity.INFO, $"Edge drawn from ({start.X};{start.Y}) to ({end.X};{end.Y}) with control point at ({control.X};{control.Y})!");

            // section for the arrowhead
            Vector arrowDir = start - end;
            arrowDir.Normalize();
            Vector arrowNorm = new(-arrowDir.Y, arrowDir.X);

            double headLength = 10;
            double headWidth = 5;

            Point base1 = end + arrowDir * headLength + arrowNorm * headWidth;
            Point base2 = end + arrowDir * headLength - arrowNorm * headWidth;

            PointCollection points = [end, base1, base2];

            Polygon arrowHead = new()
            {
                Points = points,
                Fill = color,
                RenderTransform = new RotateTransform(0, to.X, to.Y)
            };

            cv.Children.Add(arrowHead);
            MainWindow.GetLogger().Log(LogSeverity.INFO, $"Arrowhead drawn at {end.X};{end.Y}");

            // midpoint of the quadratic Bézier curve at t = 0.5 -> B(t) = (1 - t)^2 * P0 + 2(1 - t)t * P1 + t^2 * P2
            double t = 0.5;
            Point midCurve = new()
            {
                X = (1 - t) * (1 - t) * start.X + 2 * (1 - t) * t * control.X + t * t * end.X,
                Y = (1 - t) * (1 - t) * start.Y + 2 * (1 - t) * t * control.Y + t * t * end.Y
            };

            // the weight of the path at the calculated midpoint of the curve
            TextBlock weightBlock = new()
            {
                Text = Convert.ToInt32(weight).ToString(),
                Foreground = Brushes.Black,
                Background = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Width = 18,
                Height = 12
            };

            Canvas.SetLeft(weightBlock, midCurve.X - weightBlock.Width / 2);
            Canvas.SetTop(weightBlock, midCurve.Y - weightBlock.Height / 2);

            cv.Children.Add(weightBlock);
            MainWindow.GetLogger().Log(LogSeverity.INFO, $"Weight {weight} drawn at {midCurve.X - weightBlock.Width / 2};{midCurve.Y - weightBlock.Height / 2}");
        }
    }
}