using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    [Serializable]
    public class SceneSettingsComponent : ScriptableObject
    {
        public bool active = true;
    }
}
