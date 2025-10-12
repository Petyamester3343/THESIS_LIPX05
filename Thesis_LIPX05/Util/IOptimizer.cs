using static Thesis_LIPX05.Util.SGraph;

namespace Thesis_LIPX05.Util
{
    // The base interface for all optimizers
    public interface IOptimizer
    {
        public List<Node> Optimize();
    }
}
