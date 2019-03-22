using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WpfApp1
{
    public class LogEntry
    {
        public readonly long Ticks;
        public LogEntry(TraceEventCache eventCache)
        {
            Ticks = eventCache.Timestamp;
            dateTime = eventCache.DateTime.ToLocalTime();
        }
        private readonly DateTime dateTime;
        public DateTime DateTime { get => dateTime; }
        public Brush Category { get; set; }
        public string Message { get; set; }
    }
}

namespace WpfApp1.UILog
{

    public interface IDCLogView
    {
        string Name { get; }
        void AddLogEntry(LogEntry entry);
    }

    public class UITraceListener : TraceListener
    {
        public static readonly Dictionary<string, IDCLogView> LogViews = new Dictionary<string, IDCLogView>();
        private IDCLogView dc = null;

        public UITraceListener() : base()
        {
        }

        public UITraceListener(string name) : this()
        {
            Name = name;
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            Dc()?.AddLogEntry(new LogEntry(eventCache)
            {
                Category = categoryBrushes[eventType],
                Message = message,
            });
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            if (args == null)
            {
                this.TraceEvent(eventCache, source, eventType, id, format);
            }
            else
            {
                this.TraceEvent(eventCache, source, eventType, id, String.Format(format, args));
            }
        }

        public override void Write(string message)
        {
            // dc.Log(message, categoryBrushes[TraceEventType.Error]); // Only Debug.Assert failures are expected here
        }

        public override void WriteLine(string message)
        {
            // dc.Log(message, categoryBrushes[TraceEventType.Error]); // Only Debug.Assert failures are expected here
        }

        private IDCLogView Dc()
        {
            if (dc == null)
            {
                dc = LogViews[Name];
            }
            return dc;
        }

        private static readonly ReadOnlyDictionary<TraceEventType, Brush> categoryBrushes = new ReadOnlyDictionary<TraceEventType, Brush>(new Dictionary<TraceEventType, Brush>()
        {
            { TraceEventType.Information, Brushes.LightGreen },
            { TraceEventType.Warning , Brushes.Yellow },
            { TraceEventType.Error, Brushes.OrangeRed },
            { TraceEventType.Critical, Brushes.OrangeRed },
            { TraceEventType.Resume, Brushes.White},
            { TraceEventType.Start, Brushes.White },
            { TraceEventType.Stop, Brushes.White },
            { TraceEventType.Suspend, Brushes.White },
            { TraceEventType.Transfer, Brushes.White },
            { TraceEventType.Verbose, Brushes.White },
        });
    }

}
