using System.Collections.Generic;
using Mono.Cecil;

namespace GK2ModInstaller.Patcher
{
    public static class WorkshopAutoLoaderPatcher
    {
        public static IEnumerable<string> TargetDLLs { get { yield return "Assembly-CSharp.dll"; } }

        public static void Patch(AssemblyDefinition assembly)
        {
            Bootstrap.Run();
        }
    }
}
