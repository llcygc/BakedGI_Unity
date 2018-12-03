using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class SceneSettingsProfile : ScriptableObject
    {
        public List<SceneSettingsComponent> components = new List<SceneSettingsComponent>();
    }
}
