using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace GK2ModInstaller.Tests
{
    public class EntryContractTests
    {
        private static Assembly LoadLoader()
        {
            // ищем сборку загрузчика рядом с тестовой (копируется project reference'ом решения)
            var dir = AppContext.BaseDirectory;
            var path = Path.Combine(dir, "GK2.WorkshopLoader.dll");
            Assert.True(File.Exists(path), "GK2.WorkshopLoader.dll не найден рядом с тестами: " + path);
            return Assembly.LoadFrom(path);
        }

        [Fact]
        public void Entry_run_has_the_frozen_signature()
        {
            var asm = LoadLoader();
            var type = asm.GetType("GK2ModInstaller.Loader.Entry", throwOnError: false);
            Assert.NotNull(type);
            var run = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(run);
            Assert.Equal(typeof(void), run.ReturnType);
            var pars = run.GetParameters();
            Assert.Equal(new[] { "bepInExRoot", "gameRoot", "workshopRoot", "acfPath", "source" }, pars.Select(p => p.Name).ToArray());
            Assert.All(pars, p => Assert.Equal(typeof(string), p.ParameterType));
        }

        [Fact]
        public void Loader_assembly_is_named_as_expected()
        {
            var asm = LoadLoader();
            Assert.Equal("GK2.WorkshopLoader", asm.GetName().Name);
        }
    }
}
