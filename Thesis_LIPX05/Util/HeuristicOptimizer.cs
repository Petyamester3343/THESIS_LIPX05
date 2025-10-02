using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    internal class HeuristicOptimizer(Dictionary<string, Node> nodes, List<Edge> edges)
        : IOptimizer
    {
        public List<Node> Optimize()
        {
            var topo = TopologicalSort(nodes, edges);
            var dist = nodes.Keys.ToDictionary(k => k, _ => double.NegativeInfinity);
            Dictionary<string, string> pred = [];

            foreach (var n in nodes.Keys)
                if (!edges.Any(e => e.To.ID == n))
                    dist[n] = 0; // start nodes with no incoming edges

            foreach (var u in topo)
            {
                foreach (var edge in edges.Where(e => e.From.ID == u))
                {
                    var v = edge.To.ID;
                    var cost = edge.Cost;

                    if (dist[v] < (dist[u] + cost))
                    {
                        dist[v] = dist[u] + cost;
                        pred[v] = u;
                        MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"Updated distance for {v} to {dist[v]} via {u}");
                    }
                }
            }

            string end = dist.Aggregate((a, b) => a.Value > b.Value ? a : b).Key;
            Stack<string> path = [];

            while (end != null)
            {
                path.Push(end);
                pred.TryGetValue(end, out end!);
                MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"Backtracking path, current node: {end}");
            }

            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"Longest path found with cost: {dist[path.Peek()]}");
            return [.. path.Select(id => nodes[id])];
        }

        // Topological sort using Kahn's algorithm
        protected static List<string> TopologicalSort(Dictionary<string, Node> nodes, List<Edge> edges)
        {
            var inDeg = nodes.Keys.ToDictionary(n => n, _ => 0);
            foreach (var edge in edges) inDeg[edge.To.ID]++;

            var queue = new Queue<string>(inDeg.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
            List<string> sorted = [];

            while (queue.Count != 0)
            {
                var node = queue.Dequeue();
                sorted.Add(node);
                MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"Node {node} added to topological sort.");

                foreach (var edge in edges.Where(e => e.From.ID == node))
                {
                    inDeg[edge.To.ID]--;
                    if (inDeg[edge.To.ID] == 0) queue.Enqueue(edge.To.ID);
                    MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"Decreased in-degree of {edge.To.ID} to {inDeg[edge.To.ID]}");
                }
            }

            if (sorted.Count != nodes.Count)
            {
                MainWindow.GetLogger().Log(LogManager.LogSeverity.ERROR, "Graph has at least one cycle, topological sort not possible.");
                throw new InvalidOperationException("Graph has at least one cycle, topological sort not possible.");
            }

            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, "Topological sort completed successfully.");
            return sorted;
        }
    }
}
