using static System.Console;

using NodeKVP = System.Collections.Generic.KeyValuePair<string, Y0KAI_SA.Node>;

namespace Y0KAI_SA
{
    internal class SimulatedAnnealing(Graph g)
    {
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
                Error.WriteLine("Initial random sequence caused MaxValue makespan; search span may be challenging.");

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
                    if (!isSilent) WriteLine($"New best makespan found: {bestCost:F2}");
                }

                initTemp *= coolRate;
            }

            if (!isSilent) WriteLine($"Optimization complete. Best makespan: {bestCost:F2}");
            return [.. bestPath];
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
            }
            while (i.Equals(j));

            (neighbor[i], neighbor[j]) = (neighbor[j], neighbor[i]);

            return neighbor;
        }

        // Generates the makespan's graph
        private double EvaluateMakespan(List<string> jobSeq, bool isSilent)
        {
            g.Edges.Clear();

            // technological edges
            foreach (NodeKVP kvp in g.Nodes.Where(n => n.Value.TimeM1 > 0 && n.Key.EndsWith("_M1")))
            {
                g.Edges.Add(new()
                {
                    FromID = kvp.Key,
                    ToID = $"{kvp.Key[..^3]}_M2",
                    Cost = kvp.Value.TimeM1
                });
            }

            // sequential edges
            for (int i = 0; i < jobSeq.Count - 1; i++)
            {
                g.Edges.Add(new()
                {
                    FromID = $"{jobSeq[i]}_M1",
                    ToID = $"{jobSeq[i + 1]}_M1",
                    Cost = 0d
                });
                g.Edges.Add(new()
                {
                    FromID = $"{jobSeq[i]}_M2",
                    ToID = $"{jobSeq[i + 1]}_M2",
                    Cost = 0d
                });
            }

            // products (J_i_M2 -> P_i)
            if (jobSeq.Count > 0)
                g.Edges.Add(new()
                {
                    FromID = $"{jobSeq.Last()}_M2",
                    ToID = $"P{int.Parse(jobSeq.Last().Replace("J", ""))}",
                    Cost = 0d
                });

            double makespan =
                GetLongestPathEFTs(isSilent)
                .Where(kvp => kvp.Key.StartsWith('P'))
                .Max(kvp => kvp.Value);

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

                foreach (Edge e in g.Edges.Where(e => e.FromID == u))
                {
                    string v = e.ToID;
                    double cost = e.Cost;

                    Node nodeU = g.Nodes[u];
                    double durU = (nodeU.TimeM1 > 0) ? nodeU.TimeM1 : nodeU.TimeM2;

                    double reqStartV = (dist[u] + durU) + cost; // EST(u) = EFT(u) + Cost

                    if (dist[v] < reqStartV)
                        dist[v] = reqStartV;
                }
            }

            foreach (NodeKVP kvp in g.Nodes)
            {
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
                if (!isSilent)
                    WriteLine($"Node {n} added to topological order.");

                foreach (Edge e in edges.Where(e => e.FromID == n))
                {
                    inDegree[e.ToID]--;
                    if (inDegree[e.ToID] is 0)
                        zeroInDegree.Enqueue(e.ToID);

                    if (!isSilent)
                        WriteLine($"Decreased in-degree of {e.ToID} to {inDegree[e.ToID]}.");
                }
            }
            if (topoOrder.Count != nodes.Count)
            {
                if (!isSilent)
                    Error.WriteLine("Graph has at least one cycle; topological sort not possible.");
                throw new FormatException("Graph has at least one cycle; topological sort not possible.");
            }

            if (!isSilent)
                WriteLine("Topological sort completed successfully.");
            return topoOrder;
        }
    }
}