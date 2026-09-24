using System;
using System.Windows.Forms;

namespace GK2ModInstaller.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length >= 2 && (args[0] == "--install" || args[0] == "--uninstall"))
            {
                Cli.Run(args[0], args[1]);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
