using System.Windows;
using static Thesis_LIPX05.Util.LogManager;
using static Thesis_LIPX05.Util.PrecedenceGraph;

namespace Thesis_LIPX05.Util.Optimizers
{
    internal class JohnsonOptimizer(Dictionary<string, Node> nodes, List<Edge> edges) : IOptimizer
    {
        private const string LogCtx = "Johnson's Rule Optimizer";

        private class JobData
        {
            public required string ID { get; set; }

            // to be switched with a double container for more than 2 machines
            public required double TimeM1 { get; set; }
            public required double TimeM2 { get; set; }

            /* for future extensions (more than 2 machines, compatible with flexible flow shop)
            public required double[][] Times { get; set; }
            */
        }

        // Johnson's Rule implementation (with only 2 machines for now)
        public List<Node> Optimize()
        {
            List<JobData> jobs = [.. nodes.Values
                .Where(n => n.ID.EndsWith("_M1") && n.TimeM1 > 0)
                .Select(n => {
                    string baseID = n.ID[..^3];
                    double timeM2 = nodes.TryGetValue($"{baseID}_M2", out Node? m2Node) ? m2Node.TimeM2 : double.MaxValue;
                    return new JobData { ID = baseID, TimeM1 = n.TimeM1, TimeM2 = timeM2 };
                })
                .Where(j => j.TimeM1 > 0 || j.TimeM2 > 0)];

            if (jobs.Count is 0)
            {
                LogGeneralActivity(LogSeverity.ERROR,
                    "No valid jobs found for Johnson's Rule optimization.", GeneralLogContext.INTEG_SOLVER);
                return [];
            }

            List<string> optSeq = RunJohnsonRule(jobs);

            LogSolverActivity(LogSeverity.INFO, $"Optimized job sequence: {string.Join(" -> ", optSeq)}", LogCtx);

            return RebuildGraphForMakespan(optSeq);
        }

        // Applies Johnson's Rule to determine the optimal job sequence (for 2 machines)
        private static List<string> RunJohnsonRule(List<JobData> jobs)
        {
            List<JobData>
                s1 = [], // M1 < M2
                s2 = []; // M1 >= M2

            foreach (JobData job in jobs)
            {
                if (job.TimeM1 < job.TimeM2) s1.Add(job);
                else s2.Add(job);
                jobs.Remove(job);
            }

            s1.Sort((a, b) => a.TimeM1.CompareTo(b.TimeM1));
            s2.Sort((a, b) => b.TimeM2.CompareTo(a.TimeM2));

            return [.. from j1 in s1 select j1.ID, .. from j2 in s2 select j2.ID];
        }

        // Rebuilds the S-Graph based on the optimized job sequence to calculate makespan
        public List<Node> RebuildGraphForMakespan(List<string> optSeq)
        {
            // 1.: clear edges
            edges.Clear();
            LogSolverActivity(LogSeverity.INFO, "Cleared all existing S-Graph edges for sequence rebuild.", LogCtx);

            // 2.: re-adding tech. edges (cost = duration of the M1 task (technological precedence))
            foreach (Node node in nodes.Values.Where(n => n.ID.EndsWith("_M1")))
                AddEdge(node.ID, $"{node.ID[..^3]}_M2");

            // 3. M1 and M2 sequences (Job_k_Mx -> Job_k+1_Mx) (the cost must be 0, as EST/EFT calculation handles the duration)
            for (int i = 0; i < optSeq.Count - 1; i++)
                for (int j = 1; j <= MainWindow.GetMachineCount(); j++)
                    AddEdge($"{optSeq[i]}_M{j}", $"{optSeq[i + 1]}_M{j}");

            // 4.: re-applying terminating edges
            foreach (string optSeqFrag in optSeq)
                AddEdge($"J{int.Parse(optSeqFrag.Replace("J", ""))}_M2", $"P{int.Parse(optSeqFrag.Replace("J", ""))}");
            
            LogSolverActivity(LogSeverity.INFO,
                "Graph rebuilt with necessary technological constraints (cost = duration) and zero-cost sequential constraints.", LogCtx);

            // 5.: return Topological Sort result
            return [.. from id in TopoSort(nodes, edges) select nodes[id]];
        }

        // Performs a topological sort on the directed graph (inspired by Kahn's algorithm)
        public static List<string> TopoSort(Dictionary<string, Node> allNodes, List<Edge> allEdges)
        {
            Dictionary<string, int> inDegree = allNodes.Keys.ToDictionary(k => k, v => 0);
            foreach (Edge e in allEdges) inDegree[e.To.ID]++;

            Queue<string> zeroInDegree = new(from kvp in inDegree where kvp.Value is 0 select kvp.Key);
            List<string> topoOrder = [];

            try
            {
                while (zeroInDegree.Count is not 0)
                {
                    string n = zeroInDegree.Dequeue();
                    topoOrder.Add(n);
                    LogSolverActivity(LogSeverity.INFO,
                        $"Node {n} added to topological order.", LogCtx);

                    foreach (Edge e in allEdges.Where(e => e.From.ID == n))
                    {
                        inDegree[e.To.ID]--;
                        if (inDegree[e.To.ID] is 0)
                            zeroInDegree.Enqueue(e.To.ID);
                        LogSolverActivity(LogSeverity.INFO,
                            $"Decreased in-degree of {e.To.ID} to {inDegree[e.To.ID]}.", LogCtx);
                    }
                }
                if (topoOrder.Count != allNodes.Count)
                    LogSolverActivity(LogSeverity.ERROR,
                        "Graph has at least one cycle; topological sort not possible.", LogCtx);
            }
            catch
            {
                MessageBox.Show("Graph is not a DAG; topological sort failed.",
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return [];
            }

            LogSolverActivity(LogSeverity.INFO,
                "Topological sort completed successfully.", LogCtx);
            return topoOrder;
        }
    }
}