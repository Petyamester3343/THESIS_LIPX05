using static System.Math;

namespace Y0KAI_SA
{
    internal class SimulatedAnnealing
    {
        // Custom equality comparer for tuples of strings to ensure case-insensitive comparison
        private class TupleStringEqualityComparer : IEqualityComparer<(string, string)>
        {
            // Override Equals to ensure that tuples (a, b) and (a, b) are considered equal (in a case-insensitive manner)
            public bool Equals((string, string) x, (string, string) y) =>
                StringComparer.OrdinalIgnoreCase.Equals(x.Item1, y.Item1)
                &&
                StringComparer.OrdinalIgnoreCase.Equals(x.Item2, y.Item2);

            // Override GetHashCode to ensure that (a, b) and (b, a) produce different hash codes (with using XOR)
            public int GetHashCode((string, string) obj) =>
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1 ?? string.Empty)
                ^
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2 ?? string.Empty);
        }

        private readonly Graph graph;
        private readonly Random rnd = new();
        private const double DependencyPenalty = 100000;

        private readonly Dictionary<(string, string), double> edgeCostLookup;
        private readonly Dictionary<(string, string), bool> reverseDependencyCheck;

        // Constructor to initialize the SimulatedAnnealing instance with a graph
        public SimulatedAnnealing(Graph g)
        {
            graph = g;
            edgeCostLookup = graph.Edges.ToDictionary(e => (e.FromID, e.ToID), e => e.Cost);
            reverseDependencyCheck = graph.Edges.ToDictionary(
                e => (e.ToID, e.FromID),
                e => true,
                new TupleStringEqualityComparer()
            );
        }

        // Main method to perform the simulated annealing algorithm
        public List<string> Solve(double initTemp, double coolRate, int maxIt, bool isSilent)
        {
            List<string>
                currPath = [.. graph.Nodes.Keys.OrderBy(_ => rnd.Next())],
                bestPath = [.. currPath];

            double
                currCost = EvalPath(currPath, isSilent),
                bestCost = currCost;

            for (int i = 0; i < maxIt && initTemp > 0.1; i++)
            {
                var neighborPath = GenNeighbor(currPath);
                double
                    neighborCost = EvalPath(neighborPath, isSilent),
                    d = neighborCost - currCost;

                if (d > 0 || rnd.NextDouble() < Exp(d / initTemp))
                {
                    currPath = neighborPath;
                    currCost = neighborCost;
                }

                if (currCost > bestCost)
                {
                    bestPath = [.. currPath];
                    bestCost = currCost;
                }

                initTemp *= coolRate;
            }
            return FilterPathToDAG(bestPath);
        }

        // Evaluate the total cost of a given path, applying penalties for any reverse dependencies
        private double EvalPath(List<string> path, bool isSilent)
        {
            double
                total = 0,
                penalty = 0;

            for (int i = 0; i < path.Count - 1; i++)
            {
                string
                    from = path[i],
                    to = path[i + 1];

                total += edgeCostLookup.TryGetValue((from, to), out double cost) ? cost : 0;
                penalty += reverseDependencyCheck.ContainsKey((from, to)) ? DependencyPenalty : 0;
                if (!isSilent)
                    Console.WriteLine($"{i + 1}. iteration\ncost: {cost}\npenalty: {penalty}\ntotal: {total}\ncurrent value: {total - penalty}\n");
            }

            return total - penalty;
        }

        // Generate a neighboring solution by swapping two random nodes in the path
        private List<string> GenNeighbor(List<string> path)
        {
            List<string> neighbor = [.. path];

            int i, j;
            do
            {
                i = rnd.Next(neighbor.Count);
                j = rnd.Next(neighbor.Count);
            } while (i == j);

            (neighbor[i], neighbor[j]) = (neighbor[j], neighbor[i]);

            return neighbor;
        }

        // Filter the best path to ensure it forms a valid Directed Acyclic Graph (DAG) by removing nodes that violate dependencies
        private List<string> FilterPathToDAG(List<string> best)
        {
            List<string> valid = [];
            HashSet<string> visited = [];

            var dependencies = graph.Nodes.Keys.ToDictionary(id => id, id => new List<string>());
            foreach (var e in graph.Edges) dependencies[e.ToID].Add(e.FromID);

            foreach (string next in best)
            {
                bool dependenciesMet = dependencies[next].All(visited.Contains);

                if (dependenciesMet)
                {
                    valid.Add(next);
                    visited.Add(next);
                }
            }

            return valid;
        }
    }
}