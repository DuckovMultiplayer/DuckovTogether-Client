using EscapeFromDuckovCoopMod.Utils.Logger.Core;
using EscapeFromDuckovCoopMod.Utils.Logger.LogHandlers;
using EscapeFromDuckovCoopMod.Utils.Logger.Logs;

namespace EscapeFromDuckovCoopMod.Utils.Logger.Tools
{
    /// <summary>
    /// 一个专门为本项目设计的 Logger 单例辅助类
    /// </summary>
    public class LoggerHelper
    {
        private static RelayLogHandler _relayLogHandler;
        
        private static readonly Lazy<LogHandlers.Logger> _instance = new Lazy<LogHandlers.Logger>(
            () =>
            {
                var logger = new LogHandlers.Logger();

                FileLogHandlerForConsoleMod.Init(logger);

                var asyncConsoleHandler = LogHandlerAsyncDecorator.CreateDecorator(
                    new ConsoleLogHandler()
                );
                logger.AddHandler(asyncConsoleHandler);
                
                _relayLogHandler = new RelayLogHandler();
                logger.AddHandler(_relayLogHandler);

                return logger;
            },
            LazyThreadSafetyMode.ExecutionAndPublication
        );

        public static LogHandlers.Logger Instance => _instance.Value;
        
        public static void EnableRelayLogging(string relayAddress, int relayPort, string roomId)
        {
            if (_relayLogHandler != null)
            {
                _relayLogHandler.Enable(relayAddress, relayPort, roomId);
            }
        }
        
        public static void DisableRelayLogging()
        {
            if (_relayLogHandler != null)
            {
                _relayLogHandler.Disable();
            }
        }

        // 替代掉 Debug.Log 之类的玩意
        public static void Log(string message)
        {
            Instance.Log(new Log(LogLevel.Info, message));
        }

        public static void LogWarning(string message)
        {
            Instance.Log(new Log(LogLevel.Warning, message));
        }

        public static void LogError(string message)
        {
            Instance.Log(new Log(LogLevel.Error, message));
        }

        public static void LogException(Exception exception)
        {
            Instance.Log(new Log(LogLevel.Error, exception.ToString()));
        }

        // 提供其他方法，以免懒得使用 Instance 调用
        public static void Log<TLog>(TLog log)
            where TLog : struct, ILog
        {
            Instance.Log(log);
        }

        // 与 Log 的扩展方法对齐
        public static void Log(LogLevel logLevel, string message)
        {
            Instance.Log(new Log(logLevel, message));
        }

        public static void LogInfo(string message)
        {
            Instance.Log(new Log(LogLevel.Info, message));
        }

        public static void LogTrace(string message)
        {
            Instance.Log(new Log(LogLevel.Trace, message));
        }

        public static void LogDebug(string message)
        {
            Instance.Log(new Log(LogLevel.Debug, message));
        }

        public static void LogFatal(string message)
        {
            Instance.Log(new Log(LogLevel.Fatal, message));
        }

        // 与 LabelLog 的扩展方法对齐
        public static void Log(LogLevel logLevel, string message, string label)
        {
            Instance.Log(new LabelLog(logLevel, message, label));
        }

        public static void Log(string message, string label)
        {
            Instance.Log(new LabelLog(LogLevel.Info, message, label));
        }

        public static void LogInfo(string message, string label)
        {
            Instance.Log(new LabelLog(LogLevel.Info, message, label));
        }

        public static void LogTrace(string message, string label)
        {
            Instance.Log(new LabelLog(LogLevel.Trace, message, label));
        }

        public static void LogDebug(string message, string label)
        {
            Instance.Log(new LabelLog(LogLevel.Debug, message, label));
        }

        public static void LogWarning(string message, string label)
        {
            Instance.Log(new LabelLog(LogLevel.Warning, message, label));
        }

        public static void LogError(string message, string label)
        {
            Instance.Log(new LabelLog(LogLevel.Error, message, label));
        }

        public static void LogFatal(string message, string label)
        {
            Instance.Log(new LabelLog(LogLevel.Fatal, message, label));
        }
    }
}
