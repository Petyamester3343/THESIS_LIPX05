using static Thesis_LIPX05.Util.LogManager;
using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util.Optimizers
{
    internal class LSOptimizer(Dictionary<string, Node> nodes, List<Edge> edges) : IOptimizer
    {
        private const string LogCtx = "List Scheduling";

        public List<Node> Optimize()
        {
            LogSolverActivity(LogSeverity.INFO,
                "Starting list scheuler...", LogCtx);

            // create a feasible base sequence
            List<string> initSeq = JohnsonOptimizer.TopoSort(nodes, edges);

            List<string> jobSeq = [.. from id in initSeq
                                      where id.EndsWith("_M1") && nodes.ContainsKey(id)
                                      select id[..^3]];

            // clearup before work
            edges.Clear();

            // add sequential edges (J_i_MX -> J_i+1_MX) and product links (J_i_M2 -> P_i)
            RemedyNecessaryEdges(jobSeq);

            // create sequence from the newly constrained graph.
            List<string> finalSchIDs = JohnsonOptimizer.TopoSort(nodes, edges);

            // -- Time calculation (EST/EFT) [recycling the logic from Johnson's Rule] --
            Dictionary<string, double> EFT = [];

            // EFT initialization
            foreach (Node n in nodes.Values)
                EFT[n.ID] = 0d;

            // filling the Gantt chart
            List<Node> ganttPath = [];
            foreach (string id in finalSchIDs)
            {
                if (!nodes.TryGetValue(id, out Node? curr) || curr.ID.StartsWith('P'))
                {
                    LogSolverActivity(LogSeverity.INFO,
                        "Product node detected, skipping...", LogCtx);
                    continue;
                }

                double dur = curr.TimeM1 > 0 ? curr.TimeM1 : curr.TimeM2;
                if (dur <= 0) continue;

                double est = 0d;
                List<Edge> pred = [.. from e in edges
                                      where e.To.ID == curr.ID
                                      select e];

                foreach (Edge e in pred)
                {
                    if (EFT.TryGetValue(e.From.ID, out double predEFT))
                        est = Math.Max(est, predEFT + e.Cost);
                    LogSolverActivity(LogSeverity.INFO,
                        $"Estimated start time updated ({est})!", LogCtx);
                }

                EFT[curr.ID] = est + dur;
                LogSolverActivity(LogSeverity.INFO,
                    $"EFT for {curr.ID} updated({EFT[curr.ID]})!", LogCtx);

                ganttPath.Add(curr);
                LogSolverActivity(LogSeverity.INFO,
                    $"Node {curr.ID} added to the Gantt path!", LogCtx);
            }

            double finalMakespan = EFT
                .Where(kvp => kvp.Key.StartsWith('P'))
                .Max(kvp => kvp.Value);

            LogSolverActivity(LogSeverity.INFO,
                $"Optimization complete! Makespan: {finalMakespan:F2}.", LogCtx);

            return ganttPath;
        }

        private void RemedyNecessaryEdges(List<string> jobSeq)
        {
            // re-apply technological edges
            foreach (Node node in nodes.Values.Where(n => n.ID.EndsWith("_M1")))
                AddEdge(node.ID, $"{node.ID[..^3]}_M2");

            // add sequential edges edges (J_i_Mx -> J_i+1_Mx)
            for (int i = 0; i < jobSeq.Count - 1; i++)
                for (int j = 1; j <= MainWindow.GetMachineCount(); j++)
                    AddEdge($"{jobSeq[i]}_M{j}", $"{jobSeq[i + 1]}_M{j}");


            // re-apply terminating edges between the last of the machines and the products
            foreach (string jobID in jobSeq)
            {
                int edgeNum = int.Parse(jobID.Replace("J", ""));
                AddEdge($"J{edgeNum}_M{MainWindow.GetMachineCount()}", $"P{edgeNum}");
            }

            LogSolverActivity(LogSeverity.INFO,
                "Vital edges added to the graph!", LogCtx);
        }
    }
}