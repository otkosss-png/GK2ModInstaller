using System.Collections.Generic;
using Mono.Cecil;

namespace GK2ModInstaller.Patcher
{
    public static class WorkshopAutoLoaderPatcher
    {
        // ВАЖНО: самый ранний доступный таргет — к моменту обработки Assembly-CSharp игра уже держит
        // свои Managed\*.dll (game-folder моды не скопировать/не откатить). BepInEx сам хукает CoreModule.
        public static IEnumerable<string> TargetDLLs { get { yield return "UnityEngine.CoreModule.dll"; } }

        public static void Patch(AssemblyDefinition assembly)
        {
            Bootstrap.Run();
        }
    }
}
