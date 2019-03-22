using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(Control), new FrameworkPropertyMetadata(true));
        }
    }
}
