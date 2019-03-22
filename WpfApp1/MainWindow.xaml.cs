using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DCMain Dc { get => (DCMain)this.DataContext; }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void miRun_Click(object sender, RoutedEventArgs e)
        {
            ((DCToolWinUsb)(Dc.ToolChain.Tool1)).Test1_For_Error();
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            Trace.TraceInformation("MainWindow Initialized");
        }
    }
}
