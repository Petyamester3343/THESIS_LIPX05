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

            List<string> initSeq = JohnsonOptimizer.TopoSort(nodes, edges);

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

                List<Edge> pred = [.. edges.Where(e => e.To.ID == curr.ID)];

                foreach (Edge e in pred)
                    if (EFT.TryGetValue(e.From.ID, out double predEFT))
                        est = Math.Max(est, predEFT + e.Cost);

                EFT[id] = est + dur;
                schPath.Add(curr);
            }

            LogSolverActivity(LogSeverity.INFO, $"List scheduling completed with {schPath.Count} scheduled nodes.", LogCtx);

            return schPath;
        }
    }
}
