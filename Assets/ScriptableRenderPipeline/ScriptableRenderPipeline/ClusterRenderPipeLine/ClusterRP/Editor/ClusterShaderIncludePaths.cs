using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    static class ClusterIncludePaths
    {
        [ShaderIncludePath]
        public static string[] GetPaths()
        {
            var srpMarker = Directory.GetFiles(Application.dataPath, "SRPMARKER", SearchOption.AllDirectories).FirstOrDefault();
            var paths = new string[srpMarker == null ? 1 : 2];
            var index = 0;
            if (srpMarker != null)
            {
                var rootPath = Directory.GetParent(srpMarker).ToString();
                paths[index] = Path.Combine(rootPath, "ScriptableRenderPipeline/ClusterRenderPipeline");
                index++;
            }
            paths[index] = Path.GetFullPath("Packages/com.unity.render-pipelines.cluster-pipeline");
            return paths;
        }
    }
}
