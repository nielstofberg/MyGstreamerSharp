using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GstreamerSharp_1_18
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Environment.SetEnvironmentVariable("PATH", @"C:\gstreamer_1_18\1.0\msvc_x86_64\bin");
            //Environment.SetEnvironmentVariable("PATH", @"C:\gstreamer\1.0\x86\bin");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
