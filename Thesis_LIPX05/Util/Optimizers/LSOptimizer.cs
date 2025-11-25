using static Thesis_LIPX05.Util.LogManager;
using static Thesis_LIPX05.Util.PrecedenceGraph;

namespace Thesis_LIPX05.Util.Optimizers
{
    internal class LSOptimizer(Dictionary<string, Node> nodes, List<Edge> edges) : IOptimizer
    {
        private const string LogCtx = "List Scheduling";

        public List<Node> Optimize()
        {
            LogSolverActivity(LogSeverity.INFO,
                "Starting list scheduler...", LogCtx);

            // creating initial job sequence from M1 nodes
            List<string> jobSeq = [.. from id in JohnsonOptimizer.TopoSort(nodes, edges)
                                      where id.EndsWith("_M1") && nodes.ContainsKey(id)
                                      select id[..^3]];

            // clearup before work
            edges.Clear();

            // add sequential edges (J_i_MX -> J_i+1_MX) and product links (J_i_M2 -> P_i)
            RemedyNecessaryEdges(jobSeq);

            // -- Time calculation (EST/EFT) [recycling the logic from Johnson's Rule] --
            Dictionary<string, double> EFT = [];

            // EFT initialization
            foreach (Node n in nodes.Values)
                EFT[n.ID] = 0d;

            // filling the Gantt chart
            List<Node> ganttPath = [];
            foreach (string id in JohnsonOptimizer.TopoSort(nodes, edges))
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
                foreach (Edge e in GetPredicted(curr))
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

        private List<Edge> GetPredicted(Node curr) =>
            [.. from e in edges where e.To.ID == curr.ID select e];

        private void RemedyNecessaryEdges(List<string> jobSeq)
        {
            // re-apply technological edges
            foreach (Node node in nodes.Values.Where(n => n.ID.EndsWith("_M1")))
            {
                AddEdge(node.ID, $"{node.ID[..^3]}_M2");
                LogSolverActivity(LogSeverity.INFO,
                    $"Technological edge added: {node.ID} -> {node.ID[..^3]}_M2", LogCtx);
            }

            // add sequential edges edges (J_i_Mx -> J_i+1_Mx)
            for (int i = 0; i < jobSeq.Count - 1; i++)
                for (int j = 1; j <= MainWindow.GetMachineCount(); j++)
                {
                    AddEdge($"{jobSeq[i]}_M{j}", $"{jobSeq[i + 1]}_M{j}");
                    LogSolverActivity(LogSeverity.INFO,
                        $"Sequential edge added: {jobSeq[i]}_M{j} -> {jobSeq[i + 1]}_M{j}", LogCtx);
                }

            // re-apply terminating edges between the last of the machines and the products
            foreach (string jobID in jobSeq)
            {
                AddEdge($"J{int.Parse(jobID.Replace("J", ""))}_M{MainWindow.GetMachineCount()}", $"P{int.Parse(jobID.Replace("J", ""))}");
                LogSolverActivity(LogSeverity.INFO,
                    $"Terminating edge added: J{int.Parse(jobID.Replace("J", ""))}_M{MainWindow.GetMachineCount()} -> P{int.Parse(jobID.Replace("J", ""))}", LogCtx);
            }

            LogSolverActivity(LogSeverity.INFO,
                "Vital edges added to the graph!", LogCtx);
        }
    }
}