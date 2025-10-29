namespace Y0KAI_BnB
{
    public class Node
    {
        public required string ID {  get; set; }
        public required string Desc { get; set; }
        public required double TimeM1 { get; set; }
        public required double TimeM2 { get; set; }
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
