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
        private readonly TextBox _gameDir = new TextBox { Width = 420, ReadOnly = true };
        private readonly Label _status = new Label { AutoSize = true };
        private readonly CheckBox _bepinex = new CheckBox { Text = "BepInEx 5.4.23.5", Checked = true, AutoSize = true };
        private readonly CheckBox _framework = new CheckBox { Text = "GK2 Mod Framework", Checked = true, AutoSize = true };
        private readonly CheckBox _backup = new CheckBox { Text = "Бэкап существующего BepInEx", AutoSize = true };
        private readonly CheckBox _autoLoader = new CheckBox { Text = "Автозагрузка Workshop-модов", Checked = true, AutoSize = true };
        private readonly TextBox _log = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Height = 160, Width = 520 };
        private readonly ProgressBar _progress = new ProgressBar { Style = ProgressBarStyle.Marquee, Visible = false, Width = 520 };

        public MainForm()
        {
            Text = "Graveyard Keeper 2 — установка модов";
            Width = 560; Height = 420; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

            var browse = new Button { Text = "Обзор…", Width = 80 };
            browse.Click += (s, e) => Browse();

            var install = new Button { Text = "Установить", Width = 140, Height = 32 };
            install.Click += (s, e) => DoInstall();

            var uninstall = new Button { Text = "Удалить BepInEx", Width = 140, Height = 32 };
            uninstall.Click += (s, e) => DoUninstall();

            var l1 = new Label { Text = "Папка игры:", AutoSize = true };
            var row = new FlowLayoutPanel { AutoSize = true };
            row.Controls.AddRange(new Control[] { _gameDir, browse, _status });
            var checks = new FlowLayoutPanel { AutoSize = true };
            checks.Controls.AddRange(new Control[] { _bepinex, _framework, _autoLoader, _backup });
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
            _status.Text = ok ? "✓ папка игры найдена" : "✗ укажите папку игры";
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
            if (!GameLocator.ValidateGameDir(dir)) { MessageBox.Show(this, "Неверная папка игры."); return; }
            if (BepInExInstaller.IsGameRunning()) { MessageBox.Show(this, "Закройте игру перед установкой."); return; }

            try
            {
                _progress.Visible = true; AppendLog("Установка…");
                using (var bep = _bepinex.Checked ? OpenResource("BepInEx_win_x64_5.4.23.5.zip") : null)
                using (var fw = _framework.Checked ? OpenResource("GK2.Framework.zip") : null)
                using (var patcher = _autoLoader.Checked ? OpenResource("GK2.WorkshopAutoLoader.dll") : null)
                    BepInExInstaller.Install(dir, bep, fw, patcher, _backup.Checked, AppendLog);
                var problems = BepInExInstaller.Verify(dir, _autoLoader.Checked);
                AppendLog(problems.Count == 0 ? "Готово. Запустите игру 1 раз — появится меню Mods." : "Проблемы: " + string.Join(", ", problems));
            }
            catch (Exception ex) { AppendLog("Ошибка: " + ex.Message); }
            finally { _progress.Visible = false; }
        }

        private void DoUninstall()
        {
            if (MessageBox.Show(this, "Удалить BepInEx и фреймворк?", "Подтверждение", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { BepInExInstaller.Uninstall(_gameDir.Text, AppendLog); AppendLog("Удалено."); }
            catch (Exception ex) { AppendLog("Ошибка: " + ex.Message); }
        }
    }
}
