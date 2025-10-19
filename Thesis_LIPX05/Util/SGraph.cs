using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;

using static Thesis_LIPX05.Util.LogManager;

// Aliases for clarity
using FilePath = System.IO.Path;
using WindowPoint = System.Windows.Point;
using ShapePath = System.Windows.Shapes.Path;

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
            public WindowPoint Position { get; set; } // position of the node on the canvas
            public static double Radius { get; set; } = 35; // radius of the node, default is 35 (for visual representation)
            public required double TimeM1 { get; set; } // time in minutes for machine 1
            public required double TimeM2 { get; set; } // time in minutes for machine 2
        }

        // The nested class representing a unidirectional (directed) edge between two nodes
        public class Edge // a unidirectional edge between two or more nodes
        {
            public required Node From { get; set; }
            public required Node To { get; set; }
            public required double Cost { get; set; }
        }

        // Private read-only static collections to store necessary data
        private readonly static List<Edge> edges = [];
        private readonly static Dictionary<string, Node> nodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly static Dictionary<string, int> edgeCountFromNode = [];

        // Getters for nodes and edges
        public static Dictionary<string, Node> GetNodes() => nodes;
        public static List<Edge> GetEdges() => edges;

        // Writes the graph to a custom XML file
        // The XML structure is as follows:
        /*
         <SGraph>
           <Nodes>
             <Node ID="Node1" Desc="Description1" />
             <Node ID="Node2" Desc="Description2" />
             ...
           </Nodes>
           <Edges>
             <Edge From="Node1" To="Node2" Cost="10" />
             <Edge From="Node2" To="Node3" Cost="20" />
             ...
           </Edges>
         </SGraph>
         */
        public static string WriteSGraph2XML(string path)
        {
            bool succ;

            XDocument graph = new();
            XElement root = new("SGraph");
            graph.Add(root);

            XElement nodes = new("Nodes");
            root.Add(nodes);
            foreach (Node node in GetNodes().Values)
            {
                XElement nodeElement = new("Node",
                    new XAttribute("ID", node.ID),
                    new XAttribute("Desc", node.Desc),
                    new XAttribute("TimeM1", node.TimeM1),
                    new XAttribute("TimeM2", node.TimeM2));
                nodes.Add(nodeElement);
            }

            XElement edges = new("Edges");
            root.Add(edges);
            foreach (Edge edge in GetEdges())
            {
                XElement edgeElement = new("Edge",
                    new XAttribute("From", edge.From.ID),
                    new XAttribute("To", edge.To.ID),
                    new XAttribute("Cost", edge.Cost));
                edges.Add(edgeElement);
            }

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            string fullPath = FilePath.Combine(path, $"SGraph_{Guid.NewGuid()}.xml");

            try
            {
                graph.Save(fullPath);
                LogGeneralActivity(LogSeverity.INFO,
                    $"SGraph saved to {fullPath}!", GeneralLogContext.S_GRAPH);
                succ = true;
            }
            catch (Exception ex)
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    $"Failed to save S-Graph to XML: {ex.Message}", GeneralLogContext.S_GRAPH);
                MessageBox.Show($"Failed to save S-Graph to XML: {ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                succ = false;
            }

            return succ ? fullPath : string.Empty;
        }

        // Adds a node to the graph
        public static void AddNode(string id, string desc, WindowPoint position, double t1, double t2)
        {
            if (!nodes.ContainsKey(id))
                nodes.Add(id, new()
                {
                    ID = id,
                    Desc = desc,
                    Position = position,
                    TimeM1 = t1,
                    TimeM2 = t2
                });
        }

        // Adds an edge to the graph
        public static void AddEdge(string fromID, string toID, double cost)
        {
            if (nodes.TryGetValue(fromID, out Node? from)
                && nodes.TryGetValue(toID, out Node? to)
                && from is not null
                && to is not null
                && !double.IsNaN(cost)) edges.Add(new()
                {
                    From = from,
                    To = to,
                    Cost = cost
                });
        }

        // Renders the graph on the given canvas
        public static void Render(Canvas cv, int rowLimit)
        {
            cv.Children.Clear();
            edgeCountFromNode.Clear();

            List<Node> sorted = [.. nodes.Values];

            foreach (Node n in sorted)
            {
                DrawNode(n, cv);
                DrawNodeLabel(n, cv);
            }

            foreach (Edge e in edges)
            {
                DrawArrow(cv, e.From.ID, e.From.Position, e.To.Position, Brushes.DarkBlue, e.Cost);
                LogGeneralActivity(LogSeverity.INFO,
                    $"Edge from {e.From.ID} to {e.To.ID} with cost {e.Cost} drawn.", GeneralLogContext.S_GRAPH);
            }

            if (nodes.Count > 0)
            {
                cv.Width = nodes.Values.Max(n => n.Position.X) + (Node.Radius * 2) + 50;
                cv.Height = nodes.Values.Max(n => n.Position.Y) + (Node.Radius * 2) + 50;
                LogGeneralActivity(LogSeverity.INFO,
                    $"Canvas resized to {cv.Width}x{cv.Height}!", GeneralLogContext.S_GRAPH);
            }
            else
            {
                cv.Width = 300;
                cv.Height = 200;
                LogGeneralActivity(LogSeverity.INFO,
                    $"Canvas resized to default {cv.Width}x{cv.Height}!", GeneralLogContext.S_GRAPH);
            }
        }

        // Draws a node as a circle with a tooltip on the provided canvas
        private static void DrawNode(Node node, Canvas cv)
        {
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
                        Width = 100,
                        Foreground = Brushes.Black,
                        TextWrapping = TextWrapping.Wrap,
                    }
                }
            };

            Canvas.SetLeft(graphNode, node.Position.X);
            Canvas.SetTop(graphNode, node.Position.Y);
            cv.Children.Add(graphNode);
            LogGeneralActivity(LogSeverity.INFO,
                $"Node {node.ID} at {node.Position.X};{node.Position.Y}", GeneralLogContext.S_GRAPH);
        }

        // Draws the label of a node on the provided canvas
        private static void DrawNodeLabel(Node node, Canvas cv)
        {
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

            Canvas.SetLeft(txt, node.Position.X);
            Canvas.SetTop(txt, node.Position.Y + Node.Radius * 2 + 5);
            cv.Children.Add(txt);
        }

        // Draws a quadratic Bezier curve with a triangular polygon at its end between two nodes on the provided canvas
        private static void DrawArrow(Canvas cv, string fromID, WindowPoint from, WindowPoint to, Brush color, double weight, double thickness = 2)
        {
            // Calculating node centers
            WindowPoint centerFrom = new(from.X + Node.Radius, from.Y + Node.Radius);
            WindowPoint centerTo = new(to.X + Node.Radius, to.Y + Node.Radius);

            // Calculating connection points
            Vector delta = centerTo - centerFrom;
            double dist = delta.Length;

            // If nodes are too close, skip drawing edges
            if (dist <= Node.Radius * 2 + 1)
            {
                LogGeneralActivity(LogSeverity.WARNING,
                    "Nodes are too close or overlapping; skipping edge line!", GeneralLogContext.S_GRAPH);
                return;
            }

            // Normalized direction from A's center to B's center
            Vector dirTo = delta / dist;

            // Calculating connection points on the circumference
            WindowPoint
                start = centerFrom + dirTo * Node.Radius,
                end = centerTo - dirTo * Node.Radius; // subtract to move backward from centerTo

            // Handling multiple edges
            edgeCountFromNode[fromID] = edgeCountFromNode.TryGetValue(fromID, out int edgeCount) ? ++edgeCount : 1;

            // If there are multiple edges, curve the nth (where n > 1), otherwise do nothing
            double dCurve = (edgeCount > 1) ? 20 * (edgeCount - 1) : 0;

            // Calculating the Bezier curve's control point
            Vector normal = new(-dirTo.Y, dirTo.X); // 90 degree rotation
            WindowPoint
                mid = new((start.X + end.X) / 2, (start.Y + end.Y) / 2),
                control = mid + normal * dCurve;

            // Drawing the directed edge
            DrawCurvedEdge(start, control, end, color, thickness, cv);

            // Drawing the arrowhead
            // It relies on the geometrically correct 'end' point
            DrawArrowHead(control, end, color, cv);

            // Drawing the weight label
            // Midpoint of a quadratic Bezier curve at t = 0.5 is (0,25 * P_0 + 0,5 * P_1 + 0,25 * P_2)
            DrawWeightOnEdge(start, control, end, weight, cv);
        }

        // Helper method for drawing a Bezier curve
        private static void DrawCurvedEdge(WindowPoint start, WindowPoint control, WindowPoint end, Brush color, double thickness, Canvas cv)
        {
            PathFigure figure = new() { StartPoint = start };
            QuadraticBezierSegment segment = new() { Point1 = control, Point2 = end };
            figure.Segments.Add(segment);

            PathGeometry geo = new();
            geo.Figures.Add(figure);
            ShapePath shapePath = new() { Stroke = color, StrokeThickness = thickness, Data = geo };
            cv.Children.Add(shapePath);
            LogGeneralActivity(LogSeverity.INFO,
                $"Edge drawn from ({start.X:F1};{start.Y:F1}) to ({end.X:F1};{end.Y:F1})!", GeneralLogContext.S_GRAPH);
        }

        // Helper method for drawing an arrowhead at the end of a Bezier curve
        private static void DrawArrowHead(WindowPoint control, WindowPoint end, Brush color, Canvas cv)
        {
            Vector arrowDir = control - end;
            arrowDir.Normalize();
            Vector arrowNorm = new(-arrowDir.Y, arrowDir.X);

            double
                headL = 10,
                headW = 5;

            WindowPoint
                b1 = end + arrowDir * headL + arrowNorm * headW,
                b2 = end + arrowDir * headL - arrowNorm * headW;

            Polygon arrowHead = new()
            {
                Points = [end, b1, b2],
                Fill = color
            };

            cv.Children.Add(arrowHead);
        }

        // Helper method for drawing the weight label on the midpoint of the edge
        private static void DrawWeightOnEdge(WindowPoint start, WindowPoint control, WindowPoint end, double weight, Canvas cv)
        {
            double t = 0.5;
            WindowPoint midCurve = new()
            {
                X = Math.Pow(1 - t, 2) * start.X + 2 * (1 - t) * t * control.X + Math.Pow(t, 2) * end.X,
                Y = Math.Pow(1 - t, 2) * start.Y + 2 * (1 - t) * t * control.Y + Math.Pow(t, 2) * end.Y
            };

            Vector
                line = end - start,
                normal = new(-line.Y, line.X);
            normal.Normalize();

            double distOff = 10;
            WindowPoint lblPos = midCurve + normal * distOff;

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

            Canvas.SetLeft(weightTXT, lblPos.X - weightTXT.Width / 2);
            Canvas.SetTop(weightTXT, lblPos.Y - weightTXT.Height / 2);

            cv.Children.Add(weightTXT);
            LogGeneralActivity(LogSeverity.INFO,
                $"Weight {weight} drawn at ({midCurve.X};{midCurve.Y})!", GeneralLogContext.S_GRAPH);
        }
    }
}