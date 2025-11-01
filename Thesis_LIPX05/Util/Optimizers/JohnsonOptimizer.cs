using static Thesis_LIPX05.Util.LogManager;
using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util.Optimizers
{
    internal class JohnsonOptimizer(Dictionary<string, Node> nodes, List<Edge> edges) : IOptimizer
    {
        private const string LogCtx = "Johnson's Rule Optimizer";

        private class FSJobData
        {
            public required string ID { get; set; }
            public required double TimeM1 { get; set; }
            public required double TimeM2 { get; set; }
        }

        //Johnson's Rule implementation
        public List<Node> Optimize()
        {
            List<FSJobData> jobs = [.. nodes.Values
                .Where(n => n.ID.EndsWith("_M1") && n.TimeM1 > 0)
                .Select(n => {
                    string baseID = n.ID[..^3];
                    double timeM2 = nodes.TryGetValue($"{baseID}_M2", out Node? m2Node) ? m2Node.TimeM2 : double.MaxValue;
                    return new FSJobData { ID = baseID, TimeM1 = n.TimeM1, TimeM2 = timeM2 };
                })
                .Where(j => j.TimeM1 > 0 && j.TimeM2 > 0)];

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

        // Applies Johnson's Rule to determine the optimal job sequence
        private static List<string> RunJohnsonRule(List<FSJobData> jobs)
        {
            List<FSJobData>
                s1 = [], // M1 <= M2
                s2 = []; // M1 > M2

            foreach (FSJobData job in jobs)
                if (job.TimeM1 <= job.TimeM2) s1.Add(job);
                else s2.Add(job);

            s1.Sort((a, b) => a.TimeM1.CompareTo(b.TimeM1));
            s2.Sort((a, b) => b.TimeM2.CompareTo(a.TimeM2));

            return [.. s1.Select(j => j.ID), .. s2.Select(j => j.ID)];
        }

        // Rebuilds the S-Graph based on the optimized job sequence to calculate makespan
        public List<Node> RebuildGraphForMakespan(List<string> optSeq)
        {
            // 1.: clear edges
            edges.Clear();
            LogSolverActivity(LogSeverity.INFO, "Cleared all existing S-Graph edges for sequence rebuild.", LogCtx);

            // 2.: re-adding tech. edges
            foreach (Node node in nodes.Values.Where(n => n.ID.EndsWith("_M1")))
            {
                string jobID = node.ID[..^3];
                // Cost = duration of the M1 task (technological precedence)
                AddEdge(node.ID, $"{jobID}_M2");
            }

            // 3. M1 and M2 sequences (Job_k_Mx -> Job_k+1_Mx)
            for (int i = 0; i < optSeq.Count - 1; i++)
            {
                string
                    idA_M1 = $"{optSeq[i]}_M1",
                    idB_M1 = $"{optSeq[i + 1]}_M1",
                    idA_M2 = $"{optSeq[i]}_M2",
                    idB_M2 = $"{optSeq[i + 1]}_M2";

                // The cost must be 0, as EST/EFT calculation handles the duration.
                AddEdge(idA_M1, idB_M1);
                AddEdge(idA_M2, idB_M2);
            }

            // 4.: re-applying terminating edges
            for (int i = 1; i <= 3; i++) AddEdge($"J{i}_M2", $"P{i}");
            
            LogSolverActivity(LogSeverity.INFO,
                "Graph rebuilt with necessary technological constraints (cost=duration) and zero-cost sequential constraints.", LogCtx);

            // 5.: return Topological Sort result
            return [.. TopoSort(nodes, edges).Select(id => nodes[id])];
        }

        // Performs a topological sort on the directed graph (Kahn's algorithm)
        public static List<string> TopoSort(Dictionary<string, Node> allNodes, List<Edge> allEdges)
        {
            Dictionary<string, int> inDegree = allNodes.Keys.ToDictionary(k => k, v => 0);
            foreach (Edge e in allEdges) inDegree[e.To.ID]++;

            Queue<string> zeroInDegree = new(inDegree.Where(kvp => kvp.Value is 0).Select(kvp => kvp.Key));
            List<string> topoOrder = [];

            while (zeroInDegree.Count is not 0)
            {
                string n = zeroInDegree.Dequeue();
                topoOrder.Add(n);
                LogSolverActivity(LogSeverity.INFO, $"Node {n} added to topological order.", LogCtx);

                foreach (Edge e in allEdges.Where(e => e.From.ID == n))
                {
                    string m = e.To.ID;
                    inDegree[m]--;
                    if (inDegree[m] is 0) zeroInDegree.Enqueue(m);
                    LogSolverActivity(LogSeverity.INFO, $"Decreased in-degree of {m} to {inDegree[m]}.", LogCtx);
                }
            }
            if (topoOrder.Count != allNodes.Count)
            {
                LogSolverActivity(LogSeverity.ERROR, "Graph has at least one cycle; topological sort not possible.", LogCtx);
                throw new InvalidOperationException("Graph is not a DAG; topological sort failed.");
            }

            LogSolverActivity(LogSeverity.INFO, "Topological sort completed successfully.", LogCtx);
            return topoOrder;
        }
    }
}