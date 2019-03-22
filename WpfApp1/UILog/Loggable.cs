using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.UILog
{
    public abstract class Loggable
    {
        private TraceSource _trace;
        public Loggable(TraceSource namedTraceSource)
        {
            Name = namedTraceSource.Name;
            _trace = namedTraceSource;
            //Trace.Refresh();
        }

        public virtual string Name { get; }

#region Log
        // Called by DataContext (DC...) classes
        public void LogVerbose(string format, params object[] args) => LogVerbose(String.Format(format, args));
        public void LogVerbose(string message) => _trace.TraceEvent(TraceEventType.Verbose, 0, message);
        public void LogInfo(string format, params object[] args) => LogInfo(String.Format(format, args));
        public void LogInfo(string message) => _trace.TraceInformation(message);
        public void LogWarning(string format, params object[] args) => LogWarning(String.Format(format, args));
        public void LogWarning(string message) => _trace.TraceEvent(TraceEventType.Warning, 0, message);
        public void LogError(string format, params object[] args) => LogError(String.Format(format, args));
        public void LogError(string message) => _trace.TraceEvent(TraceEventType.Error, 0, message);
#endregion

    }
}
