using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Viva.Rendering.RenderGraph;
using UnityEngine.SceneManagement;
using UnityEngine.Experimental.Rendering;
using Viva.Rendering.RenderGraph.ClusterPipeline;

using UnityEditor.SceneManagement;

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
    [CanEditMultipleObjects]
    [VolumeComponentEditor(typeof(ClusterShadowSettings))]
    public class ClusterShadowSettingsEditor : VolumeComponentEditor
    {
        private class Styles
        {
            public readonly GUIContent maxShadowDistance = new GUIContent("Maximum shadow distance");
            public readonly GUIContent maxShadowCasterCount = new GUIContent("Maximum shadowcaster count", "Max visible dynamic shadowcaster count");
            public readonly GUIContent nearPlaneOffset = new GUIContent("Shadow near plane offset");
            public readonly GUIContent hasStaticShadow = new GUIContent("Has static shadow", "Is there any lights in current loaded scenes has static shadow map");
            public readonly GUIContent shadowmapRes = new GUIContent("Dynamic Shadowmap Resolution");
            public readonly GUIContent staticShadowmapRes = new GUIContent("Static Shadowmap Resolution");
            public readonly GUIContent staticShadowmap = new GUIContent("Static Shadowmap", "The actual static shadowmap texture");
            public readonly GUIContent renderStaticShadowmapButton = new GUIContent("Render Static Shadowmap");
        }

        private static Styles s_Styles = null;
        private static Styles styles
        {
            get
            {
                if (s_Styles == null)
                    s_Styles = new Styles();
                return s_Styles;
            }
        }

        private SerializedDataParameter m_maxShadowDistance;
        private SerializedDataParameter m_maxShadowCasters;
        private SerializedDataParameter m_shadowMapResolution;
        private SerializedDataParameter m_hasStaticShadow;
        private SerializedDataParameter m_staticShadowMapResolution;
        private SerializedDataParameter m_staticShadowmap;

        private List<CachedShadowData> allCachedShadows = new List<CachedShadowData>();
        private List<CachedShadowInfo> allCachedShadowInfos = new List<CachedShadowInfo>();
        private List<int> csdIndex = new List<int>();

        public override void OnEnable()
        {
            var o = new PropertyFetcher<ClusterShadowSettings>(serializedObject);

            m_maxShadowDistance = Unpack(o.Find(x => x.MaxShadowDistance));
            m_maxShadowCasters = Unpack(o.Find(x => x.MaxShadowCasters));
            m_shadowMapResolution = Unpack(o.Find(x => x.ShadowMapResolution));
            m_hasStaticShadow = Unpack(o.Find(x => x.HasStaticShadow));
            m_staticShadowMapResolution = Unpack(o.Find(x => x.StaticShadowMapResolution));
            m_staticShadowmap = Unpack(o.Find(x => x.staticShadowmap));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(m_maxShadowDistance, styles.maxShadowDistance);
            PropertyField(m_maxShadowCasters, styles.maxShadowCasterCount);
            PropertyField(m_shadowMapResolution, styles.shadowmapRes);

            PropertyField(m_hasStaticShadow, styles.hasStaticShadow);
            if (m_hasStaticShadow.value.boolValue)
            {
                EditorGUI.indentLevel++;
                if (HasStaticShadowLightInScene())
                {
                    PropertyField(m_staticShadowMapResolution, styles.staticShadowmapRes);
                    if (GUILayout.Button(styles.renderStaticShadowmapButton))
                    {
                        if (CollectStaticShadowmaps())
                            RenderStaticShadowmaps();
                        else
                            EditorGUILayout.HelpBox("Not enough space for all the static shadow lights. Please check the static shadow map resolution settings.", MessageType.Error);
                    }
                    PropertyField(m_staticShadowmap, styles.staticShadowmap);
                }
                else
                    EditorGUILayout.HelpBox("No lights with static shadow found. Pleas confirm that all lights have Additional Lights Data and the shadow type of light was set correctly.", MessageType.Warning);
                EditorGUI.indentLevel--;
            }
        }

        private bool HasStaticShadowLightInScene()
        {
            bool hasStaticShadowLightsInScene = false;
            allCachedShadows.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.isLoaded)
                {
                    var allGameObjects = s.GetRootGameObjects();
                    for (int j = 0; j < allGameObjects.Length; j++)
                    {
                        var go = allGameObjects[j];
                        allCachedShadows.AddRange(go.GetComponentsInChildren<CachedShadowData>());
                    }
                }
            }

            foreach (var csd in allCachedShadows)
            {
                Light l = csd.GetComponent<Light>();
                if (l != null && l.shadows != LightShadows.None && (csd.shadowUpdateType == ShadowUpdateType.Static || (l.type == LightType.Directional && csd.UseStaticShadowmapForLastCascade)))
                    hasStaticShadowLightsInScene = true;
            }

            return hasStaticShadowLightsInScene;
        }

        private bool CollectStaticShadowmaps()
        {
            int shadowWidth = m_staticShadowMapResolution.value.vector2IntValue.x;
            int shadowHeight = m_staticShadowMapResolution.value.vector2IntValue.y;

            allCachedShadowInfos.Clear();
            csdIndex.Clear();

            for (int i = 0; i < allCachedShadows.Count; i++)
            {
                var csd = allCachedShadows[i];
                Light l = csd.GetComponent<Light>();
                AdditionalShadowData asd = csd.GetComponent<AdditionalShadowData>();
                if (l != null && l.shadows != LightShadows.None && (csd.shadowUpdateType == ShadowUpdateType.Static || (l.type == LightType.Directional && csd.UseStaticShadowmapForLastCascade)))
                {
                    CachedShadowInfo info = new CachedShadowInfo();
                    uint staticShadowmapRes = l.type == LightType.Directional ? csd.StaticShadowResolution : (uint)asd.shadowResolution;
                    info.viewport = new Rect(0, 0, staticShadowmapRes, staticShadowmapRes);
                    allCachedShadowInfos.Add(info);
                    csdIndex.Add(i);
                }
            }

            return Layout(shadowWidth, shadowHeight);
        }

        bool Layout(int shadowWidth, int shadowHeight)
        {
            allCachedShadowInfos.Sort();

            float curx = 0, cury = 0, curh = 0, xmax = shadowWidth, ymax = shadowHeight;

            for (int i = 0; i < allCachedShadowInfos.Count; i++)
            {
                // shadow atlas layouting
                CachedShadowInfo info = allCachedShadowInfos[i];
                Rect vp = info.viewport;
                curh = curh >= vp.height ? curh : vp.height;

                if (curx + vp.width > xmax)
                {
                    curx = 0;
                    cury += curh;
                    curh = vp.width;
                }
                if (curx + vp.width > xmax || cury + curh > ymax)
                {
                    return false;
                }
                vp.x = curx;
                vp.y = cury;
                info.viewport = vp;
                allCachedShadowInfos[i] = info;
                curx += vp.width;
            }

            return true;
        }

        private void RenderStaticShadowmaps()
        {
            int shadowWidth = m_staticShadowMapResolution.value.vector2IntValue.x;
            int shadowHeight = m_staticShadowMapResolution.value.vector2IntValue.y;

            RenderTexture staticShadowmap = new RenderTexture(shadowWidth, shadowHeight, 32, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            //staticShadowmap.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            staticShadowmap.dimension = TextureDimension.Tex2D;
            staticShadowmap.filterMode = FilterMode.Bilinear;
            staticShadowmap.useMipMap = false;
            staticShadowmap.name = "StaticShadowmap";
            staticShadowmap.Create();

            var shadowCameraObj = new GameObject("Shadow Map Camera");
            shadowCameraObj.hideFlags = HideFlags.HideAndDontSave;
            var shadowCamera = shadowCameraObj.AddComponent<Camera>();
            var additionalCameraData = shadowCameraObj.AddComponent<AdditionalCameraData>();
            additionalCameraData.m_RenderingType = AdditionalCameraData.RenderingType.StaticShadow;
            additionalCameraData.UpdateCamera();
            shadowCamera.enabled = false;
            shadowCamera.renderingPath = RenderingPath.Forward;
            shadowCamera.nearClipPlane = 0.1f;
            shadowCamera.farClipPlane = 100.0f;
            shadowCamera.depthTextureMode = DepthTextureMode.None;
            shadowCamera.clearFlags = CameraClearFlags.SolidColor | CameraClearFlags.Depth;
            shadowCamera.backgroundColor = Color.black;
            shadowCamera.orthographic = false;
            shadowCamera.hideFlags = HideFlags.HideAndDontSave;
            shadowCamera.allowMSAA = false;
            shadowCamera.stereoTargetEye = StereoTargetEyeMask.None;
            shadowCameraObj.SetActive(false);

            //shadowCamera.clearFlags = CameraClearFlags.Depth;
            shadowCamera.cullingMask = 0;
            //shadowCamera.RenderWithShader(depthShader, "DO_NOT_RENDER");
            shadowCamera.clearFlags = CameraClearFlags.SolidColor;
            shadowCamera.cullingMask = ~0;

            for (int i = 0; i < allCachedShadowInfos.Count; i++)
            {
                var csd = allCachedShadows[csdIndex[i]];
                csd.cachedShadowInfo.Clear();
                var info = allCachedShadowInfos[i];
                Light l = csd.GetComponent<Light>();
                if (l != null && l.shadows != LightShadows.None && (csd.shadowUpdateType == ShadowUpdateType.Static || (l.type == LightType.Directional && csd.UseStaticShadowmapForLastCascade)))
                {
                    switch (l.type)
                    {
                        case LightType.Directional:
                            Vector3 lightPos = l.transform.position;
                            Vector3 offset = l.transform.localScale / 2.0f;
                            Vector3[] cornerPos = new Vector3[8];
                            cornerPos[0] = lightPos + offset;
                            cornerPos[1] = lightPos + new Vector3(offset.x * -1.0f, offset.y * 1.0f, offset.z * 1.0f);
                            cornerPos[2] = lightPos + new Vector3(offset.x * 1.0f, offset.y * -1.0f, offset.z * 1.0f);
                            cornerPos[3] = lightPos + new Vector3(offset.x * 1.0f, offset.y * 1.0f, offset.z * -1.0f);
                            cornerPos[4] = lightPos + new Vector3(offset.x * -1.0f, offset.y * -1.0f, offset.z * 1.0f);
                            cornerPos[5] = lightPos + new Vector3(offset.x * -1.0f, offset.y * 1.0f, offset.z * -1.0f);
                            cornerPos[6] = lightPos + new Vector3(offset.x * 1.0f, offset.y * -1.0f, offset.z * -1.0f);
                            cornerPos[7] = lightPos - offset;

                            Bounds lightSpaceBound = new Bounds();
                            Transform trans = l.transform;
                            trans.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                            for (int j = 0; j < 8; j++)
                            {
                                //Vector3 lightSpacePos = targetLight.transform.worldToLocalMatrix.MultiplyPoint(cornerPos[i]);
                                Vector3 lightSpacePos = trans.InverseTransformPoint(cornerPos[j]);
                                lightSpaceBound.Encapsulate(lightSpacePos);
                            }

                            Vector3 size = lightSpaceBound.extents;
                            Vector3 shadowCamPos = l.transform.localToWorldMatrix.MultiplyPoint(lightSpaceBound.center);

                            trans.localScale = offset * 2.0f;

                            shadowCamPos -= l.transform.forward * size.z;
                            float shadowCamSize = Mathf.Max(size.x, size.y);

                            shadowCamera.aspect = 1.0f;
                            shadowCamera.orthographic = true;
                            shadowCamera.nearClipPlane = 0.1f;
                            shadowCamera.farClipPlane = size.z * 2.0f;
                            shadowCamera.transform.position = shadowCamPos;//targetLight.transform.position;// m_light.transform.position;
                            shadowCamera.transform.rotation = l.transform.rotation;
                            shadowCamera.orthographicSize = shadowCamSize;
                            shadowCamera.targetTexture = staticShadowmap;
                            shadowCamera.pixelRect = info.viewport;

                            info.cullingSphereSize = Mathf.Max(shadowCamSize, size.z);
                            info.view = shadowCamera.worldToCameraMatrix;
                            info.proj = shadowCamera.projectionMatrix;
                            allCachedShadowInfos[i] = info;
                            csd.cachedShadowInfo.Add(info);
                            shadowCamera.Render();
                            break;
                        case LightType.Spot:
                            shadowCamera.aspect = 1.0f;
                            shadowCamera.orthographic = false;
                            shadowCamera.nearClipPlane = l.shadowNearPlane;
                            shadowCamera.farClipPlane = l.range;
                            shadowCamera.transform.position = l.transform.position;
                            shadowCamera.transform.rotation = l.transform.rotation;
                            shadowCamera.fieldOfView = l.spotAngle;
                            shadowCamera.targetTexture = staticShadowmap;
                            shadowCamera.pixelRect = info.viewport;

                            info.view = shadowCamera.worldToCameraMatrix;
                            info.proj = shadowCamera.projectionMatrix;
                            allCachedShadowInfos[i] = info;
                            csd.cachedShadowInfo.Add(info);
                            shadowCamera.Render();
                            break;
                        case LightType.Point:
                            break;
                        default:
                            break;
                    }
                }
                EditorUtility.SetDirty(csd);
                EditorSceneManager.MarkSceneDirty(csd.gameObject.scene);
            }

            AsyncGPUReadbackRequest shadowmapReadRequest = AsyncGPUReadback.Request(staticShadowmap, 0, TextureFormat.RFloat);
            shadowmapReadRequest.WaitForCompletion();
            float[] shadowmapDatas = shadowmapReadRequest.GetData<float>().ToArray();
            Color[] shadowmapColors = new Color[shadowWidth * shadowHeight];

            for (int i = 0; i < shadowWidth * shadowHeight; i++)
                shadowmapColors[i] = new Color(shadowmapDatas[i], 0.0f, 0.0f, 0.0f);

            var shadowMap2D = new Texture2D(shadowWidth, shadowHeight, TextureFormat.RFloat, false);
            shadowMap2D.SetPixels(shadowmapColors);

            List<Volume> volumes = ClusterUtils.FindComponentsofType<Volume>();
            foreach(Volume vol in volumes)
            {
                ClusterShadowSettings settings = null;
                if(vol.isGlobal && vol.sharedProfile.TryGet<ClusterShadowSettings>(out settings))
                {
                    var o = new PropertyFetcher<ClusterShadowSettings>(serializedObject);
                    if (settings == (ClusterShadowSettings)target)
                    {
                        string sceneName = volumes[0].gameObject.scene.name;
                        string scenePath = volumes[0].gameObject.scene.path;
                        int pointIndex = scenePath.LastIndexOf('.');
                        string sceneDir = scenePath.Remove(pointIndex);
                        bool exist = Directory.Exists(sceneDir);
                        if (!exist)
                            Directory.CreateDirectory(sceneDir);
                        String shadowmapPath = sceneDir + "/" + staticShadowmap.name + ".asset";
                        AssetDatabase.CreateAsset(shadowMap2D, shadowmapPath);
                        m_staticShadowmap.value.objectReferenceValue = AssetDatabase.LoadAssetAtPath(shadowmapPath, typeof(Texture)) as Texture;
                    }
                }
            }

            //if (volumes.Count == 1)
            //{
            //    string sceneName = volumes[0].gameObject.scene.name;
            //    string scenePath = volumes[0].gameObject.scene.path;
            //    int pointIndex = scenePath.LastIndexOf('.');
            //    string sceneDir = scenePath.Remove(pointIndex);
            //    bool exist = Directory.Exists(sceneDir);
            //    if (!exist)
            //        Directory.CreateDirectory(sceneDir);
            //    String shadowmapPath = sceneDir + "/" + staticShadowmap.name + ".asset";
            //    AssetDatabase.CreateAsset(shadowMap2D, shadowmapPath);
            //    m_staticShadowmap.value.objectReferenceValue = AssetDatabase.LoadAssetAtPath(shadowmapPath, typeof(Texture)) as Texture;
            //}

            staticShadowmap.Release();
            UnityEngine.Object.DestroyImmediate(additionalCameraData);
            UnityEngine.Object.DestroyImmediate(shadowCamera);
            UnityEngine.Object.DestroyImmediate(shadowCameraObj);
        }
    }
}
