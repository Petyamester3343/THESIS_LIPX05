using static System.Console;

using BnBPQ = System.Collections.Generic.PriorityQueue<Y0KAI_BnB.BranchAndBound.BnBNode, double>;
using JobList = System.Collections.Generic.List<Y0KAI_BnB.BranchAndBound.JobData>;
using NodeKVP = System.Collections.Generic.KeyValuePair<string, Y0KAI_BnB.Node>;

namespace Y0KAI_BnB
{
    internal class BranchAndBound(Graph g)
    {
        public class JobData
        {
            public required string ID { get; set; }
            public required double TimeM1 { get; set; } // V_A Time
            public required double TimeM2 { get; set; } // V_B Time
        }

        public class BnBNode
        {
            public List<string> ScheduledJobs { get; set; } = [];
            public HashSet<string> UnscheduledJobs { get; set; } = [];
            public double LowerBound { get; set; } = double.MaxValue;
        }

        public List<string> Solve(bool isSilent)
        {
            List<string>
                baseJobIDs = GetBaseJobIDs(),
                initSeq = RunJohnson(GetJobs4Johnson(baseJobIDs));

            double currCUB = CalcMakespan(initSeq, isSilent);

            List<string> bestSch = initSeq;

            if (currCUB is double.MaxValue)
            {
                Error.WriteLine("Initial C_UB is infeasable. Cannot start Branch & Bound!");
                return [];
            }

            BnBPQ openList = new();

            BnBNode root = new()
            {
                UnscheduledJobs = [.. baseJobIDs],
                ScheduledJobs = [],
                LowerBound = CalcLowerBound([])
            };
            openList.Enqueue(root, root.LowerBound);

            while (openList.Count > 0)
            {
                BnBNode curr = openList.Dequeue();
                if (curr.LowerBound >= currCUB) continue;

                foreach (string j2s in curr.UnscheduledJobs)
                {
                    List<string> newSeq = [.. curr.UnscheduledJobs, j2s];
                    double newLB = CalcLowerBound(newSeq);

                    if (newLB < currCUB)
                    {
                        if (newSeq.Count == baseJobIDs.Count)
                        {
                            double currMs = CalcMakespan(newSeq, isSilent);
                            if (currMs < currCUB)
                            {
                                currCUB = currMs;
                                bestSch = newSeq;
                            }
                        }
                        else
                        {
                            BnBNode newNode = new()
                            {
                                ScheduledJobs = newSeq,
                                UnscheduledJobs = [.. curr.UnscheduledJobs.Except([j2s])],
                                LowerBound = newLB
                            };
                            openList.Enqueue(newNode, newNode.LowerBound);
                        }
                    }
                }
            }

            if (!isSilent) WriteLine("Optimization complete!");

            return bestSch;
        }

        private JobList GetJobs4Johnson(List<string> baseJobIDs)
        {
            JobList j4j = [];

            foreach (string bID in baseJobIDs)
            {
                double
                    tm1 = g.Nodes.TryGetValue($"{bID}_M1", out Node? m1N) ? m1N.TimeM1 : 0d,
                    tm2 = g.Nodes.TryGetValue($"{bID}_M2", out Node? m2N) ? m2N.TimeM2 : 0d;

                if (tm1 > 0 || tm2 > 0)
                    j4j.Add(new()
                    {
                        ID = bID,
                        TimeM1 = tm1,
                        TimeM2 = tm2
                    });
            }

            return (j4j.Count is not 0) ? j4j : [];
        }

        private List<string> GetBaseJobIDs() => [.. (from id in g.Nodes.Keys
                 where id.EndsWith("_M1")
                 select id[..^3])
                .Distinct()];

        private static List<string> RunJohnson(JobList jobs)
        {
            JobList
                s1 = [],
                s2 = [];

            foreach (JobData job in jobs)
                if (job.TimeM1 <= job.TimeM2) s1.Add(job);
                else s2.Add(job);

            s1.Sort((a, b) => a.TimeM1.CompareTo(b.TimeM1));
            s2.Sort((a, b) => a.TimeM2.CompareTo(b.TimeM2));

            return [.. s1.Select(j => j.ID), .. s2.Select(j => j.ID)];
        }

        private double CalcMakespan(List<string> jobSeq, bool isSilent)
        {
            g.Edges.Clear();

            foreach (NodeKVP kvp in g.Nodes.Where(n => n.Value.TimeM1 > 0 && n.Key.EndsWith("_M1")))
                g.Edges.Add(new()
                {
                    FromID = kvp.Key,
                    ToID = $"{kvp.Key[..^3]}_M2",
                    Cost = 0d
                });

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

            if (jobSeq.Count > 0)
                g.Edges.Add(new()
                {
                    FromID = $"{jobSeq.Last()}_M2",
                    ToID = $"P{int.Parse(jobSeq.Last().Replace("J", ""))}",
                    Cost = 0d
                });

            Dictionary<string, double> eftTimes = GetLongestPathEFTs(isSilent);

            double makespan = eftTimes.Where(kvp => kvp.Key.StartsWith('P')).Max(kvp => kvp.Value);
            return (makespan <= 0 || makespan is double.MaxValue) ? double.MaxValue : makespan;
        }

        private (double CM1, double CM2) GetPartialCompletionTimes(List<string> schJobs)
        {
            double
                lastFM1 = 0,
                lastFM2 = 0;

            foreach (string job in schJobs)
            {
                Node
                    m1N = g.Nodes[$"{job}_M1"],
                    m2N = g.Nodes[$"{job}_M2"];

                double startM1 = lastFM1;
                lastFM1 = startM1 + m1N.TimeM1;

                double startM2 = Math.Max(lastFM2, lastFM1);
                lastFM2 = startM2 + m2N.TimeM2;
            }

            return (lastFM1, lastFM2);
        }

        private double CalcLowerBound(List<string> pSeq)
        {
            (double CM1P, double CM2P) = GetPartialCompletionTimes(pSeq);

            List<string> allJobs = GetBaseJobIDs();
            HashSet<string> unscheduledSet = [.. allJobs.Except(pSeq)];

            double
                remTM1 = 0d,
                remTM2 = 0d;

            foreach (string job in unscheduledSet)
            {
                remTM1 += g.Nodes.TryGetValue($"{job}_M1", out Node? m1Node) ? m1Node.TimeM1 : 0d;
                remTM2 += g.Nodes.TryGetValue($"{job}_M2", out Node? m2Node) ? m2Node.TimeM2 : 0d;
            }

            double
                minT2Rem = GetMinimumT2Remaining(unscheduledSet),
                LBM1 = CM1P + remTM1 + minT2Rem,
                LBM2 = CM2P + remTM2;

            return Math.Max(LBM1, LBM2);
        }

        private double GetMinimumT2Remaining(HashSet<string> shs) => shs.Count is not 0
            ? shs.Min(job => g.Nodes.TryGetValue($"{job}_M2", out Node? m2Node) ? m2Node.TimeM2 : double.MaxValue)
            : 0d;

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
                inDeg[e.ToID]++;

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
            foreach (Edge e in edges)
                inDegree[e.ToID]++;

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
