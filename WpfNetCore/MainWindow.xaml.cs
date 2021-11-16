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
using Video;

namespace WpfNetCore
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            //Environment.SetEnvironmentVariable("PATH", @"C:\gstreamer\1.0\x86\bin");
            Environment.SetEnvironmentVariable("PATH", @"C:\gstreamer_1_18\1.0\mingw_x86_64\bin");
            InitializeComponent();
        }
    }
}