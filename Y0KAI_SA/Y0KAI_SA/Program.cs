using System.Text.RegularExpressions;

namespace Y0KAI_SA
{
    internal class Program
    {
        static void Main(string[] args)
        {
            double
                initTemp = 1000.0,
                coolRate = 0.995;
            int maxIt = 500;

            bool isSilent = false;
            List<string> posArgs = [];

            foreach(string arg in args)
            {
                if (arg.Equals("-s", StringComparison.OrdinalIgnoreCase)) isSilent = true;
                else posArgs.Add(arg);
            }

            if (args.Length >= 1 && double.TryParse(posArgs[0], out double temp)) initTemp = temp;
            if (args.Length >= 2 && double.TryParse(posArgs[1], out double rate)) coolRate = rate;
            if (args.Length >= 3 && int.TryParse(posArgs[2], out int iter)) maxIt = iter;

            Graph g = ReadGraph();
            if (g.Nodes.Count == 0) return;

            SimulatedAnnealing solver = new(g);
            List<string> finalPathIDs = solver.Solve(initTemp, coolRate, maxIt, isSilent);

            foreach (string node in finalPathIDs)
                if(!isSilent) Console.WriteLine($"NODE {node}");
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
