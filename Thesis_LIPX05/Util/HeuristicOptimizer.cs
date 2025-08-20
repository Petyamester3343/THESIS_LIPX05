using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
    internal class HeuristicOptimizer(
        Dictionary<string, Node> nodes,
        List<Edge> edges) : OptimizerBase(nodes, edges)
#pragma warning restore CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
    {
        public override List<Node> Optimize()
        {
            var topo = TopologicalSort(nodes, edges);
            var dist = nodes.Keys.ToDictionary(k => k, _ => double.NegativeInfinity);
            var pred = new Dictionary<string, string>();

            foreach (var n in nodes.Keys) if (!edges.Any(e => e.To.ID == n)) dist[n] = 0; // start nodes with no incoming edges

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
                    }
                }
            }

            var end = dist.Aggregate((a, b) => a.Value > b.Value ? a : b).Key;
            Stack<string> path = [];

            while (end != null)
            {
                path.Push(end);
                pred.TryGetValue(end, out end);
            }

            return path.Select(id => nodes[id]).ToList();
        }

        protected static List<string> TopologicalSort(Dictionary<string, Node> nodes, List<Edge> edges)
        {
            var inDeg = nodes.Keys.ToDictionary(n => n, _ => 0);
            foreach (var edge in edges) inDeg[edge.To.ID]++;

            var queue = new Queue<string>(inDeg.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
            var sorted = new List<string>();

            while (queue.Count != 0)
            {
                var node = queue.Dequeue();
                sorted.Add(node);

                foreach (var edge in edges.Where(e => e.From.ID == node))
                {
                    inDeg[edge.To.ID]--;
                    if (inDeg[edge.To.ID] == 0) queue.Enqueue(edge.To.ID);
                }
            }

            return sorted;
        }
    }
}
