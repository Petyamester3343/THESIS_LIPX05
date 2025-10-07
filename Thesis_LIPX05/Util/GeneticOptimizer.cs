using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    internal class GeneticOptimizer(Dictionary<string, Node> nodes, List<Edge> edges, int populationSize = 50, int generations = 100)
        : IOptimizer

    {
        private readonly Random rnd = new();
        private readonly Dictionary<(Node, Node), double> edgeCosts =
            edges.ToDictionary(e => (e.From, e.To), e => e.Cost);
        private readonly Dictionary<(Node, Node), bool> dependencyCheck =
            edges.ToDictionary(e => (e.From, e.To), e => true);

        private const double DependencyPenalty = 100000;

        public List<Node> Optimize()
        {
            // initialize population with random paths
            var population = Enumerable.Range(0, populationSize)
                .Select(_ => nodes.Values.OrderBy(_ => rnd.Next()).ToList())
                .ToList();

            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                $"Initial population of {populationSize} paths created.");

            var best = population[0];
            double bestFitness = double.MinValue;

            // evolve population over generations
            for (int i = 0; i < generations; i++)
            {
                var scored = population
                    .Select(p => new { Path = p, Score = Evaluate(p) })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                    $"Generation {i + 1}/{generations} evaluated.");

                if (scored[0].Score > bestFitness)
                {
                    bestFitness = scored[0].Score;
                    best = scored[0].Path;
                    MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                        $"New best path found with cost: {bestFitness}");
                }

                // selecting top 20%
                List<List<Node>> parents = [.. scored.Take(populationSize / 5).Select(x => x.Path)];
                MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                    $"Generation {i + 1}/{generations} best path cost: {scored[0].Score}");

                // mutation and crossover to create new generation
                List<List<Node>> newPopulation = [.. parents];

                while (newPopulation.Count < populationSize)
                {
                    var p1 = parents[rnd.Next(parents.Count)];
                    var p2 = parents[rnd.Next(parents.Count)];

                    var child = Crossover(p1, p2);
                    if (rnd.NextDouble() < 0.2) Mutate(child);

                    newPopulation.Add(child);
                }

                population = newPopulation;
                MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                    $"Generation {i + 1}/{generations} evolved.");
            }

            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                $"Optimization completed. Best path cost: {bestFitness}");
            return best;
        }

        // Evaluates the fitness of a path by summing the costs of its edges
        private double Evaluate(List<Node> path)
        {
            double
                total = 0,
                penalty = 0;

            for (int i = 0; i < path.Count - 1; i++)
            {
                var from = path[i];
                var to = path[i + 1];

                total += edgeCosts.TryGetValue((from, to), out double cost) ? cost : 0;
                penalty += dependencyCheck.ContainsKey((to, from)) ? DependencyPenalty : 0;
            }

            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                $"Path evaluated with total cost: {total}, penalty: {penalty}");
            return total - penalty;
        }

        // Performs crossover between two parent paths to create a child path
        private List<Node> Crossover(List<Node> p1, List<Node> p2)
        {
            int cut = rnd.Next(1, p1.Count - 1);
            List<Node> child = [.. p1.Take(cut)];
            foreach (Node node in p2) if (!child.Contains(node)) child.Add(node);

            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                $"Crossover performed at cut index {cut}.");
            return child;
        }

        // Mutates a path by swapping two nodes
        private void Mutate(List<Node> path)
        {
            int
                i = rnd.Next(path.Count),
                j = rnd.Next(path.Count);

            (path[i], path[j]) = (path[j], path[i]); // swap the two nodes within tuples

            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                $"Mutation performed by swapping indices {i} and {j}.");
        }
    }
}
