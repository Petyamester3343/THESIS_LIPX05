using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
    public class BnBOptimizer(Dictionary<string, Node> nodes, List<Edge> edges) : OptimizerBase(nodes, edges)
#pragma warning restore CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
    {
        private double bestCost = double.NegativeInfinity;
        private List<string> bestPath = [];

        public override List<Node> Optimize()
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

            if (path.Count > nodes.Count) return; // return in case of invalid path length

            // critical path exploration in a secluded scope
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
                    ExplorePath(path, totalCost + edge.Cost); // recursion into the next node
                    path.RemoveAt(path.Count - 1); // backtracking in case of return
                }
            }
        }
    }
}
