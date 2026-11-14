#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OperatorConsole.Editor
{
    /// <summary>
    /// OCBinding resolves members by name through reflection, which the IL2CPP linker can't see — so it emits a
    /// link.xml preserving every bound member found in built scenes and in project prefabs.
    /// </summary>
    internal sealed class OCBindingLinker : IPreprocessBuildWithReport, IProcessSceneWithReport, IUnityLinkerProcessor
    {
        // Keyed by assembly name, then by link.xml type name.
        private static readonly Dictionary<string, Dictionary<string, HashSet<string>>> s_preserved = new();

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => s_preserved.Clear();

        // Scenes are only loaded during the build here; the linker step runs after them, so it can't open them itself.
        public void OnProcessScene(Scene scene, BuildReport? report)
        {
            if (report == null) return;
            foreach (var root in scene.GetRootGameObjects())
                Collect(root.GetComponentsInChildren<OCBinding>(true));
        }

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null) Collect(prefab.GetComponentsInChildren<OCBinding>(true));
            }

            var path = Path.Combine(data.inputDirectory, "OCBindingLink.xml");
            File.WriteAllText(path, BuildLinkXml());
            return path;
        }

        private static void Collect(IEnumerable<OCBinding> bindings)
        {
            foreach (var binding in bindings)
            foreach (var link in binding.Links)
            {
                var target = link.Target;
                if (target == null) continue;
                if (!OCBindingMembers.TryGetPreservedMember(link.Member, out var name, out var isField)) continue;

                var type = target.GetType();
                var element = $"<{(isField ? "field" : "method")} name=\"{SecurityElement.Escape(name)}\"/>";

                var assembly = type.Assembly.GetName().Name;
                if (!s_preserved.TryGetValue(assembly, out var types))
                    s_preserved[assembly] = types = new Dictionary<string, HashSet<string>>();
                var typeName = type.FullName!.Replace('+', '/');
                if (!types.TryGetValue(typeName, out var members))
                    types[typeName] = members = new HashSet<string>();
                members.Add(element);
            }
        }

        private static string BuildLinkXml()
        {
            var sb = new StringBuilder("<linker>\n");
            foreach (var (assembly, types) in s_preserved.OrderBy(a => a.Key))
            {
                sb.Append($"  <assembly fullname=\"{SecurityElement.Escape(assembly)}\">\n");
                foreach (var (type, members) in types.OrderBy(t => t.Key))
                {
                    sb.Append($"    <type fullname=\"{SecurityElement.Escape(type)}\">\n");
                    foreach (var m in members.OrderBy(m => m)) sb.Append($"      {m}\n");
                    sb.Append("    </type>\n");
                }
                sb.Append("  </assembly>\n");
            }
            return sb.Append("</linker>\n").ToString();
        }
    }
}
