using System.Globalization;
using System.IO;
using System.Text;

namespace Thesis_LIPX05.Util
{
    public class LogManager
    {
        public enum LogSeverity
        {
            INFO,
            WARNING,
            ERROR
        }

        public enum GeneralLogContext
        {
            CLEAR,
            CLOSE,
            CONSTRAINT,
            DATATABLE,
            EXITUS,
            EXPORT,
            EXTERN_SOLVER,
            GANTT,
            INITIALIZATION,
            INTEG_SOLVER,
            MODIFY,
            LOAD,
            SAVE,
            S_GRAPH,
            SYNC,
        }

        private static readonly string CurrDir = Environment.CurrentDirectory;
        private static readonly string LogDirName = "Log";
        private static readonly string LogDirPath = Path.Combine(CurrDir, LogDirName);

        private static StreamWriter? LogWriter;

        static LogManager()
        {
            Init();

            try
            {
                string logFileName = $"Y0KAILog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
                string fullPath = Path.Combine(LogDirPath, logFileName);

                LogWriter = new(fullPath, append: true, encoding: Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FATAL: Failed to initialize log writer: {ex.Message}");
            }
        }

        public static DirectoryInfo Init() =>
            !Directory.Exists(LogDirPath) ? Directory.CreateDirectory(LogDirPath) : new(LogDirPath);
        
        public static void LogGeneralActivity(LogSeverity severity, string msg, GeneralLogContext ctx)
        {
            if (LogWriter is null) return;

            LogWriter.WriteLine($"{DateTime.Now.ToString("g", CultureInfo.CreateSpecificCulture("en-EN"))} // {severity} // [{ctx}] // {msg}");
            LogWriter.Flush();
        }

        public static void LogSolverActivity(LogSeverity severity, string msg, string solverName)
        {
            if (LogWriter is null) return;

            string ctx = solverName ?? "App";

            LogWriter.WriteLine($"{DateTime.Now.ToString("g", CultureInfo.CreateSpecificCulture("en-EN"))} // {severity} // [{ctx}] // {msg}");
            LogWriter.Flush();
        }

        public static void CloseLog()
        {
            LogWriter?.Close();
            LogWriter = null;
        }
    }
}
