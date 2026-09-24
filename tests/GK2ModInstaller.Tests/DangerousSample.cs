using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;

namespace GK2ModInstaller.Tests
{
    // Эти методы никогда не вызываются — они нужны только как IL для проверки CodeScan.
    internal static class DangerousSample
    {
        public static void Touch()
        {
            var client = new HttpClient();
            var start = new ProcessStartInfo("cmd.exe");
            File.Delete("some.tmp");
            var asm = Assembly.Load("Nothing");
            System.Console.WriteLine(client != null && start != null && asm != null);
        }
    }
}
