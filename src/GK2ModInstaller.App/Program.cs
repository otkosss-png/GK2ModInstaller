using System;
using System.Drawing;
using System.Drawing.Imaging;
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
            if (args.Length >= 2 && args[0] == "--screenshot")
            {
                Screenshot(args[1]);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static void Screenshot(string path)
        {
            var form = new MainForm { Location = new Point(-4000, -4000), ShowInTaskbar = false };
            form.Show();
            Application.DoEvents();
            using (var bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                bmp.Save(path, ImageFormat.Png);
            }
            form.Close();
        }
    }
}
