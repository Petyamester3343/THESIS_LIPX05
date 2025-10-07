using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    public class BnBOptimizer(Dictionary<string, Node> nodes, List<Edge> edges)
        : IOptimizer
    {
        private double bestCost = double.NegativeInfinity;
        private List<string> bestPath = [];
        private Dictionary<string, double> maxRemainingCosts = [];

        private void PreComputeMaxRemainingCosts()
        {
            foreach (var node in nodes.Keys) maxRemainingCosts[node] = 0.0;

            bool changed;
            do
            {
                changed = false;
                foreach (var e in edges)
                {
                    var u = e.From.ID;
                    var v = e.To.ID;
                    var cost = e.Cost;

                    double newCost = cost + maxRemainingCosts[v];
                    if (newCost > maxRemainingCosts[u])
                    {
                        maxRemainingCosts[u] = newCost;
                        changed = true;
                    }
                }
            } while (changed);

            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, "Precomputed maximum remaining costs for nodes.");
        }

        public List<Node> Optimize()
        {
            PreComputeMaxRemainingCosts();
            
            List<string> start = [.. nodes.Keys.Where(n => !edges.Any(e => e.To.ID == n))];

            foreach (string st in start) ExplorePath([st], 0.0);

            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"Best path found with cost: {bestCost}");
            return [.. bestPath.Select(id => nodes[id])];
        }

        // Recursive method to branch and bound using backtracking
        private void ExplorePath(List<string> path, double totalCost)
        {
            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"Exploring path: {string.Join(" -> ", path)} with cost: {totalCost}");

            string last = path.Last();
            var successors = edges
                .Where(e => e.From.ID == last)
                .Select(e => e.To.ID)
                .Where(n => !path.Contains(n))
                .ToList();

            // 1.: Checking for termination (dead end)
            if (successors.Count == 0)
            {
                // If the path is complete, check if best
                if (totalCost > bestCost)
                {
                    bestCost = totalCost;
                    bestPath = [.. path];
                    MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"New best path found with cost: {bestCost}");
                }
                MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, "No successors found, backtracking.");
                return;
            }

            // 2.: Branching to each successor
            foreach (var succ in successors)
            {
                var edge = edges.First(e => e.From.ID == last && e.To.ID == succ);
                double cost2Succ = edge.Cost;
                double potCost = totalCost + cost2Succ + HeuristicEstimate(succ);
                if (potCost <= bestCost)
                {
                    MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"Pruning path at {succ} with potential cost: {potCost} <= best cost: {bestCost}");
                    continue; // pruning
                }

                // Proceed to recursion
                List<string> newPath = [succ];
                ExplorePath(path, totalCost + edge.Cost); // recursion into the next node
            }
        }

        private double HeuristicEstimate(string nodeID) => 
            maxRemainingCosts.TryGetValue(nodeID, out double estimate) ? estimate : 0.0;
    }
}