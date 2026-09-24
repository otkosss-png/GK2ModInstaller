using System;
using System.Windows.Forms;

namespace GK2ModInstaller.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MessageBox.Show("scaffold");
        }
    }
}
