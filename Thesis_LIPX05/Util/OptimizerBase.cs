namespace Thesis_LIPX05.Util
{
    public abstract class OptimizerBase : IOptimizer
    {
        protected readonly Dictionary<string, SGraph.Node> nodes4Gantt;
        protected readonly List<SGraph.Edge> edges4Gantt;

        protected OptimizerBase(Dictionary<string, SGraph.Node> nodes, List<SGraph.Edge> edges)
        {
            nodes4Gantt = nodes;
            edges4Gantt = edges;
        }

        public abstract List<SGraph.Node> Optimize();
    }
}
