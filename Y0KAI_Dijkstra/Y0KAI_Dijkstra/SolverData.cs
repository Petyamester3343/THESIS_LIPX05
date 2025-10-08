using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Y0KAI_Dijkstra
{
    internal class SolverData
    {
        public class Node
        {
            public required string ID { get; set; }
            public required string Desc { get; set; }
        }

        public class Edge
        {
            public required string FromID { get; set; }
            public required string ToID { get; set; }
            public required double Cost { get; set; }
        }

        public class Graph
        {
            public Dictionary<string, Node> Nodes { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<Edge> Edges { get; } = [];
        }
    }
}
