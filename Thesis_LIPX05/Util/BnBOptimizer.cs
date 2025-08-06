namespace Thesis_LIPX05.Util
{
    public class BnBOptimizer(Dictionary<string, SGraph.Node> nodes, List<SGraph.Edge> edges) : OptimizerBase(nodes, edges)
    {
        private double bestCost = double.NegativeInfinity;
        private List<string> bestPath = [];

        public override List<SGraph.Node> Optimize()
        {
            var start = nodes.Keys
                .Where(n => !edges.Any(e => e.To.ID == n))
                .ToList();

            foreach (var st in start) ExplorePath([st], 0.0);

            return [.. bestPath.Select(id => nodes[id])];
        }

        private void ExplorePath(List<string> path, double totalCost) // recursive branching
        {
            if (path.Count == nodes.Count) // all nodes visited
            {
                if (totalCost > bestCost)
                {
                    bestCost = totalCost;
                    bestPath = [.. path];
                }
                return;
            }

            if (path.Count > nodes.Count) return; // invalid path length

            {
                string last = path.Last();
                var successors = edges
                    .Where(e => e.From.ID == last)
                    .Select(e => e.To.ID)
                    .Where(n => !path.Contains(n))
                    .ToList();

                if (successors.Count == 0)
                {
                    if (totalCost > bestCost)
                    {
                        bestCost = totalCost;
                        bestPath = [.. path];
                    }
                    return;
                }

                foreach (var succ in successors)
                {
                    var edge = edges.First(e => e.From.ID == last && e.To.ID == succ);
                    path.Add(succ);
                    ExplorePath(path, totalCost + edge.Cost);
                    path.RemoveAt(path.Count - 1); // backtrack
                }
            }
        }
    }
}
