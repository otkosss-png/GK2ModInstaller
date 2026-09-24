using System;
using System.IO;
using System.Linq;
using GK2ModInstaller.Core;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class CodeScanTests
    {
        [Fact]
        public void Finds_network_process_filedelete_and_codeload_in_test_assembly()
        {
            var found = CodeScan.Scan(typeof(DangerousSample).Assembly.Location);
            var cats = found.Select(f => f.Category).Distinct().ToList();
            Assert.Contains(FindingCategory.Network, cats);
            Assert.Contains(FindingCategory.Process, cats);
            Assert.Contains(FindingCategory.FileDelete, cats);
            Assert.Contains(FindingCategory.CodeLoad, cats);
            Assert.All(found, f => Assert.False(string.IsNullOrEmpty(f.Detail)));
        }

        [Fact]
        public void Clean_assembly_produces_no_findings()
        {
            string path = Path.Combine(Path.GetTempPath(), "gk2clean_" + Guid.NewGuid().ToString("N") + ".dll");
            try
            {
                WriteCleanProbe(path);
                Assert.Empty(CodeScan.Scan(path));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        // Эталон «чистой» сборки собираем сами Cecil'ом: ни Core (там есть Process/File.Delete
        // в установщике), ни сама тестовая сборка (в ней DangerousSample) эталоном быть не могут.
        private static void WriteCleanProbe(string path)
        {
            var asm = Mono.Cecil.AssemblyDefinition.CreateAssembly(
                new Mono.Cecil.AssemblyNameDefinition("CleanProbe", new Version(1, 0, 0, 0)),
                "CleanProbe", Mono.Cecil.ModuleKind.Dll);
            var type = new Mono.Cecil.TypeDefinition("CleanProbe", "CleanSample",
                Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed);
            asm.MainModule.Types.Add(type);
            var method = new Mono.Cecil.MethodDefinition("Add",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                asm.MainModule.TypeSystem.Int32);
            method.Parameters.Add(new Mono.Cecil.ParameterDefinition("a", Mono.Cecil.ParameterAttributes.None, asm.MainModule.TypeSystem.Int32));
            method.Parameters.Add(new Mono.Cecil.ParameterDefinition("b", Mono.Cecil.ParameterAttributes.None, asm.MainModule.TypeSystem.Int32));
            method.Body = new Mono.Cecil.Cil.MethodBody(method);
            var il = method.Body.GetILProcessor();
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_1);
            il.Emit(Mono.Cecil.Cil.OpCodes.Add);
            il.Emit(Mono.Cecil.Cil.OpCodes.Ret);
            type.Methods.Add(method);
            asm.Write(path);
        }

        [Fact]
        public void Broken_dll_gives_no_findings_and_does_not_throw()
        {
            string path = Path.Combine(Path.GetTempPath(), "gk2scan_" + Guid.NewGuid().ToString("N") + ".dll");
            File.WriteAllText(path, "не сборка");
            try { Assert.Empty(CodeScan.Scan(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Missing_file_gives_no_findings()
        {
            Assert.Empty(CodeScan.Scan(Path.Combine(Path.GetTempPath(), "gk2scan_missing_" + Guid.NewGuid().ToString("N") + ".dll")));
            Assert.Empty(CodeScan.Scan(null));
        }
    }
}
