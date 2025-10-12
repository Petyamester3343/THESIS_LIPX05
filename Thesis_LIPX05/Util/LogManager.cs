using System.IO;

namespace Thesis_LIPX05.Util
{
    public class LogManager
    {
        public readonly static StreamWriter logWriter = new($"Log\\Y0KAILog_{DateTime.Now:yyyy-mm-dd_hh-mm-ss}.log");

        public static void Log(LogSeverity severity, string msg)
        {
            logWriter.WriteLine($"{DateTime.Now:yyyy. mm. dd. hh:mm:ss} // {severity} // {msg}");
            logWriter.Flush();
        }

        public enum LogSeverity
        {
            INFO,
            WARNING,
            ERROR
        }
    }

    public class SolverLogManager(string solverName) : LogManager
    {
        public string SolverName { get; set; } = solverName;

        public void LogSolverActivity(LogSeverity severity, string msg)
        {
            logWriter.WriteLine($"{DateTime.Now:yyyy. mm. dd. hh:mm:ss} // {severity} // [{SolverName}] {msg}");
        }
    }
}
