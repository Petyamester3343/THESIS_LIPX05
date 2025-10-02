namespace Thesis_LIPX05.Util
{
    // A helper class for storing custom solvers
    internal class CustomSolver
    {
        public required string Name { get; set; }
        public required string Path { get; set; }
        public required string[] Arguments { get; set; }
    }
}
