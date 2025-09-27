using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    internal class GeneticOptimizer(Dictionary<string, Node> nodes, List<Edge> edges, int populationSize = 50, int generations = 100)
        : IOptimizer

    {
        private readonly Random rnd = new();

        public List<Node> Optimize()
        {
            // initialize population with random paths
            var population = Enumerable.Range(0, populationSize)
                .Select(_ => nodes.Values.OrderBy(_ => rnd.Next()).ToList())
                .ToList();

            var best = population[0];
            double bestFitness = Evaluate(best);

            // evolve population over generations
            for (int i = 0; i < generations; i++)
            {
                var scored = population
                    .Select(p => new { Path = p, Score = Evaluate(p) })
                    .OrderBy(x => x.Score)
                    .ToList();

                if (scored[0].Score < bestFitness)
                {
                    bestFitness = scored[0].Score;
                    best = scored[0].Path;
                }

                // selecting top 20%
                var parents = scored.Take(populationSize / 5).Select(x => x.Path).ToList();

                // mutation and crossover to create new generation
                var newPopulation = new List<List<Node>>(parents);

                while (newPopulation.Count < populationSize)
                {
                    var p1 = parents[rnd.Next(parents.Count)];
                    var p2 = parents[rnd.Next(parents.Count)];

                    var child = Crossover(p1, p2);
                    if (rnd.NextDouble() < 0.2) Mutate(child);

                    newPopulation.Add(child);
                }

                population = newPopulation;
            }

            return best;
        }

        // Evaluates the fitness of a path by summing the costs of its edges
        private double Evaluate(List<Node> path)
        {
            double t = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                var edge = edges.FirstOrDefault(e => e.From == path[i] && e.To == path[i + 1]); // needs some re-check
                t += (edge is not null) ? edge.Cost : 1000; // large penalty for missing edges 
            }
            return t;
        }

        // Performs crossover between two parent paths to create a child path
        private List<Node> Crossover(List<Node> p1, List<Node> p2)
        {
            int cut = rnd.Next(1, p1.Count - 1);
            var child = new List<Node>(p1.Take(cut));
            foreach (var node in p2) if (!child.Contains(node)) child.Add(node);
            return child;
        }

        // Mutates a path by swapping two nodes
        private void Mutate(List<Node> path)
        {
            int
                i = rnd.Next(path.Count),
                j = rnd.Next(path.Count);
            (path[i], path[j]) = (path[j], path[i]); // swap the two nodes within tuples
        }
    }
}
