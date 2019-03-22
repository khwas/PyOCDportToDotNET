using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WpfApp1.UILog;

namespace WpfApp1
{

    public interface ITool
    {
    }

    /// <summary>
    /// Data Context for User Control
    /// </summary>
    public abstract class DCTool : DCBase, INotifyPropertyChanged
    {
        public DCTool(TraceSource namedTraceSource) : base(namedTraceSource)
        {
        }

        #region WPF Client Notifications
        internal override void OnLastErrorChanged()
        {
            InvalidateProperty(nameof(RunStateIndicator));
            InvalidateProperty(nameof(VisibilityIndicator));
        }

        internal override void OnPendingActionCounterChanged()
        {
            InvalidateProperty(nameof(RunStateIndicator));
            InvalidateProperty(nameof(VisibilityIndicator));
        }

        internal virtual void OnToolChanged()
        {
            InvalidateProperty(nameof(RunStateIndicator));
            InvalidateProperty(nameof(VisibilityIndicator));
        }
        #endregion

        #region Run State
        internal enum ERunState
        {
            Disabled,  // Gray
            Running,   // White
            Complete,  // Green
            Failed,    // Red
        }

        private ERunState GetRunState()
        {
            if (!DCBase.LastErrorEmpty.Equals(LastError)) return ERunState.Failed;
            if (PendingActionsCounter > 0) return ERunState.Running;
            return (Tool == null) ? ERunState.Disabled : ERunState.Complete;
        }

        private static ReadOnlyDictionary<ERunState, Brush> runStateIndicator = new ReadOnlyDictionary<ERunState, Brush>(new Dictionary<ERunState, Brush>
        {
            { ERunState.Disabled, Brushes.LightGray },
            { ERunState.Running, Brushes.WhiteSmoke },
            { ERunState.Complete, Brushes.LightGreen},
            { ERunState.Failed, Brushes.OrangeRed },
        });

        public Brush RunStateIndicator
        {
            get => runStateIndicator[GetRunState()];
        }
        #endregion

        private ITool tool = null;
        public ITool Tool
        {
            get => tool;
            internal set
            {
                tool = value;
                OnToolChanged();
            }
        }

        #region Tool Window Visibility
        private bool isVisible = false;
        public Visibility IsVisible
        {
            get => isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
        public Brush VisibilityIndicator
        {
            get => isVisible ? RunStateIndicator : Brushes.Transparent;
        }
        public Brush ButtonIconFill
        {
            get => isVisible ? Brushes.Black : Brushes.DarkGray;
        }
        public void ToggleVisibility()
        {
            isVisible = !isVisible;
            InvalidateProperty(nameof(IsVisible));
            InvalidateProperty(nameof(VisibilityIndicator));
            InvalidateProperty(nameof(ButtonIconFill));
        }
        #endregion

        public abstract string About { get; }
    }
}
