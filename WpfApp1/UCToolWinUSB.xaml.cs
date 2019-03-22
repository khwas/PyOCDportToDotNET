using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Interaction logic for UCToolWinUSB.xaml
    /// </summary>
    public partial class UCToolWinUSB : UserControl
    {
        private DCToolWinUsb Dc { get => (DCToolWinUsb)DataContext; }
        public UCToolWinUSB()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Prevent an error in design mode of MS Visual Studio
            if (DesignerProperties.GetIsInDesignMode(this)) return;
            // // Prevent thread collisions on observable collections updates
            // Dc.EnableCollectionSynchronization();
            // Main window handle is available at this time
            IntPtr mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
            // Use main window handle to hook USB notifications through Windows messages mechanism
            Dc.OnLoaded(mainWindowHandle);
        }
    }
}
