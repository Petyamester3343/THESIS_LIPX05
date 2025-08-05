using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Thesis_LIPX05.Util
{
    public class SGraph
    {
        // TODO: define, build, and render to a canvas an S-graph
        // with task nodes and edges as unidirectional transitions from one node to another
        public class Node
        {
            public required string ID { get; set; } // unique ID of the node
            public required string Desc { get; set; } // description of the node
            public Point Position { get; set; } // position of the node on the canvas
        }

        public class Edge // controlled edge between two or more nodes
        {
            public required Node From { get; set; }
            public required Node To { get; set; }
            public required double Cost { get; set; }
        }

        private readonly static Dictionary<string, Node> nodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly static Dictionary<Point, int> edgeCountFromNode = [];
        private readonly static List<Edge> edges = [];

        public static Dictionary<string, Node> GetNodes() => nodes;
        public static List<Edge> GetEdges() => edges;

        public static void AddNode(string id, string desc, Point position)
        {
            if (!nodes.ContainsKey(id))
                nodes.Add(id, new() { ID = id, Desc = desc, Position = position });
        }

        public static void AddEdge(string fromID, string toID, double cost)
        {
            if (nodes.TryGetValue(fromID, out var from) && nodes.TryGetValue(toID, out var to) &&
                from != null && to != null && !double.IsNaN(cost))
            {
                edges.Add(new() { From = from, To = to, Cost = cost });
            }
        }

        public static void Render(Canvas cv, int RowLimit)
        {
            cv.Children.Clear();
            edgeCountFromNode.Clear();

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

                // tooltip for the node
                var graphToolTip = new ToolTip
                {
                    Width = 100,
                    Height = 100,
                    Content = new TextBlock
                    {
                        Text = $"ID: {node.Desc}",
                        FontSize = 12,
                        Foreground = Brushes.Black
                    }
                };
                
                // node
                var graphNode = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = Brushes.LightBlue,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    ToolTip = graphToolTip
                };

                // label
                var txt = new TextBlock
                {
                    Text = node.ID,
                    Foreground = Brushes.Black,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Width = radius * 2,
                    Height = radius / 2,
                };

                Canvas.SetLeft(graphNode, node.Position.X);
                Canvas.SetTop(graphNode, node.Position.Y);
                cv.Children.Add(graphNode);
                
                Canvas.SetLeft(txt, node.Position.X - (radius / 2) + 18);
                Canvas.SetTop(txt, node.Position.Y + (radius / 1.333));
                cv.Children.Add(txt);
            }

            // edges
            foreach (var edge in edges)
                DrawEdge(cv, edge.From.Position, edge.To.Position, Brushes.DarkBlue, edge.Cost);

            double
                maxHorSize = nodes.Values.Max(nHor => nHor.Position.X),
                maxVertSize = nodes.Values.Max(nVert => nVert.Position.Y);

            cv.Width = maxHorSize + 100;
            cv.Height = maxVertSize + 100;
        }

        private static void DrawEdge(Canvas cv, Point from, Point to, Brush color, double weight, double thickness = 2)
        {
            double r = 35;

            // offset starting point to the right edge of the source node
            Point start = new(from.X + 2 * r, from.Y + r);
            Point end = new(to.X, to.Y + r);

            // how many edges are drawn from this starting point
            if (!edgeCountFromNode.TryGetValue(from, out int edgeIndex)) edgeCountFromNode[from] = 1;
            else edgeCountFromNode[from] = ++edgeIndex;

            // first is straight, rest are curved
            double curveOffset = (edgeIndex > 1) ? 20 * (edgeIndex - 1) : 0;

            // direction of the arrow
            Vector dir = end - start;
            dir.Normalize();
            Vector normal = new(-dir.Y, dir.X);

            // midpoint and curve control
            Point mid = new((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            Point control = (edgeIndex > 1) ? mid + normal * curveOffset : mid;

            // path is quadratic Bezier
            var figure = new PathFigure { StartPoint = start };
            var segment = new QuadraticBezierSegment { Point1 = control, Point2 = end };
            figure.Segments.Add(segment);

            var geo = new PathGeometry();
            geo.Figures.Add(figure);

            var path = new Path
            {
                Stroke = color,
                StrokeThickness = thickness,
                Data = geo
            };

            cv.Children.Add(path);

            // arrowhead
            Vector arrowDir = start - end;
            arrowDir.Normalize();
            Vector arrowNorm = new(-arrowDir.Y, arrowDir.X);

            double headLength = 10;
            double headWidth = 5;

            Point base1 = end + arrowDir * headLength + arrowNorm * headWidth;
            Point base2 = end + arrowDir * headLength - arrowNorm * headWidth;

            var arrowHead = new Polygon
            {
                Points = [end, base1, base2],
                Fill = color,
                RenderTransform = new RotateTransform(0, to.X, to.Y)
            };

            cv.Children.Add(arrowHead);

            // midpoint of the quadratic Bézier curve at t = 0.5            
            // B(t) = (1 - t)^2 * P0 + 2(1 - t)t * P1 + t^2 * P2
            double t = 0.5;
            Point midCurve = new(
                (1-t) * (1-t) * start.X + 2 * (1-t) * t * control.X + t * t * end.X,
                (1-t) * (1-t) * start.Y + 2 * (1-t) * t * control.Y + t * t * end.Y
                );
            
            // the weight of the path at the midpoint of the curve
            var weightBlock = new TextBlock
            {
                Text = Convert.ToInt32(weight).ToString(),
                Foreground = Brushes.Black,
                Background = Brushes.White,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Width = 18,
                Height = 12
            };

            Canvas.SetLeft(weightBlock, midCurve.X - weightBlock.Width / 2);
            Canvas.SetTop(weightBlock, midCurve.Y - weightBlock.Height / 2);

            cv.Children.Add(weightBlock);
        }
    }
}