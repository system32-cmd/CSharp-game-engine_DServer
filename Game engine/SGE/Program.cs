using System;
using System.Windows.Forms;

namespace SGE
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new EditorForm());
        }
    }
}
