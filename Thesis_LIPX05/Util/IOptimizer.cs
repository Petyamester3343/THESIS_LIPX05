using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    // The base class for all optimizers (the origin of their inheritance)
    public interface IOptimizer
    {
        public List<Node> Optimize();
    }
}
