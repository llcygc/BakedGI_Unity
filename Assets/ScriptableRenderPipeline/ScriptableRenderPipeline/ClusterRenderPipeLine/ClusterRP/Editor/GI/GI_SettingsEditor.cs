using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Viva.Rendering.RenderGraph;
using Viva.Rendering.RenderGraph.ClusterPipeline;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    [CanEditMultipleObjects]
    [CustomEditorForRenderPipeline(typeof(GI_Settings), typeof(ClusterRenderPipeLineAsset))]
    public class GI_SettingsEditor : Editor
    {
        SerializedProperty isDynamic;
        SerializedProperty probeDimension;
        SerializedProperty showDebug;

        private void OnEnable()
        {
            isDynamic = serializedObject.FindProperty("IsDynamic");
            probeDimension = serializedObject.FindProperty("ProbeDimensions");
            showDebug = serializedObject.FindProperty("ShowDebug");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(showDebug);
            if (EditorGUI.EndChangeCheck())
            {

            }

            EditorGUILayout.PropertyField(isDynamic);
            EditorGUILayout.PropertyField(probeDimension);

            if (GUILayout.Button("Update Probes"))
            {
                (serializedObject.targetObject as GI_Settings).AllocateProbes();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
