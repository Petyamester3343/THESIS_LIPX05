using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    // The base class for all optimizers (the origin of their inheritance)
    public abstract class OptimizerBase(Dictionary<string, Node> nodes, List<Edge> edges)
    {
        protected readonly Dictionary<string, Node> nodes4Gantt = nodes;
        protected readonly List<Edge> edges4Gantt = edges;

        public abstract List<Node> Optimize();
    }
}
