using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using GK2ModInstaller.Core;
using Microsoft.Win32;

namespace GK2ModInstaller.App
{
    public sealed class MainForm : Form
    {
        private static string T(string key) => LoaderText.Get(key);

        private readonly TextBox _gameDir = new TextBox { Width = 420, ReadOnly = true };
        private readonly Label _status = new Label { AutoSize = true };
        private readonly CheckBox _bepinex = new CheckBox { Text = "BepInEx 5.4.23.5", Checked = true, AutoSize = true };
        private readonly CheckBox _framework = new CheckBox { Text = "GK2 Mod Framework", Checked = true, AutoSize = true };
        private readonly CheckBox _backup = new CheckBox { Text = T("InstBackup"), AutoSize = true };
        private readonly CheckBox _autoLoader = new CheckBox { Text = T("InstAutoLoader"), Checked = true, AutoSize = true };
        private readonly CheckBox _keepPlugins = new CheckBox { Text = T("InstKeepPlugins"), Checked = true, AutoSize = true };
        private readonly TextBox _log = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Height = 160, Width = 520 };
        private readonly ProgressBar _progress = new ProgressBar { Style = ProgressBarStyle.Marquee, Visible = false, Width = 520 };

        public MainForm()
        {
            Text = T("InstTitle");
            Width = 560; Height = 420; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

            var browse = new Button { Text = T("InstBrowse"), Width = 80 };
            browse.Click += (s, e) => Browse();

            var install = new Button { Text = T("InstInstall"), Width = 140, Height = 32 };
            install.Click += (s, e) => DoInstall();

            var uninstall = new Button { Text = T("InstUninstall"), Width = 140, Height = 32 };
            uninstall.Click += (s, e) => DoUninstall();

            var l1 = new Label { Text = T("InstGameFolder"), AutoSize = true };
            var row = new FlowLayoutPanel { AutoSize = true };
            row.Controls.AddRange(new Control[] { _gameDir, browse, _status });
            var checks = new FlowLayoutPanel { AutoSize = true };
            checks.Controls.AddRange(new Control[] { _bepinex, _framework, _autoLoader, _backup, _keepPlugins });
            var buttons = new FlowLayoutPanel { AutoSize = true };
            buttons.Controls.AddRange(new Control[] { install, uninstall });

            var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12), AutoScroll = true };
            stack.Controls.AddRange(new Control[] { l1, row, checks, buttons, _progress, _log });
            Controls.Add(stack);

            AutoDetect();
        }

        private void AutoDetect()
        {
            try
            {
                string steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                string found = GameLocator.FindGameInSteamRoot(steam);
                if (found != null) { _gameDir.Text = found; SetStatus(true); }
                else SetStatus(false);
            }
            catch { SetStatus(false); }
        }

        private void Browse()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _gameDir.Text = dlg.SelectedPath;
                    SetStatus(GameLocator.ValidateGameDir(dlg.SelectedPath));
                }
            }
        }

        private void SetStatus(bool ok)
        {
            _status.Text = ok ? T("InstFolderFound") : T("InstFolderMissing");
            _status.ForeColor = ok ? System.Drawing.Color.Green : System.Drawing.Color.Firebrick;
        }

        private void AppendLog(string s)
        {
            _log.AppendText(s + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        private Stream OpenResource(string logicalName)
        {
            var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName);
            if (s != null) return s;
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logicalName);
            return File.Exists(path) ? File.OpenRead(path) : null;
        }

        private void DoInstall()
        {
            string dir = _gameDir.Text;
            if (!GameLocator.ValidateGameDir(dir)) { MessageBox.Show(this, T("InstBadFolder")); return; }
            if (BepInExInstaller.IsGameRunning()) { MessageBox.Show(this, T("InstCloseGame")); return; }

            try
            {
                _progress.Visible = true; AppendLog(T("InstInstalling"));
                using (var bep = _bepinex.Checked ? OpenResource("BepInEx_win_x64_5.4.23.5.zip") : null)
                using (var fw = _framework.Checked ? OpenResource("GK2.Framework.zip") : null)
                using (var patcher = _autoLoader.Checked ? OpenResource("GK2.WorkshopAutoLoader.dll") : null)
                using (var loader = _autoLoader.Checked ? OpenResource("GK2.WorkshopLoader.dll") : null)
                    BepInExInstaller.Install(dir, bep, fw, patcher, loader, _backup.Checked, AppendLog);
                var problems = BepInExInstaller.Verify(dir, _autoLoader.Checked);
                AppendLog(problems.Count == 0 ? T("InstDone") : T("InstProblems") + string.Join(", ", problems));
            }
            catch (Exception ex) { AppendLog(T("InstError") + ex.Message); }
            finally { _progress.Visible = false; }
        }

        private void DoUninstall()
        {
            bool keep = _keepPlugins.Checked;
            string nl2 = Environment.NewLine + Environment.NewLine;
            string msg =
                T("InstUninstallQuestion") + nl2 +
                T("InstUninstallWhat") + nl2 +
                (keep ? T("InstUninstallKeep") : T("InstUninstallAll") + nl2 + T("InstUninstallTip")) +
                nl2 + T("InstContinue");

            var caption = keep ? T("InstCaptionKeep") : T("InstCaptionAll");
            if (MessageBox.Show(this, msg, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { BepInExInstaller.Uninstall(_gameDir.Text, AppendLog, keep); AppendLog(T("InstRemoved")); }
            catch (Exception ex) { AppendLog(T("InstError") + ex.Message); }
        }
    }
}
