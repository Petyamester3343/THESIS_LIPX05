namespace Y0KAI_SA
{
    internal class SimulatedAnnealing(Graph g)
    {
        // Custom equality comparer for tuples of strings to ensure case-insensitive comparison
        private class TupleStringEqualityComparer(StringComparer oic) : IEqualityComparer<(string, string)>
        {
            // Override Equals to ensure that tuples (a, b) and (a, b) are considered equal (in a case-insensitive manner)
            public bool Equals((string, string) x, (string, string) y) => oic.Equals(x.Item1, y.Item1) && oic.Equals(x.Item2, y.Item2);

            // Override GetHashCode to ensure that (a, b) and (b, a) produce different hash codes (with using XOR)
            public int GetHashCode((string, string) obj) => oic.GetHashCode(obj.Item1 ?? string.Empty) ^ oic.GetHashCode(obj.Item2 ?? string.Empty);
        }

        private readonly Graph g = g;
        private readonly Random rnd = new();

        // Main method to perform the simulated annealing algorithm
        public List<string> Solve(double initTemp, double coolRate, int maxIt, bool isSilent)
        {
            List<string>
                baseJobIDs = [.. g.Nodes.Keys.Where(id => id.EndsWith("_M1")).Select(id => id[..^3]).Distinct()],
                currPath = [.. baseJobIDs.OrderBy(id => id)],
                bestPath = [.. currPath];

            double
                currCost = EvaluateMakespan(currPath, isSilent),
                bestCost = currCost;

            if (bestCost == double.MaxValue && !isSilent)
                Console.Error.WriteLine("Initial random sequence caused MaxValue makespan; search span may be challenging.");

            for (int i = 0; i < maxIt && initTemp > 0.1; i++)
            {
                List<string> neighborPath = GenNeighbor(currPath);
                double
                    neighborCost = EvaluateMakespan(neighborPath, isSilent),
                    dVal = neighborCost - currCost;

                if (dVal < 0 || rnd.NextDouble() < Math.Exp(-dVal / initTemp))
                {
                    currPath = neighborPath;
                    currCost = neighborCost;
                }

                if (currCost < bestCost)
                {
                    bestPath = [.. currPath];
                    bestCost = currCost;
                    if (!isSilent) Console.WriteLine($"New best makespan found: {bestCost:F2}");
                }

                initTemp *= coolRate;
            }

            if (!isSilent) Console.WriteLine($"Optimization complete. Best makespan: {bestCost:F2}");
            return [.. bestPath.Where(id => id is not "Prod" or "Prod_M1" or "Prod_M2")];
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
            } while (i.Equals(j));

            (neighbor[i], neighbor[j]) = (neighbor[j], neighbor[i]);

            return neighbor;
        }

        // Generates the makespan's graph
        private double EvaluateMakespan(List<string> jobSeq, bool isSilent)
        {
            const double ZeroDelay = 0.0;

            g.Edges.Clear();

            foreach (KeyValuePair<string, Node> kvp in g.Nodes.Where(n => n.Value.TimeM1 > 0 && n.Key.EndsWith("_M1")))
            {
                string
                    fromID = kvp.Key,
                    baseID = fromID[..^3];
                Node node = kvp.Value;

                g.Edges.Add(new()
                {
                    FromID = fromID,
                    ToID = $"{baseID}_M2",
                    Cost = node.TimeM1
                });
            }

            for (int i = 0; i < jobSeq.Count - 1; i++)
            {
                string
                    jobA = jobSeq[i],
                    jobB = jobSeq[i + 1];

                g.Edges.Add(new()
                {
                    FromID = $"{jobA}_M1",
                    ToID = $"{jobB}_M1",
                    Cost = ZeroDelay
                });

                g.Edges.Add(new()
                {
                    FromID = $"{jobA}_M2",
                    ToID = $"{jobB}_M2",
                    Cost = ZeroDelay
                });
            }

            if (jobSeq.Count > 0)
            {
                string last = jobSeq.Last();

                g.Edges.Add(new()
                {
                    FromID = $"{last}_M1",
                    ToID = $"Prod_M1",
                    Cost = ZeroDelay
                });

                g.Edges.Add(new()
                {
                    FromID = $"{last}_M2",
                    ToID = $"Prod_M2",
                    Cost = ZeroDelay
                });
            }

            Dictionary<string, double> eftTimes = GetLongestPathEFTs(isSilent);
            double makespan = eftTimes.Where(kvp => kvp.Key.EndsWith("_M2")).Max(kvp => kvp.Value);

            return (makespan <= 0) ? double.MaxValue : makespan;
        }

        // Fetches the path's EFT (earliest finish time)
        private Dictionary<string, double> GetLongestPathEFTs(bool isSilent)
        {
            List<string> topo = TopoSort(g.Nodes, g.Edges, isSilent);
            if (topo.Count is 0)
                return g.Nodes.Keys.ToDictionary(k => k, v => double.MaxValue);
            
            // sentinel value for unreached nodes
            const double MinDist = -1.0;

            Dictionary<string, double> dist = g.Nodes.Keys.ToDictionary(k => k, v => MinDist);
            Dictionary<string, int> inDeg = g.Nodes.Keys.ToDictionary(k => k, v => 0);

            foreach (Edge e in g.Edges) inDeg[e.ToID]++;

            // initialize starting nodes (in-deg 0) to EST = 0.0
            foreach (KeyValuePair<string, int> kvp in inDeg.Where(kvp => kvp.Value is 0))
                dist[kvp.Key] = 0.0;

            foreach (string u in topo)
            {
                if (dist[u] <= MinDist) continue;
                
                Node nodeU = g.Nodes[u];
                double durU = (nodeU.TimeM1 > 0) ? nodeU.TimeM1 : nodeU.TimeM2;

                double eftU = dist[u] + durU; // EFT(u)

                foreach (Edge e in g.Edges.Where(e => e.FromID == u))
                {
                    string v = e.ToID;
                    double cost = e.Cost;

                    double reqStartV = eftU + cost; // EST(u) = EFT(u) + Cost

                    if (dist[v] < reqStartV)
                        dist[v] = reqStartV;
                }
            }

            foreach (KeyValuePair<string, Node> kvp in g.Nodes)
                if (dist.ContainsKey(kvp.Key))
                {
                    if (dist[kvp.Key] <= MinDist)
                    {
                        dist[kvp.Key] = double.MaxValue;
                        continue;
                    }

                    double finalDur = (kvp.Value.TimeM1 > 0) ? kvp.Value.TimeM1 : kvp.Value.TimeM2;
                    dist[kvp.Key] += finalDur;
                }
                
            return dist;
        }

        // Topological sort inspired by Kahn's algorithm
        private static List<string> TopoSort(Dictionary<string, Node> nodes, List<Edge> edges, bool isSilent)
        {
            Dictionary<string, int> inDegree = nodes.Keys.ToDictionary(k => k, v => 0);
            foreach (Edge e in edges) inDegree[e.ToID]++;

            Queue<string> zeroInDegree = new(inDegree.Where(kvp => kvp.Value is 0).Select(kvp => kvp.Key));
            List<string> topoOrder = [];

            while (zeroInDegree.Count is not 0)
            {
                string n = zeroInDegree.Dequeue();
                topoOrder.Add(n);
                if (!isSilent) Console.WriteLine($"Node {n} added to topological order.");

                foreach (Edge e in edges.Where(e => e.FromID == n))
                {
                    string m = e.ToID;
                    inDegree[m]--;
                    if (inDegree[m] is 0) zeroInDegree.Enqueue(m);
                    if (!isSilent) Console.WriteLine($"Decreased in-degree of {m} to {inDegree[m]}.");
                }
            }
            if (topoOrder.Count != nodes.Count)
            {
                if (!isSilent) Console.Error.WriteLine("Graph has at least one cycle; topological sort not possible.");
                return [];
            }

            if (!isSilent) Console.WriteLine("Topological sort completed successfully.");
            return topoOrder;
        }
    }
}