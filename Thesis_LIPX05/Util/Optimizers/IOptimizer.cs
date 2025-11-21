using static Thesis_LIPX05.Util.PrecedenceGraph;

namespace Thesis_LIPX05.Util.Optimizers
{
    // The base interface for all optimizers
    public interface IOptimizer
    {
        public List<Node> Optimize();
    }
}
