using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WpfApp1.UILog;

namespace WpfApp1
{
    /// <summary>
    /// Base class for all Data Context classes
    /// - WPF compliant notifications
    /// - Actions Queue
    /// - Logging
    /// </summary>
    public abstract class DCBase : Loggable, IDCLogView, INotifyPropertyChanged
    {
        #region Plain parts: Fields, Constructor, Readonly Properties
        public const string LastErrorEmpty = "No Error";
        private string _lastError = LastErrorEmpty;
        private int _pendingActionsCounter = 0;
        private ObservableCollection<LogEntry> _logEntries = new ObservableCollection<LogEntry>();

        public DCBase(TraceSource namedTraceSource):base(namedTraceSource)
        {
            // Register self as named IDCLogView
            // so the trace listener(s) could store LogEntries into collection
            UITraceListener.LogViews.Add(Name, this as IDCLogView);
            LogVerbose("Constructor {0} (Name={1})", this.GetType().Name, Name);
        }

        public ObservableCollection<LogEntry> LogEntries { get => _logEntries; }
        #endregion

        #region WPF Client Notifications
        public event PropertyChangedEventHandler PropertyChanged;
        public void InvalidateProperty(string propertyName, bool sync = true)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                if (sync)
                {
                    handler(this, new PropertyChangedEventArgs(propertyName));
                }
                else
                {
                    handler.BeginInvoke(this, new PropertyChangedEventArgs(propertyName), null, null);
                }
            }
        }
        internal abstract void OnLastErrorChanged();
        internal abstract void OnPendingActionCounterChanged();
        #endregion

        #region Volatile Properties
        public string LastError
        {
            get => _lastError ?? LastErrorEmpty;
            private set
            {
                _lastError = value;
                InvalidateProperty(nameof(LastError));
                OnLastErrorChanged();
            }
        }
        #endregion

        #region Actions Queue
        internal int PendingActionsCounter { get => _pendingActionsCounter; }

        private int IncrementPendingActionsCounter()
        {
            int result = Interlocked.Increment(ref _pendingActionsCounter);
            OnPendingActionCounterChanged();
            return result;
        }
        private int DecrementPendingActionsCounter()
        {
            int result = Interlocked.Decrement(ref _pendingActionsCounter);
            OnPendingActionCounterChanged();
            return result;
        }

        internal void PostAction(Action action)
        {
            ThreadPool.QueueUserWorkItem((Object state) =>
            {
                IncrementPendingActionsCounter();
                try
                {
                    LastError = null;
                    action();
                }
                catch (Exception e)
                {
                    string error = String.Format("{0}: {1}\r\n{2}", e.GetType().Name, e.Message, e.StackTrace);
                    LogError(error);
                    LastError = error;
                }
                finally
                {
                    DecrementPendingActionsCounter();
                }
            });
        }
        #endregion

        #region Log
        // Called by Trace Listener
        public void AddLogEntry(LogEntry entry)
        {
            ThreadPool.QueueUserWorkItem((Object state) =>
            {
                // Keep no more than 100 last entries
                lock (LogEntries)
                {
                    while (LogEntries.Count > 100) LogEntries.RemoveAt(0);
                }
                // The log entries can arrive out of order. Use Timestamp values to keep correct visual order
                // Instead of using CollectionView, simply search suitable insert point near the end of collection
                // Intuitively, this approach is more efficient than CollectionView, but was never actually tested to compare speed
                lock (LogEntries)
                {
                    int index = LogEntries.Count;
                    while (index > 0)
                    {
                        index--;
                        if (LogEntries.ElementAt(index).Ticks < entry.Ticks)
                        {
                            // found entry with earlier timestamp, insert after it
                            LogEntries.Insert(index + 1, entry);
                            return;
                        }
                    }
                    LogEntries.Add(entry);
                }
            });
        }
        #endregion
    }
}
