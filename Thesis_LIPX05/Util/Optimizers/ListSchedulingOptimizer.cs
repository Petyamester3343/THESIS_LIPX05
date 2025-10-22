using static Thesis_LIPX05.Util.LogManager;
using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util.Optimizers
{
    internal class ListSchedulingOptimizer(Dictionary<string, Node> nodes, List<Edge> edges) : IOptimizer
    {
        private const string LogCtx = "List Scheduling";

        public List<Node> Optimize()
        {
            LogSolverActivity(LogSeverity.INFO, "Starting List scheduling...", LogCtx);

            // 1.: initial job seqence
            List<string>
                initSeq = JohnsonOptimizer.TopoSort(nodes, edges);

            // the list containing the extracted base jobs based on M1
            List<string> jobSeq = [.. initSeq.Where(id => id.EndsWith("_M1") && nodes[id].TimeM1 > 0).Select(id => id[..^3])];

            // 2.: re-build graph with M1/M2 sequential edges
            RebuildGraphForMakespan(jobSeq);

            // 3.: topological sort on the fully constrained graph
            List<string> finalSch = JohnsonOptimizer.TopoSort(nodes, edges);

            // 4.: time calculation (EST/EFT)
            Dictionary<string, double> EFT = [];
            foreach (Node n in nodes.Values) EFT[n.ID] = 0.0;
            List<Node> schPath = [];

            foreach (string id in initSeq)
            {
                if (!nodes.TryGetValue(id, out Node? curr)) continue;

                double dur = curr.TimeM1 > 0 ? curr.TimeM1 : curr.TimeM2;
                if (dur <= 0)
                {
                    LogSolverActivity(LogSeverity.WARNING, $"Node {curr.ID} has non-positive duration ({dur}), skipping...", LogCtx);
                    continue;
                }

                double est = 0.0; // earliest start time

                // finding logical predecessors (tech + seq)
                List<Edge> pred = [.. edges.Where(e => e.To.ID == curr.ID)];

                foreach (Edge e in pred)
                    if (EFT.TryGetValue(e.From.ID, out double predEFT))
                        est = Math.Max(est, predEFT + e.Cost); // EST is max of (predecessor's EFT + edge cost)

                EFT[id] = est + dur;
                schPath.Add(curr);
            }

            LogSolverActivity(LogSeverity.INFO, $"List scheduling completed with {schPath.Count} scheduled nodes.", LogCtx);
            return schPath;
        }

        public static void RebuildGraphForMakespan(List<string> jobSeq)
        {
            // M1 and M2 sequences (J(i)_M1 -> J(i+1)_M1)
            for (int i = 0; i < jobSeq.Count - 1; i++)
            {
                string
                    idA_M1 = $"{jobSeq[i]}_M1",
                    idB_M1 = $"{jobSeq[i + 1]}_M1";

                AddEdge(idA_M1, idB_M1);
            }

            for (int i = 0; i < jobSeq.Count - 1; i++)
            {
                string
                    idA_M2 = $"{jobSeq[i]}_M2",
                    idB_M2 = $"{jobSeq[i + 1]}_M2";

                AddEdge(idA_M2, idB_M2);
            }

            // connecting the last job to the product nodes
            string lastJobID = jobSeq.Last();

            AddEdge($"{lastJobID}_M1", $"Prod_M1");
            AddEdge($"{lastJobID}_M2", $"Prod_M2");

            LogSolverActivity(LogSeverity.INFO,
                "Graph modified to enforce list scheduling sequence.", LogCtx);
        }
    }
}