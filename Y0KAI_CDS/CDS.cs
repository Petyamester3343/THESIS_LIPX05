using static System.Console;

using NodeKVP = System.Collections.Generic.KeyValuePair<string, Y0KAI_CDS.Node>;

namespace Y0KAI_CDS
{
    internal class CDS(Graph g)
    {
        private class VJobData
        {
            public required string V_ID { get; set; }
            public required double V_TM1 { get; set; }
            public required double V_TM2 { get; set; }
        }

        private int GetMachineCount() => (from key in g.Nodes.Keys
                                          where key.Contains("_M")
                                          select key[..^2])
                                            .Distinct()
                                            .Count();

        private double GetTimeForMachine(string jobID, int mID)
        {
            if (g.Nodes.TryGetValue($"{jobID}_M{mID}", out Node? n))
            {
                if (mID == 1) return n.TimeM1;
                else if (mID == 2) return n.TimeM2;
                else return 0d;
            }

            return 0d;
        }

        private double CalcCMax(List<string> seq, bool isSilent)
        {
            g.Edges.Clear();

            foreach (string id in seq)
                g.Edges.Add(new()
                {
                    FromID = $"{id}_M1",
                    ToID = $"{id}_M2",
                    Cost = 0d
                });

            for (int i = 0; i < seq.Count - 1; i++)
                for (int m = 1; m <= GetMachineCount(); m++)
                    g.Edges.Add(new()
                    {
                        FromID = $"{seq[i]}_M{m}",
                        ToID = $"{seq[i + 1]}_M{m}",
                        Cost = 0d
                    });

            if (seq.Count > 0)
                g.Edges.Add(new()
                {
                    FromID = $"{seq.Last()}_M2",
                    ToID = $"P{int.Parse(seq.Last().Replace("J", ""))}",
                    Cost = 0d
                });

            double makespan = GetLongestPathEFTs(isSilent)
                              .Where(kvp => kvp.Key.StartsWith('P') && kvp.Value is not double.MaxValue)
                              .Max(kvp => kvp.Value);

            return (makespan <= 0) ? double.MaxValue : makespan;
        }

        private Dictionary<string, double> GetLongestPathEFTs(bool isSilent)
        {
            List<string> topo = TopoSort(g.Nodes, g.Edges, isSilent);
            if (topo.Count is 0)
                return g.Nodes.Keys.ToDictionary(k => k, v => double.MaxValue);

            // sentinel value for unreached nodes
            const double MinDist = -1.0;

            Dictionary<string, double> dist = g.Nodes.Keys.ToDictionary(k => k, v => MinDist);
            Dictionary<string, int> inDeg = g.Nodes.Keys.ToDictionary(k => k, v => 0);

            foreach (Edge e in g.Edges)
                if (g.Nodes.ContainsKey(e.ToID))
                    inDeg[e.ToID]++;
                else
                {
                    if (!isSilent) Error.WriteLine($"Warning: Edge to non-existent node {e.ToID} ignored.");
                }

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

        private static List<string> TopoSort(Dictionary<string, Node> nodes, List<Edge> edges, bool isSilent)
        {
            Dictionary<string, int> inDegree = nodes.Keys.ToDictionary(k => k, v => 0);
            foreach (Edge e in edges)
                if (nodes.ContainsKey(e.ToID))
                    inDegree[e.ToID]++;
                else
                {
                    if (!isSilent) Error.WriteLine($"Warning: Edge to non-existent node {e.ToID} ignored.");
                }

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

        public List<string> Solve(bool isSilent)
        {
            if (GetMachineCount() < 2)
            {
                if (!isSilent) Error.WriteLine("Error: Johnson's algorithm requires at least two machines.");
                return [];
            }

            List<string>
                baseJobIDs = [.. (from gk in g.Nodes.Keys where gk.EndsWith("_M1") select gk[..^3]).Distinct()],
                best = [];

            double minCMax = double.MaxValue;

            for (int k = 1; k <= GetMachineCount() - 1; k++)
            {
                List<VJobData> j4j = [];

                foreach (string id in baseJobIDs)
                {
                    double
                        tM1 = 0d,
                        tM2 = 0d;

                    for (int m = 1; m <= k; m++)
                        tM1 += GetTimeForMachine(id, m);

                    for (int m = GetMachineCount() - k + 1; m <= GetMachineCount(); m++)
                        tM2 += GetTimeForMachine(id, m);

                    if (tM1 > 0 || tM2 > 0)
                        j4j.Add(new()
                        {
                            V_ID = id,
                            V_TM1 = tM1,
                            V_TM2 = tM2
                        });
                }

                if (j4j.Count is 0) continue;

                List<string> candidate = RunJohnson(j4j);

                double cMax = CalcCMax(candidate, isSilent);
                if (cMax < minCMax)
                {
                    minCMax = cMax;
                    best = candidate;
                }
            }

            if (minCMax is double.MaxValue && !isSilent)
            {
                Error.WriteLine("No feasible schedule found for any sequence.");
                return [];
            }

            return best;

        }

        private static List<string> RunJohnson(List<VJobData> jobs)
        {
            List<VJobData>
                s1 = [], // M1 < M2
                s2 = []; // M1 >= M2

            foreach (VJobData job in jobs)
            {
                if (job.V_TM1 < job.V_TM2) s1.Add(job);
                else s2.Add(job);
            }

            s1.Sort((a, b) => a.V_TM1.CompareTo(b.V_TM1)); // Ascending M1
            s2.Sort((a, b) => b.V_TM2.CompareTo(a.V_TM2)); // Descending M2

            return [.. from j1 in s1 select j1.V_ID, .. from j2 in s2 select j2.V_ID];
        }
    }
}
