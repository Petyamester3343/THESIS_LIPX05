using static System.Console;

using static Y0KAI_Dijkstra.SolverData;

namespace Y0KAI_Dijkstra
{
    internal static class Dijkstra
    {
        public static List<string> Solve(Graph g, bool isSilent)
        {
            PriorityQueue<string, double> pq = new();

            var dist = g.Nodes.Keys.ToDictionary(k => k, _ => double.PositiveInfinity);
            Dictionary<string, string> pred = [];

            var start = g.Nodes.Keys.Where(n => !g.Edges.Any(e => e.ToID == n));

            foreach (var s in start)
            {
                dist[s] = 0;
                pq.Enqueue(s, 0); // node's ID, cost
            }

            while (pq.Count > 0)
            {
                if (!pq.TryDequeue(out string? uID, out double uDist))
                {
                    if (!isSilent) WriteLine("Minimal elements unavailable, proceeding to continue...");
                    continue;
                }

                if (uDist > dist[uID])
                {
                    WriteLine($"Variable \"dist(U)\" ({uDist}) is bigger than the distance of {uID}, proceeding to continue...");
                    continue;
                }

                foreach (var edge in g.Edges.Where(e => e.FromID == uID))
                {
                    string vID = edge.ToID;
                    double negCost = -edge.Cost;

                    if (dist[uID] + negCost < dist[vID])
                    {
                        dist[vID] = dist[uID] + negCost;
                        pred[vID] = uID;
                        pq.Enqueue(vID, dist[vID]);
                        if (!isSilent) WriteLine($"To: {vID}; Cost: {dist[vID]}");
                    }
                }
            }

            var endNode = dist.Aggregate((min, next) => next.Value < min.Value ? next : min).Key;

            Stack<string> path = [];
            string? curr = endNode;

            while (curr is not null && dist[curr] is not double.PositiveInfinity)
            {
                path.Push(curr);
                if (!isSilent) WriteLine($"Node \"{curr}\" has been pushed into the path stack!");
                if (!pred.TryGetValue(curr, out curr)) 
                {
                    WriteLine($"Current node is unavailable, halting iteration...");
                    break;
                }
            }

            return [.. path.Reverse()];
        }
    }
}
