using static Thesis_LIPX05.Util.LogManager;
using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    public class BnBOptimizer(Dictionary<string, Node> nodes, List<Edge> edges) : IOptimizer
    {
        private double bestCost = double.NegativeInfinity;
        private readonly SolverLogManager logManager = new("BnBOptimizer");

        private List<string> bestPath = [];
        private readonly Dictionary<string, double> maxRemainingCosts = [];

        public List<Node> Optimize()
        {
            PreComputeMaxRemainingCosts();
            
            List<string> start = [.. nodes.Keys.Where(n => !edges.Any(e => e.To.ID == n))];

            foreach (string st in start) ExplorePath([st], 0.0);

            logManager.LogSolverActivity(LogSeverity.INFO, $"Best path found with cost: {bestCost}");
            return [.. bestPath.Select(id => nodes[id])];
        }

        private void PreComputeMaxRemainingCosts()
        {
            foreach (string node in nodes.Keys)
                maxRemainingCosts[node] = 0.0;

            bool changed;
            do
            {
                changed = false;
                foreach (var e in edges)
                {
                    string u = e.From.ID;
                    string v = e.To.ID;
                    double cost = e.Cost;

                    double newCost = cost + maxRemainingCosts[v];
                    if (newCost > maxRemainingCosts[u])
                    {
                        maxRemainingCosts[u] = newCost;
                        changed = true;
                    }
                }
            } while (changed);

            logManager.LogSolverActivity(LogSeverity.INFO, "Precomputed maximum remaining costs for nodes.");
        }

        // Recursive method to branch and bound using backtracking
        private void ExplorePath(List<string> path, double totalCost)
        {
            logManager.LogSolverActivity(LogSeverity.INFO, $"Exploring path: {string.Join(" -> ", path)} with cost: {totalCost}");

            string last = path.Last();
            List<string> successors = [.. edges
                .Where(e => e.From.ID == last)
                .Select(e => e.To.ID)
                .Where(n => !path.Contains(n))];

            // 1.: Checking for termination (dead end)
            if (successors.Count == 0)
            {
                // If the path is complete, check if best
                if (totalCost > bestCost)
                {
                    bestCost = totalCost;
                    bestPath = [.. path];
                    logManager.LogSolverActivity(LogSeverity.INFO, $"New best path found with cost: {bestCost}");
                }
                logManager.LogSolverActivity(LogSeverity.INFO, "No successors found, backtracking.");
                return;
            }

            // 2.: Branching to each successor
            foreach (string succ in successors)
            {
                Edge edge = edges.First(e => e.From.ID == last && e.To.ID == succ);
                double cost2Succ = edge.Cost;
                double potCost = totalCost + cost2Succ + HeuristicEstimate(succ);
                if (potCost <= bestCost)
                {
                    logManager.LogSolverActivity(LogSeverity.INFO,
                        $"Pruning path at {succ} with potential cost: {potCost} <= best cost: {bestCost}");
                    continue; // pruning
                }

                // Proceed to recursion
                List<string> newPath = [succ];
                ExplorePath(path, totalCost + edge.Cost); // recursion into the next node
            }
        }

        private double HeuristicEstimate(string nodeID) => maxRemainingCosts.TryGetValue(nodeID, out double estimate) ? estimate : 0.0;
    }
}