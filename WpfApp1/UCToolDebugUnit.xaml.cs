using System;
using System.Collections.Generic;
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
    /// Interaction logic for UCToolDebugUnit.xaml
    /// </summary>
    public partial class UCToolDebugUnit : UserControl
    {

        private DCToolDebugUnit Dc { get => (DCToolDebugUnit)DataContext; }

        public UCToolDebugUnit()
        {
            InitializeComponent();
        }

        private void btConnect_Click(object sender, RoutedEventArgs e)
        {
            Dc.ConnectAsync();
        }

        private void btDisconnect_Click(object sender, RoutedEventArgs e)
        {
            Dc.DisconnectAsync();
        }

    }
}
