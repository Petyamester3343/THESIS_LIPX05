using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    public class BnBOptimizer(Dictionary<string, Node> nodes, List<Edge> edges)
        : IOptimizer
    {
        private double bestCost = double.NegativeInfinity;
        private List<string> bestPath = [];

        public List<Node> Optimize()
        {
            List<string> start = [.. nodes.Keys.Where(n => !edges.Any(e => e.To.ID == n))];

            foreach (string st in start) ExplorePath([st], 0.0);

            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"Best path found with cost: {bestCost}");
            return [.. bestPath.Select(id => nodes[id])];
        }

        // Recursive method to branch and bound using backtracking
        private void ExplorePath(List<string> path, double totalCost)
        {
            if (path.Count == nodes.Count) // all nodes visited
            {
                if (totalCost > bestCost)
                {
                    bestCost = totalCost;
                    bestPath = [.. path];
                    MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"New best path found with cost: {bestCost}");
                }
                return;
            }

            // return in case of invalid path length
            if (path.Count > nodes.Count)
            {
                MainWindow.GetLogger().Log(LogManager.LogSeverity.WARNING, "Path length exceeded number of nodes, backtracking.");
                return;
            }

            // critical path exploration in a secluded scope
            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"Exploring path: {string.Join(" -> ", path)} with cost: {totalCost}");
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
                        MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"New best path found with cost: {bestCost}");
                    }
                    MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, "No successors found, backtracking.");
                    return;
                }

                foreach (var succ in successors)
                {
                    var edge = edges.First(e => e.From.ID == last && e.To.ID == succ);
                    path.Add(succ);
                    ExplorePath(path, totalCost + edge.Cost); // recursion into the next node
                    path.RemoveAt(path.Count - 1); // backtracking in case of return
                    MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"Backtracked to path: {string.Join(" -> ", path)} with cost: {totalCost}");
                }
            }
        }
    }
}
