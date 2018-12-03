using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Experimental.Rendering;
using Viva.Rendering.RenderGraph.ClusterPipeline;
using Viva.Rendering.RenderGraph;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    using UnityObject = UnityEngine.Object;

    public class ClusterRenderPipelineMenuItems
    {
        [MenuItem("ClusterRenderPipeline/Add \"Additional Light-shadow Data\" (if not present)")]
        static void AddAdditionalLightData()
        {
            var lights = UnityObject.FindObjectsOfType(typeof(Light)) as Light[];

            foreach (var light in lights)
            {
                bool dirty = false;
                // Do not add a component if there already is one.
                if (light.GetComponent<ClusterAdditionalLightData>() == null)
                {
                    light.gameObject.AddComponent<ClusterAdditionalLightData>();
                    dirty = true;
                }

                if (light.GetComponent<AdditionalShadowData>() == null)
                {
                    light.gameObject.AddComponent<AdditionalShadowData>();
                    dirty = true;
                }

                if (light.GetComponent<CachedShadowData>() == null)
                {
                    light.gameObject.AddComponent<CachedShadowData>();
                    dirty = true;
                }

                if (dirty)
                    EditorUtility.SetDirty(light.gameObject);
            }
        }

        public static List<T> FindObjectsOfTypeAll<T>()
        {
            List<T> results = new List<T>();

            for(int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if(s.isLoaded)
                {
                    var allGameObjects = s.GetRootGameObjects();
                    for(int j = 0; j < allGameObjects.Length; j++)
                    {
                        var go = allGameObjects[j];
                        results.AddRange(go.GetComponentsInChildren<T>(true));
                    }
                }
            }

            return results;
        }

        [MenuItem("ClusterRenderPipeline/Add \"Additional Camera Data\" (if not present)")]
        static void AddAdditionalCameraData()
        {
            //var cameras = UnityObject.FindObjectsOfType(typeof(Camera)) as Camera[];

            //foreach (var camera in cameras)
            //{
            //    if (camera.GetComponent<ClusterAdditionalCameraData>() != null)
            //        UnityObject.DestroyImmediate(camera.GetComponent<ClusterAdditionalCameraData>());
            //    // Do not add a component if there already is one.
            //    if (camera.GetComponent<AdditionalCameraData>() == null)
            //        camera.gameObject.AddComponent<AdditionalCameraData>();
            //}

            var cameras = FindObjectsOfTypeAll<Camera>();

            foreach(var camera in cameras)
            {
                bool dirty = false;
                if (camera.GetComponent<ClusterAdditionalCameraData>() != null)
                {
                    UnityObject.DestroyImmediate(camera.GetComponent<ClusterAdditionalCameraData>());
                    dirty = true;
                }
                // Do not add a component if there already is one.
                if (camera.GetComponent<AdditionalCameraData>() == null)
                {
                    camera.gameObject.AddComponent<AdditionalCameraData>();
                    dirty = true;
                }

                if(dirty)
                    EditorUtility.SetDirty(camera.gameObject);
            }
        }
                
        //// This script is a helper for the artists to re-synchronize all layered materials
        //[MenuItem("ClusterRenderPipeline/Synchronize all Layered materials")]
        //static void SynchronizeAllLayeredMaterial()
        //{
        //    var materials = Resources.FindObjectsOfTypeAll<Material>();

        //    foreach (var mat in materials)
        //    {
        //        if (mat.shader.name == "HDRenderPipeline/LayeredLit" || mat.shader.name == "HDRenderPipeline/LayeredLitTessellation")
        //        {
        //            LayeredLitGUI.SynchronizeAllLayers(mat);
        //            EditorUtility.SetDirty(mat);
        //        }
        //    }
        //}

        // The goal of this script is to help maintenance of data that have already been produced but need to update to the latest shader code change.
        // In case the shader code have change and the inspector have been update with new kind of keywords we need to regenerate the set of keywords use by the material.
        // This script will remove all keyword of a material and trigger the inspector that will re-setup all the used keywords.
        // It require that the inspector of the material have a static function call that update all keyword based on material properties.
        //[MenuItem("HDRenderPipeline/Test/Reset all materials keywords")]
        //static void ResetAllMaterialKeywords()
        //{
        //    try
        //    {
        //        var materials = Resources.FindObjectsOfTypeAll<Material>();

        //        for (int i = 0, length = materials.Length; i < length; i++)
        //        {
        //            EditorUtility.DisplayProgressBar(
        //                "Setup materials Keywords...",
        //                string.Format("{0} / {1} materials cleaned.", i, length),
        //                i / (float)(length - 1));

        //            ClusterEditorUtils.ResetMaterialKeywords(materials[i]);
        //        }
        //    }
        //    finally
        //    {
        //        EditorUtility.ClearProgressBar();
        //    }
        //}

        //[MenuItem("ClusterRenderPipeline/Test/Reset all materials keywords in project")]
        //static void ResetAllMaterialKeywordsInProject()
        //{
        //    try
        //    {
        //        var matIds = AssetDatabase.FindAssets("t:Material");

        //        for (int i = 0, length = matIds.Length; i < length; i++)
        //        {
        //            var path = AssetDatabase.GUIDToAssetPath(matIds[i]);
        //            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        //            EditorUtility.DisplayProgressBar(
        //                "Setup materials Keywords...",
        //                string.Format("{0} / {1} materials cleaned.", i, length),
        //                i / (float)(length - 1));

        //            HDEditorUtils.ResetMaterialKeywords(mat);
        //        }
        //    }
        //    finally
        //    {
        //        EditorUtility.ClearProgressBar();
        //    }
        //}

        static void CheckOutFile(bool VSCEnabled, Material mat)
        {
            if (VSCEnabled)
            {
                UnityEditor.VersionControl.Task task = UnityEditor.VersionControl.Provider.Checkout(mat, UnityEditor.VersionControl.CheckoutMode.Both);

                if (!task.success)
                {
                    Debug.Log(task.text + " " + task.resultCode);
                }
            }
        }

        //[MenuItem("HDRenderPipeline/Update/Update SSS profile indices")]
        //static void UpdateSSSProfileIndices()
        //{
        //    try
        //    {
        //        var matIds = AssetDatabase.FindAssets("t:Material");

        //        for (int i = 0, length = matIds.Length; i < length; i++)
        //        {
        //            var path = AssetDatabase.GUIDToAssetPath(matIds[i]);
        //            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        //            EditorUtility.DisplayProgressBar(
        //                "Setup materials Keywords...",
        //                string.Format("{0} / {1} materials SSS updated.", i, length),
        //                i / (float)(length - 1));

        //            bool VSCEnabled = (UnityEditor.VersionControl.Provider.enabled && UnityEditor.VersionControl.Provider.isActive);

        //            if (mat.shader.name == "HDRenderPipeline/LitTessellation" ||
        //                mat.shader.name == "HDRenderPipeline/Lit")
        //            {
        //                float fvalue = mat.GetFloat("_MaterialID");
        //                if (fvalue == 0.0) // SSS
        //                {
        //                    CheckOutFile(VSCEnabled, mat);
        //                    int ivalue = mat.GetInt("_SubsurfaceProfile");
        //                    if (ivalue == 15)
        //                    {
        //                        mat.SetInt("_SubsurfaceProfile", 0);
        //                    }
        //                    else
        //                    {
        //                        mat.SetInt("_SubsurfaceProfile", ivalue + 1);
        //                    }

        //                    EditorUtility.SetDirty(mat);
        //                }
        //            }
        //            else if (mat.shader.name == "HDRenderPipeline/LayeredLit" ||
        //                        mat.shader.name == "HDRenderPipeline/LayeredLitTessellation")
        //            {
        //                float fvalue = mat.GetFloat("_MaterialID");
        //                if (fvalue == 0.0) // SSS
        //                {
        //                    CheckOutFile(VSCEnabled, mat);
        //                    int numLayer = (int)mat.GetFloat("_LayerCount");

        //                    for (int x = 0; x < numLayer; ++x)
        //                    {
        //                        int ivalue = mat.GetInt("_SubsurfaceProfile" + x);
        //                        if (ivalue == 15)
        //                        {
        //                            mat.SetInt("_SubsurfaceProfile" + x, 0);
        //                        }
        //                        else
        //                        {
        //                            mat.SetInt("_SubsurfaceProfile" + x, ivalue + 1);
        //                        }
        //                    }
        //                    EditorUtility.SetDirty(mat);
        //                }
        //            }
        //        }
        //    }
        //    finally
        //    {
        //        EditorUtility.ClearProgressBar();
        //    }
        //}

        //// Function used only to check performance of data with and without tessellation
        //[MenuItem("HDRenderPipeline/Test/Remove tessellation materials (not reversible)")]
        //static void RemoveTessellationMaterials()
        //{
        //    var materials = Resources.FindObjectsOfTypeAll<Material>();

        //    var litShader = Shader.Find("HDRenderPipeline/Lit");
        //    var layeredLitShader = Shader.Find("HDRenderPipeline/LayeredLit");

        //    foreach (var mat in materials)
        //    {
        //        if (mat.shader.name == "HDRenderPipeline/LitTessellation")
        //        {
        //            mat.shader = litShader;
        //            // We remove all keyword already present
        //            HDEditorUtils.RemoveMaterialKeywords(mat);
        //            LitGUI.SetupMaterialKeywordsAndPass(mat);
        //            EditorUtility.SetDirty(mat);
        //        }
        //        else if (mat.shader.name == "HDRenderPipeline/LayeredLitTessellation")
        //        {
        //            mat.shader = layeredLitShader;
        //            // We remove all keyword already present
        //            HDEditorUtils.RemoveMaterialKeywords(mat);
        //            LayeredLitGUI.SetupMaterialKeywordsAndPass(mat);
        //            EditorUtility.SetDirty(mat);
        //        }
        //    }
        //}

        //[MenuItem("HDRenderPipeline/Export Sky to Image")]
        //static void ExportSkyToImage()
        //{
        //    var renderpipeline = RenderPipelineManager.currentPipeline as ClusterRenderPipeline;
        //    if (renderpipeline == null)
        //    {
        //        Debug.LogError("HDRenderPipeline is not instantiated.");
        //        return;
        //    }

        //    var result = renderpipeline.ExportSkyToTexture();
        //    if (result == null)
        //        return;

        //    // Encode texture into PNG
        //    byte[] bytes = result.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
        //    UnityObject.DestroyImmediate(result);

        //    string assetPath = EditorUtility.SaveFilePanel("Export Sky", "Assets", "SkyExport", "exr");
        //    if (!string.IsNullOrEmpty(assetPath))
        //    {
        //        File.WriteAllBytes(assetPath, bytes);
        //        AssetDatabase.Refresh();
        //    }
        //}

        [MenuItem("GameObject/Cluster Render Pipeline/Scene Settings", false, 10)]
        static void CreateCustomGameObject(MenuCommand menuCommand)
        {
            //var sceneSettings = new GameObject("Scene Settings");
            //GameObjectUtility.SetParentAndAlign(sceneSettings, menuCommand.context as GameObject);
            //Undo.RegisterCreatedObjectUndo(sceneSettings, "Create " + sceneSettings.name);
            //Selection.activeObject = sceneSettings;
            //sceneSettings.AddComponent<SceneSettings>();

            var parent = menuCommand.context as GameObject;
            var scenesettings = CoreEditorUtils.CreateGameObject(parent, "Scene Settings");
            GameObjectUtility.SetParentAndAlign(scenesettings, menuCommand.context as GameObject);
            Selection.activeObject = scenesettings;

            var profile = VolumeProfileFactory.CreateVolumeProfile(scenesettings.scene, "Scene Settings");
            VolumeProfileFactory.CreateVolumeComponent<CommonSettings>(profile, true, false);

            var volume = scenesettings.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        [MenuItem("GameObject/Cluster Render Pipeline/Volume Effect")]
        static void CreateVolumeEffectObject(MenuCommand menuCommand)
        {
            var volumeEffect = new GameObject("Volume Effect");
            GameObjectUtility.SetParentAndAlign(volumeEffect, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(volumeEffect, "Create" + volumeEffect.name);
            Selection.activeObject = volumeEffect;
            volumeEffect.AddComponent<VolumeEffect>();
        }

        class DoCreateNewAsset<TAssetType> : ProjectWindowCallback.EndNameEditAction where TAssetType : ScriptableObject
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var newAsset  = CreateInstance<TAssetType>();
                newAsset.name = Path.GetFileName(pathName);
                AssetDatabase.CreateAsset(newAsset, pathName);
                ProjectWindowUtil.ShowCreatedAsset(newAsset);
            }
        }

        class DoCreateNewAssetCommonSettings : DoCreateNewAsset<CommonSettings> {}
        class DoCreateNewAssetHDRISkySettings : DoCreateNewAsset<HDRISkySettings> {}
        class DoCreateNewAssetVolumetricSettings : DoCreateNewAsset<VolumetricSettings> {}
        class DoCreateNewAssetSubsurfaceScatteringSettings : DoCreateNewAsset<SubsurfaceScatteringSettings> {}

        [MenuItem("Assets/Create/ClusterRenderPipeline/Common Settings", priority = 700)]
        static void MenuCreateCommonSettings()
        {
            var icon = EditorGUIUtility.FindTexture("ScriptableObject Icon");
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateNewAssetCommonSettings>(), "New CommonSettings.asset", icon, null);
        }

        [MenuItem("Assets/Create/ClusterRenderPipeline/Subsurface Scattering Settings", priority = 702)]
        static void MenuCreateSubsurfaceScatteringProfile()
        {
            var icon = EditorGUIUtility.FindTexture("ScriptableObject Icon");
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateNewAssetSubsurfaceScatteringSettings>(), "New SSS Settings.asset", icon, null);
        }

        [MenuItem("Assets/Create/ClusterRenderPipeline/HDRISky Settings", priority = 750)]
        static void MenuCreateHDRISkySettings()
        {
            var icon = EditorGUIUtility.FindTexture("ScriptableObject Icon");
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateNewAssetHDRISkySettings>(), "New HDRISkySettings.asset", icon, null);
        }

        [MenuItem("Assets/Create/ClusterRenderPipeline/Volumetric Settings", priority = 760)]
        static void MenuCreateVolumetricSettings()
        {
            var icon = EditorGUIUtility.FindTexture("ScriptableObject Icon");
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateNewAssetVolumetricSettings>(), "New VolumetricSettings.asset", icon, null);
        }
    }
}
