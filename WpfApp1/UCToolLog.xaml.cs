using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfApp1.UILog;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for UCToolLog.xaml
    /// </summary>
    public partial class UCToolLog : UserControl
    {
        private ObservableCollection<LogEntry> LogEntries { get => (ObservableCollection<LogEntry>)DataContext; }

        public UCToolLog()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Prevent an error in design mode of MS Visual Studio
            if (DesignerProperties.GetIsInDesignMode(this)) return;
            // To get rid of strong coupling when DataContext must be aware about bound window Dispatcher it is better to keep DataContext completely unaware about threading
            // All members in DataContext can be made plain value objects like Color etc.
            // Including the ObservableCollection object, which can be synchronized transparently since .NET 4.5
            // To support the safe concurrent updates and changes in ObservableCollection the BindingOperations has new mechanism named EnableCollectionSynchronization
            BindingOperations.EnableCollectionSynchronization(LogEntries, LogEntries);

            // Scroll to bottom on every change
            LogEntries.CollectionChanged += LogEntries_CollectionChanged;
        }

        private void LogEntries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            icLogEntries.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (VisualTreeHelper.GetChildrenCount(icLogEntries) > 0)
                {
                    ScrollViewer scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(icLogEntries, 0);
                    scrollViewer.ScrollToBottom();
                }
            }));
        }
    }
}
