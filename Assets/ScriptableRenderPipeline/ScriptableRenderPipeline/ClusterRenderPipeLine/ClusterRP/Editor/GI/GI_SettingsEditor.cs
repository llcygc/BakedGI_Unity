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
        SerializedProperty showDebug;
        SerializedProperty nearPlane;
        SerializedProperty farPlane;

        SerializedProperty probeDimension;

        private void OnEnable()
        {
            probeDimension = serializedObject.FindProperty("ProbeDimensions");

            isDynamic = serializedObject.FindProperty("IsDynamic");
            showDebug = serializedObject.FindProperty("ShowDebug");
            nearPlane = serializedObject.FindProperty("NearPlane");
            farPlane = serializedObject.FindProperty("FarPlane");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            {
                EditorGUILayout.PropertyField(isDynamic);
                EditorGUILayout.PropertyField(showDebug);
                EditorGUILayout.PropertyField(nearPlane);
                EditorGUILayout.PropertyField(farPlane);
            }
            if (EditorGUI.EndChangeCheck())
            {
                (serializedObject.targetObject as GI_Settings).UpdateProbeSettings();
            }

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(probeDimension);
            if (GUILayout.Button("Update Probes"))
            {
                (serializedObject.targetObject as GI_Settings).AllocateProbes();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
