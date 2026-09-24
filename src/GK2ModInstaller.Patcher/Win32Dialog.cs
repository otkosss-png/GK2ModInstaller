using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using GK2ModInstaller.Core;

namespace GK2ModInstaller.Patcher
{
    // Диалог через user32.MessageBoxW: WinForms в игре нет, а P/Invoke работает.
    public sealed class Win32Dialog : IDialog
    {
        private const uint MB_YESNOCANCEL = 0x00000003;
        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONWARNING = 0x00000030;
        private const uint MB_ICONINFORMATION = 0x00000040;
        private const uint MB_TOPMOST = 0x00040000;
        private const uint MB_SETFOREGROUND = 0x00010000;
        private const int IDYES = 6;
        private const int IDNO = 7;
        private const string Caption = "GK2 Workshop Loader";

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        public ConsentAnswer Ask(ModPrompt prompt)
        {
            string header = prompt.IsUpdate ? "Обновление мода из Workshop" : "Новый мод из Workshop";
            int r = MessageBoxW(IntPtr.Zero, prompt.Text(header), Caption,
                MB_YESNOCANCEL | MB_ICONWARNING | MB_TOPMOST | MB_SETFOREGROUND);
            if (r == IDYES) return ConsentAnswer.Approve;
            if (r == IDNO) return ConsentAnswer.Deny;
            return ConsentAnswer.Later;
        }

        public BulkAnswer AskBulkTrust(IReadOnlyList<ModPrompt> mods)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Найдено " + mods.Count + " мод(ов), установленных прежней версией автозагрузчика — без вашего согласия.");
            sb.AppendLine("Они уже стоят в BepInEx\\plugins\\_Workshop и работают. Доверять им всем?");
            sb.AppendLine();
            foreach (var m in mods) sb.AppendLine("• " + m.Title + " (id " + m.Id + ")");
            sb.AppendLine();
            sb.AppendLine("Yes — доверять всем · No — спросить про каждый отдельно · Cancel — оставить как есть и спросить позже");

            int r = MessageBoxW(IntPtr.Zero, sb.ToString(), Caption,
                MB_YESNOCANCEL | MB_ICONWARNING | MB_TOPMOST | MB_SETFOREGROUND);
            if (r == IDYES) return BulkAnswer.All;
            if (r == IDNO) return BulkAnswer.AskEach;
            return BulkAnswer.Later;
        }

        public void Warn(string text)
        {
            MessageBoxW(IntPtr.Zero, text, Caption, MB_OK | MB_ICONINFORMATION | MB_TOPMOST | MB_SETFOREGROUND);
        }
    }
}
