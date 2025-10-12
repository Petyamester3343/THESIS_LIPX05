using static Thesis_LIPX05.Util.LogManager;
using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    internal class HeuristicOptimizer(Dictionary<string, Node> nodes, List<Edge> edges) : IOptimizer
    {
        private readonly SolverLogManager logManager = new("HeuristicOptimizer");

        public List<Node> Optimize()
        {
            List<string> topo = TopoSort(nodes, edges);
            Dictionary<string, double> dist = nodes.Keys.ToDictionary(k => k, _ => double.NegativeInfinity);
            Dictionary<string, string> pred = [];

            Dictionary<string, int> inDeg = nodes.Keys.ToDictionary(n => n, _ => 0);
            foreach (Edge edge in edges) inDeg[edge.To.ID]++;

            foreach (KeyValuePair<string, int> kvp in inDeg.Where(kvp => kvp.Value == 0)) dist[kvp.Key] = 0.0;

            foreach (string u in topo)
            {
                foreach (Edge edge in edges.Where(e => e.From.ID == u))
                {
                    string v = edge.To.ID;
                    double cost = edge.Cost;

                    if (dist[v] < (dist[u] + cost))
                    {
                        dist[v] = dist[u] + cost;
                        pred[v] = u;
                        logManager.LogSolverActivity(LogSeverity.INFO, $"Updated distance for {v} to {dist[v]} via {u}.");
                    }
                }
            }

            string endNodeID = dist.Aggregate((a, b) => a.Value > b.Value ? a : b).Key;
            Stack<string> path = [];

            string? curr = endNodeID;
            while (curr != null)
            {
                path.Push(curr);
                if (pred.TryGetValue(curr, out string? predID))
                {
                    curr = predID;
                    logManager.LogSolverActivity(LogSeverity.INFO, $"Backtracking path, current node: {curr}.");
                }
                else curr = null;
            }

            logManager.LogSolverActivity(LogSeverity.INFO,
                $"Longest path found with cost: {endNodeID} with cost: {dist[endNodeID]}.");
            return [.. path.Select(id => nodes[id])];
        }

        // Topological sort using Kahn's algorithm
        protected List<string> TopoSort(Dictionary<string, Node> nodes, List<Edge> edges)
        {
            Dictionary<string, int> inDeg = nodes.Keys.ToDictionary(n => n, _ => 0);
            foreach (Edge edge in edges) inDeg[edge.To.ID]++;

            Queue<string> queue = new(inDeg.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
            List<string> sorted = [];

            while (queue.Count != 0)
            {
                string node = queue.Dequeue();
                sorted.Add(node);
                logManager.LogSolverActivity(LogSeverity.INFO, $"Node {node} added to topological sort.");

                foreach (Edge edge in edges.Where(e => e.From.ID == node))
                {
                    inDeg[edge.To.ID]--;
                    if (inDeg[edge.To.ID] == 0) queue.Enqueue(edge.To.ID);
                    logManager.LogSolverActivity(LogSeverity.INFO, $"Decreased in-degree of {edge.To.ID} to {inDeg[edge.To.ID]}");
                }
            }

            if (sorted.Count != nodes.Count)
            {
                logManager.LogSolverActivity(LogSeverity.ERROR,
                    "Graph has at least one cycle, topological sort not possible!");
                throw new InvalidOperationException("Graph is not a DAG, topological sort not possible.");
            }

            logManager.LogSolverActivity(LogSeverity.INFO, "Topological sort completed successfully.");
            return sorted;
        }
    }
}