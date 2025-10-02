using System.IO;

namespace Thesis_LIPX05.Util
{
    public class LogManager
    {
        public StreamWriter logWriter = new($"\\..\\Log\\Y0KAILog_{DateTime.Now:yyyy-mm-dd_hh-mm-ss}.log");

        public void Log(LogSeverity severity, string message)
        {
            logWriter.WriteLine($"{DateTime.Now:yyyy. mm. dd. hh:mm:ss} // {severity} // {message}");
            logWriter.Flush();
        }

        public enum LogSeverity
        {
            INFO,
            WARNING,
            ERROR
        }
    }
}
