using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    internal class MINLPOptimizer(
        Dictionary<string, Node> nodes,
        List<Edge> edges,
#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
        string execPath) : OptimizerBase(nodes, edges)
#pragma warning restore CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
    {
        public override List<Node> Optimize()
        {
            var osilPath = Path.GetTempFileName() + ".osil";
            File.WriteAllText(osilPath, GenOsilFromGraph(GetNodes(), GetEdges()));

            string bonminPath = execPath; // Adjust this path to your Bonmin installation
            if (!File.Exists(bonminPath))
            {
                MessageBox.Show($"Bonmin not found at {bonminPath}!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return [];
            }
            string resPath = Path.ChangeExtension(osilPath, ".sol");

            var psi = new ProcessStartInfo
            {
                FileName = bonminPath,
                Arguments = $" -osil {osilPath} -solution {resPath}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Bonmin process.");

            const int timeoutMS = 120000;
            if (!proc.WaitForExit(timeoutMS))
            {
                try
                {
                    proc.Kill(entireProcessTree: true);
                }
                catch { /* ignored */ }
                throw new TimeoutException($"Bonmin exceeded the {timeoutMS / 1000}s timeout!");
            }

            if (proc?.ExitCode != 0)
            {
                MessageBox.Show($"Bonmin failed with exit code {proc?.ExitCode}!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return [];
            }

            if (File.Exists(resPath) && new FileInfo(resPath).Length != 0)
            {
                var solution = File.ReadAllText(resPath);
                if (string.IsNullOrWhiteSpace(solution))
                {
                    MessageBox.Show("Bonmin returned an empty solution!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return [];
                }

                var parsed = ParseBonminSolution(solution);
                var sorted = GetSortedTaskStarts(parsed);

                return [.. sorted.Select(s => nodes[s.id])];
            }
            else throw new InvalidOperationException("Bonmin did not produce a solution file!");
        }

        // Generates the OSiL XML from the graph
        public static string GenOsilFromGraph(Dictionary<string, Node> nodes, List<Edge> edges)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<osil xmlns=\"http://www.coin-or.org/OSiL/1.0\">");
            sb.AppendLine("  <instanceHeader><name>SGraphSchedule</name></instanceHeader>");

            // the variables: t_{node.ID} for each node, and T for the makespan
            sb.AppendLine("  <variables>");
            int i = 0;
            foreach (var node in nodes.Values)
            {
                sb.AppendLine($"    <var name=\"t_{node.ID}\" type=\"C\" lb=\"0\" ub=\"1000\"/>");
                i++;
            }
            int makespan_idx = i; // index for T
            sb.AppendLine($"    <var name=\"T\" type=\"C\" lb=\"0\" ub=\"10000\"/>");
            sb.AppendLine("  </variables>");

            // objective: minimize the max end time of the batch
            sb.AppendLine("  <objectives>");
            sb.AppendLine("    <obj maxOrMin=\"min\" name=\"Makespan\">");
            sb.AppendLine($"       <coef idx=\"{makespan_idx}\">1</coef>");
            sb.AppendLine("    </obj>");
            sb.AppendLine("  </objectives>");

            // constraints
            sb.AppendLine("  <constraints>");
            int ci = 0;
            // precedence edges (t_to - t_from >= cost)
            foreach (var edge in edges) sb.AppendLine($"    <con name=\"Prec_{ci++}\" lb=\"{edge.Cost}\" ub=\"Infinity\"/>");

            // makespan constraint: T - {t_i + duration_i} >= 0
            foreach (var node in nodes.Values) sb.AppendLine($"    <con name=\"Makespan_{node.ID}\" lb=\"0\" ub=\"Infinity\"/>");
            sb.AppendLine("  </constraints>");

            // linear matrix (each constraint: t_to - t_from >= cost)
            sb.AppendLine("  <linearConstraintCoefficients>");

            int m = edges.Count + nodes.Count; // total number of constraints
            var start = new List<int> { 0 };
            var rowIdx = new List<int>();
            var vals = new List<int>();

            // precedence: (t_to - t_from >= cost)
            foreach (var _ in edges)
            {
                int r = rowIdx.Count;
                rowIdx.Add(r); vals.Add(1); // t_to
                rowIdx.Add(r); vals.Add(-1); // t_from
                start.Add(rowIdx.Count);
            }

            // makespan: (T - t_i >= 0)
            int rBase = edges.Count;
            foreach (var node in nodes.Values.Select((n, j) => new { n, j }))
            {
                int r = rBase + node.j;
                rowIdx.Add(r); vals.Add(1); // T
                rowIdx.Add(r); vals.Add(-1); // t_i
                start.Add(rowIdx.Count);
            }

            sb.Append("    <start>"); foreach (var s in start) sb.Append($"{s} "); sb.AppendLine("</start>");
            sb.Append("    <rowIdx>"); foreach (var r in rowIdx) sb.Append($"{r} "); sb.AppendLine("</rowIdx>");
            sb.Append("    <value>"); foreach (var v in vals) sb.Append($"{v} "); sb.AppendLine("</value>");

            sb.AppendLine("  </linearConstraintCoefficients>");
            sb.AppendLine("</osil>");

            return sb.ToString();
        }

        // Parses the solution text from Bonmin and returns a dictionary of task starts
        public static Dictionary<string, double> ParseBonminSolution(string solText)
        {
            var res = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in solText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && double.TryParse(parts[1], out double value))
                {
                    string varName = parts[0];
                    if (varName.StartsWith("t_", StringComparison.OrdinalIgnoreCase))
                    {
                        string nodeId = varName[2..]; // remove "t_"
                        res[nodeId] = value;
                    }
                }
            }

            return res;
        }

        // Returns a sorted list of task starts by their start time (contains tuples)
        public static List<(string id, double start)> GetSortedTaskStarts(Dictionary<string, double> starts) => [.. starts.OrderBy(kv => kv.Value).Select(kv => (id: kv.Key, start: kv.Value))];
    }
}
