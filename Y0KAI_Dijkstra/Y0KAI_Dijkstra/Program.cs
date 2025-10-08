using static Y0KAI_Dijkstra.SolverData;

namespace Y0KAI_Dijkstra
{
    internal class Program
    {
        static void Main(string[] args)
        {
            bool isSilent = false;

            foreach (string arg in args)
            {
                if (arg.Equals("-s", StringComparison.OrdinalIgnoreCase) || arg.Equals("--silent_mode", StringComparison.OrdinalIgnoreCase))
                {
                    isSilent = true;
                    break;
                }
            }

            Graph g = ReadGraph();
            if (g.Nodes.Count == 0) return;

            var finalPathIDs = Dijkstra.Solve(g, isSilent);

            foreach (string nodeID in finalPathIDs)
                if (!isSilent) Console.WriteLine($"NODE {nodeID}");
        }

        private static Graph ReadGraph()
        {
            Graph g = new();
            string? line;

            while ((line = Console.ReadLine()) != null)
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 0)
                {
                    string type = parts[0];

                    if (type.Equals("NODE") && parts.Length >= 3)
                    {
                        g.Nodes.TryAdd(parts[1], new Node { ID = parts[1], Desc = parts[2] });
                    }
                    else if (type.Equals("EDGE") && parts.Length >= 4)
                    {
                        if (double.TryParse(parts[3], out double cost))
                        {
                            g.Edges.Add(new() { FromID = parts[1], ToID = parts[2], Cost = cost });
                        }
                    }
                }
            }
            return g;
        }
    }
}
