using System;
using System.Collections.Generic;
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

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for UCToolDebugUnitSession.xaml
    /// </summary>
    public partial class UCToolDebugUnitSession : UserControl
    {
        private DCToolDebugUnitSession Dc { get => (DCToolDebugUnitSession)DataContext; }

        public UCToolDebugUnitSession()
        {
            InitializeComponent();
        }

        private void btOpen_Click(object sender, RoutedEventArgs e)
        {
            Dc.OpenAsync();
        }

        private void btClose_Click(object sender, RoutedEventArgs e)
        {
            Dc.CloseAsync();
        }
    }
}
