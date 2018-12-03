using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental;
using Viva.Rendering.RenderGraph;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Viva.Rendering.RenderGraph
{
    using ShadowRequestVector = VectorArray<ShadowmapBase.ShadowRequest>;
    using ShadowDataVector = VectorArray<ShadowData>;
    using ShadowPayloadVector = VectorArray<ShadowPayload>;
    using ShadowIndicesVector = VectorArray<int>;
    using ShadowAlgoVector = VectorArray<GPUShadowAlgorithm>;

    public struct CachedShadowSettings
    {
        public float MaxShadowDistance;
        public int MaxShadowCasters;
        public Vector2Int ShadowmapRes;
        public Vector2Int StaticShadowmapRes;
        public Texture StaticShadowmap;
        bool enabled;
    }


    //Exponential Shadow Map Atlas
    public class ShadowAtlasExp : ShadowAtlas
    {
        protected const int k_BlurKernelSize = 7;
        protected ComputeShader m_BlurCS;
        protected int m_KernelBlur_Linear_9_H;
        protected int m_KernelBlur_Linear_9_V;
        protected int m_KernelBlur_Tent7x7;
        protected int m_KernelBlur_Tent7X7_Read;
        protected int m_KernelBlur_Tent7X7_LDS;
        protected int m_KernelBlur_Box_PASS1;
        protected int m_KernelBlur_Box_PASS2;
        protected float[] m_Offset_Linear_3 = new float[3] { 0.0f, 1.3846153846f, 3.2307692308f };
        protected float[] m_Weight_Linear_3 = new float[3] { 0.2270270270f, 0.3162162162f, 0.0702702703f };
        protected int m_SampleCount;
        private float m_staticShadowWidth;
        private float m_staticShadowHeight;
        private RenderTexture m_TempShadowMap;
        Vector4[] fetchesUVWeight;

        //default values
        readonly ValRange m_DefESM_LightLeakBias = new ValRange("Light leak bias", 0.0f, 0.5f, 0.99f, 1.0f);
        readonly ValRange m_DefESM_DepthBias = new ValRange("Depth bias", 0.0f, 0.5f, 0.99f, 1.0f);

        public ShadowAtlasExp(ref AtlasInit init) : base(ref init)
        {
            m_BlurCS = init.shadowBlurMoments;
            if (m_BlurCS)
            {
                m_KernelBlur_Linear_9_H = m_BlurCS.FindKernel("TexturBlur_Horizontal_9_Linear");
                m_KernelBlur_Linear_9_V = m_BlurCS.FindKernel("TexturBlur_Vertical_9_Linear");
                m_KernelBlur_Tent7x7 = m_BlurCS.FindKernel("TexturBlur_Tent_7x7");
                m_KernelBlur_Tent7X7_Read = m_BlurCS.FindKernel("TexturBlur_Tent_7x7_Read");
                m_KernelBlur_Tent7X7_LDS = m_BlurCS.FindKernel("TexturBlur_Tent_7x7_LDS_8");
                m_KernelBlur_Box_PASS1 = m_BlurCS.FindKernel("TexturBlur_BOX_TWOPASS");
                m_KernelBlur_Box_PASS2 = m_BlurCS.FindKernel("TexturBlur_BOX_TWOPASS_2");
            }
        }

        public static RenderTextureFormat GetFormat()
        {
            return RenderTextureFormat.Shadowmap;
        }

        protected override void CreateShadowmap(RenderTexture shadowmap)
        {
            //m_Shadowmap.enableRandomWrite = true;
            //Debug.Log("Begin create shadow map exp");
            //base.CreateShadowmap(shadowmap);

            m_Shadowmap.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            m_Shadowmap.dimension = TextureDimension.Tex2D;
            m_Shadowmap.filterMode = FilterMode.Bilinear;
            m_Shadowmap.useMipMap = false;
            m_Shadowmap.name = "Shadow Map Exp";
            RenderTextureDescriptor desc = new RenderTextureDescriptor((int)m_Width, (int)m_Height, RenderTextureFormat.RFloat, (int)m_ShadowmapBits);
            //desc.shadowSamplingMode = ShadowSamplingMode.RawDepth;
            desc.enableRandomWrite = true;
            m_Shadowmap.descriptor = desc;
            m_ShadowmapId = new RenderTargetIdentifier(m_Shadowmap);

            m_TempShadowMap = new RenderTexture((int)m_Width, (int)m_Height, 32, RenderTextureFormat.Depth);
            m_TempShadowMap.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            m_TempShadowMap.dimension = TextureDimension.Tex2D;
            m_TempShadowMap.filterMode = FilterMode.Bilinear;
            m_TempShadowMap.useMipMap = false;
            m_TempShadowMap.name = "Temp Shadow Map Exp";
            m_TempShadowMap.enableRandomWrite = false;
            m_TempShadowMap.Create();

#if false && UNITY_PS4 && !UNITY_EDITOR
            if( m_TempShadowMap != null )
                UnityEngine.PS4.RenderSettings.DisableDepthBufferCompression( m_TempShadowMap );
#endif

            GaussionBlur.SampleShadow_ComputeSamples_Tent_7x7(new Vector4(m_WidthRcp, m_HeightRcp, m_Width, m_Height), out fetchesUVWeight);
        }

        protected override void Register(GPUShadowType type, ShadowRegistry registry)
        {
            ShadowPrecision precision = m_ShadowmapBits == 32 ? ShadowPrecision.High : ShadowPrecision.Low;
            m_SupportedAlgorithms.Reserve(1);
            m_SupportedAlgorithms.AddUniqueUnchecked((int)ShadowUtils.Pack(ShadowAlgorithm.Custom, ShadowVariant.V0, precision));

            ShadowRegistry.VariantDelegate del = (Light l, ShadowAlgorithm dataAlgorithm, ShadowVariant dataVariant, ShadowPrecision dataPrecision, ref int[] dataBlock) =>
            {
                CheckDataIntegrity(dataAlgorithm, dataVariant, dataPrecision, ref dataBlock);

                m_DefESM_LightLeakBias.Slider(ref dataBlock[0]);
                m_DefESM_DepthBias.Slider(ref dataBlock[1]);
            };

            registry.Register(type, precision, ShadowAlgorithm.Custom, "Exponential Shadow Map",
                new ShadowVariant[] { ShadowVariant.V0, ShadowVariant.V1, ShadowVariant.V2, ShadowVariant.V3, ShadowVariant.V4 },
                new string[] { "1 tap", "9 tap adaptive", "tent 3x3 (4 taps)", "tent 5x5 (9 taps)", "tent 7x7 (16 taps)" },
                new ShadowRegistry.VariantDelegate[] { del, del, del, del, del });
        }

        protected override bool CheckDataIntegrity(ShadowAlgorithm algorithm, ShadowVariant variant, ShadowPrecision precision, ref int[] dataBlock)
        {
            if (algorithm != ShadowAlgorithm.Custom)
                return false;

            const int k_BlockSize = 2;
            if (dataBlock == null || dataBlock.Length != k_BlockSize)
            {
                // set defaults
                dataBlock = new int[k_BlockSize];
                dataBlock[0] = m_DefESM_LightLeakBias.Default();
                dataBlock[1] = m_DefESM_DepthBias.Default();
                return false;
            }

            return true;
        }

        public void UpdateStaticShadowmapSettings(CachedShadowSettings settings)
        {
            m_staticShadowWidth = settings.StaticShadowmapRes.x;
            m_staticShadowHeight = settings.StaticShadowmapRes.y;
        }

        override public bool Reserve(FrameId frameId, Camera camera, bool cameraRelativeRendering, ref ShadowData shadowData, ShadowRequest sr, uint[] widths, uint[] heights, ref VectorArray<ShadowData> entries, ref VectorArray<ShadowPayload> payload, List<VisibleLight> lights)
        {
            if (m_FrameId.frameCount != frameId.frameCount)
                m_ActiveEntriesCount = 0;

            m_FrameId = frameId;

            uint algoIdx;
            int shadowAlgo = (int)sr.shadowAlgorithm;
            //if (!m_SupportedAlgorithms.FindFirst(out algoIdx, ref shadowAlgo))
            //    return false;

            ShadowData sd = shadowData;
            ShadowData dummy = new ShadowData();

            if (sr.shadowType != GPUShadowType.Point && sr.shadowType != GPUShadowType.Spot && sr.shadowType != GPUShadowType.Directional)
                return false;

            if (sr.shadowType == GPUShadowType.Directional)
            {
                for (uint i = 0; i < k_MaxCascadesInShader; ++i)
                    m_TmpSplits[i].Set(0.0f, 0.0f, 0.0f, float.NegativeInfinity);
            }

            Key key;
            key.id = sr.instanceId;
            key.faceIdx = 0;
            key.visibleIdx = (int)sr.index;
            key.shadowDataIdx = entries.Count();

            uint originalEntryCount = entries.Count();
            uint originalPayloadCount = payload.Count();
            uint originalActiveEntries = m_ActiveEntriesCount;

            uint facecnt = sr.facecount;
            uint facemask = sr.facemask;
            uint bit = 1;
            int resIdx = 0;
            bool multiFace = sr.shadowType != GPUShadowType.Spot;

            const uint k_MaxShadowDatasPerLight = 7; // 1 shared ShadowData and up to 6 faces for point lights
            entries.Reserve(k_MaxShadowDatasPerLight);

            float nearPlaneOffset = QualitySettings.shadowNearPlaneOffset;

            GPUShadowAlgorithm sanitizedAlgo = ShadowUtils.ClearPrecision(sr.shadowAlgorithm);
            AdditionalShadowData asd = lights[sr.index].light.GetComponent<AdditionalShadowData>();
            if (!asd)
                return false;

            int cascadeCnt = 0;
            float[] cascadeRatios = null;
            float[] cascadeBorders = null;
            if (sr.shadowType == GPUShadowType.Directional)
            {
                asd.GetShadowCascades(out cascadeCnt, out cascadeRatios, out cascadeBorders);
                for (int i = 0; i < m_TmpSplits.Length; i++)
                    m_TmpSplits[i].w = -1.0f;
            }

            uint multiFaceIdx = key.shadowDataIdx;
            if (multiFace)
            {
                entries.AddUnchecked(sd);
                key.shadowDataIdx++;
            }
            payload.Resize(payload.Count() + ReservePayload(sr));

            while (facecnt > 0)
            {
                if ((bit & facemask) != 0)
                {
                    uint width = widths[resIdx];
                    uint height = heights[resIdx];
                    uint ceIdx = 0;

                    bool needAlloc = true;

                    bool isShadowDynamic = true;
                    VisibleLight vl = lights[sr.index];
                    CachedShadowData csd = vl.light.GetComponent<CachedShadowData>();
                    if (csd.shadowUpdateType == ShadowUpdateType.Static || (csd.UseStaticShadowmapForLastCascade && key.faceIdx == cascadeCnt - 1))
                    {
                        isShadowDynamic = false;
                    }


                    if (/*isShadowDynamic && */!Alloc(frameId, key, width, height, out ceIdx, payload))
                    {
                        entries.Purge(entries.Count() - originalEntryCount);
                        payload.Purge(payload.Count() - originalPayloadCount);
                        uint added = m_ActiveEntriesCount - originalActiveEntries;
                        for (uint i = originalActiveEntries; i < m_ActiveEntriesCount; ++i)
                            m_EntryCache.Swap(i, m_EntryCache.Count() - i - 1);
                        m_EntryCache.Purge(added, Free);
                        m_ActiveEntriesCount = originalActiveEntries;
                        return false;
                    }

                    // read
                    float texelSizeX = 1.0f, texelSizeY = 1.0f;
                    CachedEntry ce = m_EntryCache[ceIdx];
                    ce.zclip = sr.shadowType != GPUShadowType.Directional;

                    // modify
                    Matrix4x4 vp, invvp, devproj;
                    bool isDirStaticShadowmap = false;
                    if (sr.shadowType == GPUShadowType.Point)
                        vp = ShadowUtils.ExtractPointLightMatrix(lights[sr.index], key.faceIdx, 2.0f, out ce.current.view, out ce.current.proj, out devproj, out invvp, out ce.current.lightDir, out ce.current.splitData);
                    else if (sr.shadowType == GPUShadowType.Spot)
                    {
                        float spotAngle = lights[sr.index].spotAngle;
                        float guardAngle = ShadowUtils.CalcGuardAnglePerspective(spotAngle, ce.current.viewport.width, GetFilterWidthInTexels(sr, asd), asd.normalBiasMax, 180.0f - spotAngle);
                        if(!isShadowDynamic)
                        {
                            Vector3 lightPos = vl.light.transform.position;

                            CachedShadowInfo info;// = new CachedShadowInfo();
                            if (csd.cachedShadowInfo.Count > 0)
                                info = csd.cachedShadowInfo[0];
                            else
                                info = new CachedShadowInfo();

                            Matrix4x4 scaleMatrix = Matrix4x4.identity;
                            //scaleMatrix.m22 = -1.0f;
                            Matrix4x4 view = scaleMatrix * info.view;
                            Matrix4x4 proj = info.proj; //Matrix4x4.Ortho(-shadowCamSize, shadowCamSize, -shadowCamSize, shadowCamSize, 0.1f, size.z * 2);
                            devproj = GL.GetGPUProjectionMatrix(proj, false);

                            ce.current.view = view;
                            ce.current.proj = proj;
                            ce.current.viewport = info.viewport;
                            ShadowSplitData splitData = new ShadowSplitData();
                            splitData.cullingSphere = new Vector4(0.0f, 0.0f, 0.0f, float.NegativeInfinity);
                            splitData.cullingPlaneCount = 0;
                            ce.current.splitData = splitData;

                            ShadowUtils.InvertPerspective(ref devproj, ref view, out invvp);
                            vp = devproj * view;
                            ce.current.slice = 0xffffffff;
                        }
                        else
                            vp = ShadowUtils.ExtractSpotLightMatrix(lights[sr.index], guardAngle, out ce.current.view, out ce.current.proj, out devproj, out invvp, out ce.current.lightDir, out ce.current.splitData);
                    }
                    else if (sr.shadowType == GPUShadowType.Directional)
                    {
                        if (!isShadowDynamic)
                        {
                            isDirStaticShadowmap = true;
                            Vector3 lightPos = vl.light.transform.position;

                            CachedShadowInfo info;// = new CachedShadowInfo();
                            if (csd.cachedShadowInfo.Count > 0)
                                info = csd.cachedShadowInfo[0];
                            else
                                info = new CachedShadowInfo();

                            Matrix4x4 view = info.view;
                            Matrix4x4 proj = info.proj; //Matrix4x4.Ortho(-shadowCamSize, shadowCamSize, -shadowCamSize, shadowCamSize, 0.1f, size.z * 2);
                            devproj = GL.GetGPUProjectionMatrix(proj, false);
                            uint shadowRes = csd.StaticShadowResolution;
                            ce.current.view = view;
                            ce.current.proj = proj;
                            ce.current.viewport = info.viewport;// new Rect(0, 0, shadowRes, shadowRes);
                            ShadowSplitData splitData = new ShadowSplitData();
                            splitData.cullingSphere = new Vector4(lightPos.x, lightPos.y, lightPos.z, info.cullingSphereSize);
                            splitData.cullingPlaneCount = 0;
                            ce.current.splitData = splitData;

                            ShadowUtils.InvertOrthographic(ref devproj, ref view, out invvp);
                            vp = devproj * view;
                            ce.current.slice = 0xffffffff;
                        }
                        else
                            vp = ShadowUtils.ExtractDirectionalLightMatrix(vl, key.faceIdx, /*csd.UseStaticShadowmapForLastCascade ? cascadeCnt - 1 : */cascadeCnt, cascadeRatios, nearPlaneOffset, width, height, out ce.current.view, out ce.current.proj, out devproj, out invvp, out ce.current.lightDir, out ce.current.splitData, m_CullResults, (int)sr.index);

                        m_TmpSplits[key.faceIdx] = ce.current.splitData.cullingSphere;
                        if (ce.current.splitData.cullingSphere.w != float.NegativeInfinity)
                        {
                            int face = (int)key.faceIdx;
                            texelSizeX = 2.0f / ce.current.proj.m00;
                            texelSizeY = 2.0f / ce.current.proj.m11;
                            m_TmpBorders[face] = cascadeBorders[face];
                            m_TmpSplits[key.faceIdx].w *= ce.current.splitData.cullingSphere.w;
                        }
                    }
                    else
                        vp = invvp = devproj = Matrix4x4.identity; // should never happen, though

                    if (cameraRelativeRendering)
                    {
                        Vector3 camPosWS = camera.transform.position;
                        Matrix4x4 translation = Matrix4x4.Translate(camPosWS);
                        ce.current.view *= translation;
                        vp *= translation;
                        if (sr.shadowType == GPUShadowType.Directional)
                        {
                            m_TmpSplits[key.faceIdx].x -= camPosWS.x;
                            m_TmpSplits[key.faceIdx].y -= camPosWS.y;
                            m_TmpSplits[key.faceIdx].z -= camPosWS.z;
                        }
                    }

                    // extract texel size in world space
                    int flags = 0;
                    flags |= asd.sampleBiasScale ? (1 << 0) : 0;
                    flags |= asd.edgeLeakFixup ? (1 << 1) : 0;
                    flags |= asd.edgeToleranceNormal ? (1 << 2) : 0;
                    sd.edgeTolerance = asd.edgeTolerance;
                    sd.viewBias = new Vector4(asd.viewBiasMin, asd.viewBiasMax, asd.viewBiasScale, 2.0f / ce.current.proj.m00 / ce.current.viewport.width * 1.4142135623730950488016887242097f);
                    //sd.normalBias = new Vector4(asd.normalBiasMin, asd.normalBiasMax, asd.normalBiasScale, ShadowUtils.Asfloat(flags));
                    sd.normalBias = new Vector4(csd.DepthFallOffPercent, csd.ShadowNearMultiplier, csd.ShadowFarMultiplier, ShadowUtils.Asfloat(flags));

                    // write :(
                    ce.current.shadowAlgo = (GPUShadowAlgorithm)shadowAlgo;
                    m_EntryCache[ceIdx] = ce;

                    if (sr.shadowType == GPUShadowType.Directional)
                        sd.pos = new Vector3(ce.current.view.m03, ce.current.view.m13, ce.current.view.m23);
                    else
                        sd.pos = cameraRelativeRendering ? (lights[sr.index].light.transform.position - camera.transform.position) : lights[sr.index].light.transform.position;

                    sd.shadowToWorld = invvp.transpose;
                    sd.proj = new Vector4(devproj.m00, devproj.m11, devproj.m22, devproj.m23);
                    sd.rot0 = new Vector3(ce.current.view.m00, ce.current.view.m01, ce.current.view.m02);
                    sd.rot1 = new Vector3(ce.current.view.m10, ce.current.view.m11, ce.current.view.m12);
                    sd.rot2 = new Vector3(ce.current.view.m20, ce.current.view.m21, ce.current.view.m22);
                    float actualWidth = isShadowDynamic ? m_Width : m_staticShadowWidth;
                    float actualHeight = isShadowDynamic ? m_Height : m_staticShadowHeight;
                    sd.scaleOffset = new Vector4(ce.current.viewport.width / actualWidth, ce.current.viewport.height / actualHeight, ce.current.viewport.x / actualWidth, ce.current.viewport.y / actualHeight);
                    sd.textureSize = new Vector4(m_Width, m_Height, ce.current.viewport.width, ce.current.viewport.height);
                    sd.texelSizeRcp = new Vector4(m_WidthRcp, m_HeightRcp, 1.0f / ce.current.viewport.width, 1.0f / ce.current.viewport.height);
                    sd.PackShadowmapId(m_TexSlot, m_SampSlot);
                    sd.slice = isShadowDynamic ? ce.current.slice : -1.0f;
                    sd.PackShadowType(sr.shadowType, sanitizedAlgo);
                    sd.payloadOffset = originalPayloadCount;
                    entries.AddUnchecked(sd);
                    if (!isShadowDynamic)
                        ce.current.viewport = new Rect(0, 0, 0, 0);

                    resIdx++;
                    facecnt--;
                    key.shadowDataIdx++;
                }
                else
                {
                    // we push a dummy face in, otherwise we'd need a mapping from face index to shadowData in the shader as well
                    entries.AddUnchecked(dummy);
                }
                key.faceIdx++;
                bit <<= 1;
            }

            WritePerLightPayload(lights, sr, ref sd, ref payload, ref originalPayloadCount);

            return true;
        }

        override protected uint ReservePayload(ShadowRequest sr)
        {
            uint cnt = base.ReservePayload(sr);
            return cnt;
        }

        protected override void WritePerLightPayload(List<VisibleLight> lights, ShadowRequest sr, ref ShadowData sd, ref VectorArray<ShadowPayload> payload, ref uint payloadOffset)
        {
            ShadowPayload sp = new ShadowPayload();
            if (sr.shadowType == GPUShadowType.Directional)
            {
                uint first = k_MaxCascadesInShader, second = k_MaxCascadesInShader;
                for (uint i = 0; i < k_MaxCascadesInShader; i++, payloadOffset++)
                {
                    first = (first == k_MaxCascadesInShader && m_TmpSplits[i].w > 0.0f) ? i : first;
                    second = ((second == k_MaxCascadesInShader || second == first) && m_TmpSplits[i].w > 0.0f) ? i : second;
                    sp.Set(m_TmpSplits[i]);
                    payload[payloadOffset] = sp;
                }
                if (second != k_MaxCascadesInShader)
                    sp.Set((m_TmpSplits[second] - m_TmpSplits[first]).normalized);
                else
                    sp.Set(0.0f, 0.0f, 0.0f, 0.0f);
                payload[payloadOffset] = sp;
                payloadOffset++;

                for (int i = 0; i < m_TmpBorders.Length; i += 4)
                {
                    sp.Set(m_TmpBorders[i + 0], m_TmpBorders[i + 1], m_TmpBorders[i + 2], m_TmpBorders[i + 3]);
                    payload[payloadOffset] = sp;
                    payloadOffset++;
                }
            }
            ShadowAlgorithm algo; ShadowVariant vari; ShadowPrecision prec;
            ShadowUtils.Unpack(sr.shadowAlgorithm, out algo, out vari, out prec);

            AdditionalShadowData asd = lights[sr.index].light.GetComponent<AdditionalShadowData>();
            if (!asd)
                return;

            int shadowDataFormat;
            int[] shadowData = asd.GetShadowData(out shadowDataFormat);
            if (!CheckDataIntegrity(algo, vari, prec, ref shadowData))
            {
                asd.SetShadowAlgorithm((int)algo, (int)vari, (int)prec, shadowDataFormat, shadowData);
                //Debug.Log("Fixed up shadow data for algorithm " + algo + ", variant " + vari);
            }

            if (algo == ShadowAlgorithm.Custom)
            {

                switch (vari)
                {
                    case ShadowVariant.V0:
                    case ShadowVariant.V1:
                    case ShadowVariant.V2:
                    case ShadowVariant.V3:
                    case ShadowVariant.V4:
                        {
                            sp.Set(shadowData[0] | (SystemInfo.usesReversedZBuffer ? 1 : 0), shadowData[1], 0, 0);
                            payload[payloadOffset] = sp;
                            payloadOffset++;
                        }
                        break;
                }
            }
        }

        protected override void PreUpdate(FrameId frameId, CommandBuffer cb, uint rendertargetSlice)
        {
            //cb.SetRenderTarget(m_ShadowmapId);
            //if (!IsNativeDepth())
            //{
            //    cb.GetTemporaryRT(m_TempDepthId, (int)m_Width, (int)m_Height, (int)m_ShadowmapBits, FilterMode.Bilinear, RenderTextureFormat.Shadowmap, RenderTextureReadWrite.Default);
            //    cb.SetRenderTarget(new RenderTargetIdentifier(m_TempDepthId));
            //}
            //cb.ClearRenderTarget(true, !IsNativeDepth(), m_ClearColor);

            cb.SetRenderTarget(m_TempShadowMap);
            cb.ClearRenderTarget(true, false, m_ClearColor);
        }

        override public void Update(FrameId frameId, ScriptableRenderContext renderContext, CommandBuffer cmd, CullResults cullResults, List<VisibleLight> lights)
        {
            if (m_ActiveEntriesCount == 0)
                return;

            //var profilingSample = new ProfilingSample(cmd, "Shadowmap{0}", m_TexSlot);

            string cbName = "";
            //if (!string.IsNullOrEmpty(m_ShaderKeyword))
            //{
            //    cbName = "Shadowmap.EnableShadowKeyword";
            //    cmd.BeginSample(cbName);
            //    cmd.EnableShaderKeyword(m_ShaderKeyword);
            //    cmd.EndSample(cbName);
            //}

            // loop for generating each individual shadowmap
            uint curSlice = uint.MaxValue;
            Bounds bounds;
            DrawShadowsSettings dss = new DrawShadowsSettings(cullResults, 0);
            for (uint i = 0; i < m_ActiveEntriesCount; ++i)
            {
                if (!cullResults.GetShadowCasterBounds(m_EntryCache[i].key.visibleIdx, out bounds))
                    continue;

                uint entrySlice = m_EntryCache[i].current.slice;
                if (entrySlice != curSlice && entrySlice == 0)
                {
                    Debug.Assert(curSlice == uint.MaxValue || entrySlice >= curSlice, "Entries in the entry cache are not ordered in slice order.");
                    cbName = "Shadowmap.Update.Slice" + entrySlice.ToString();
                    cmd.BeginSample(cbName);

                    if (curSlice != uint.MaxValue)
                    {
                        PostUpdate(frameId, cmd, curSlice, lights);
                    }
                    curSlice = entrySlice;
                    PreUpdate(frameId, cmd, curSlice);

                    cmd.EndSample(cbName);
                }

                bool needDrawShadow = true;
                VisibleLight vl = lights[m_EntryCache[i].key.visibleIdx];
                Light l = vl.light;// lights[m_EntryCache[i].key.visibleIdx].light;
                int cascadeCnt = l.GetComponent<AdditionalShadowData>().cascadeCount;
                if (l.type == LightType.Directional && m_EntryCache[i].key.faceIdx == cascadeCnt - 1)
                {
                    CachedShadowData csd = l.GetComponent<CachedShadowData>();
                    if (csd.UseStaticShadowmapForLastCascade)
                    {
                        needDrawShadow = false;
                    }
                }

                if (needDrawShadow)
                {
                    //cbName = string.Format("Shadowmap.Update - slice: {0}, vp.x: {1}, vp.y: {2}, vp.w: {3}, vp.h: {4}", curSlice, m_EntryCache[i].current.viewport.x, m_EntryCache[i].current.viewport.y, m_EntryCache[i].current.viewport.width, m_EntryCache[i].current.viewport.height);
                    //cmd.BeginSample(cbName);
                    cmd.SetViewport(m_EntryCache[i].current.viewport);
                    cmd.SetViewProjectionMatrices(m_EntryCache[i].current.view, m_EntryCache[i].current.proj);
                    cmd.SetGlobalVector("g_vLightDirWs", m_EntryCache[i].current.lightDir);
                    cmd.SetGlobalFloat(m_ZClipId, m_EntryCache[i].zclip ? 1.0f : 0.0f);
                    Vector3 lightDirection = -vl.localToWorld.GetColumn(2);
                    cmd.SetGlobalVector("_LightDirection", new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 1.0f));

                    float bias = 0.0f;
                    float normalBias = 0.0f;
                    const float kernelRadius = 3.65f;

                    if (vl.lightType == LightType.Directional)
                    {
                        // Scale bias by cascade's world space depth range.
                        // Directional shadow lights have orthogonal projection.
                        // proj.m22 = -2 / (far - near) since the projection's depth range is [-1.0, 1.0]
                        // In order to be correct we should multiply bias by 0.5 but this introducing aliasing along cascades more visible.
                        float sign = (SystemInfo.usesReversedZBuffer) ? 1.0f : -1.0f;
                        bias = l.shadowBias * m_EntryCache[i].current.proj.m22 * sign;

                        // Currently only square POT cascades resolutions are used.
                        // We scale normalBias
                        double frustumWidth = 2.0 / (double)m_EntryCache[i].current.proj.m00;
                        double frustumHeight = 2.0 / (double)m_EntryCache[i].current.proj.m11;
                        float texelSizeX = (float)(frustumWidth / (double)m_EntryCache[i].current.viewport.width);
                        float texelSizeY = (float)(frustumHeight / (double)m_EntryCache[i].current.viewport.height);
                        float texelSize = Mathf.Max(texelSizeX, texelSizeY);

                        // Since we are applying normal bias on caster side we want an inset normal offset
                        // thus we use a negative normal bias.
                        normalBias = -l.shadowNormalBias * texelSize * kernelRadius;
                    }
                    else if (vl.lightType == LightType.Spot)
                    {
                        float sign = (SystemInfo.usesReversedZBuffer) ? -1.0f : 1.0f;
                        bias = l.shadowBias * sign;
                        normalBias = 0.0f;
                    }

                    cmd.SetGlobalVector("_ShadowBias", new Vector4(bias, normalBias, 0, 0));
                    //cmd.EndSample(cbName);

                    dss.lightIndex = m_EntryCache[i].key.visibleIdx;
                    dss.splitData = m_EntryCache[i].current.splitData;

                    // This is done here because DrawRenderers API lives outside command buffers so we need to make sur eto call this before doing any DrawRenders
                    renderContext.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    renderContext.DrawShadows(ref dss); // <- if this was a call on the commandbuffer we would get away with using just once commandbuffer for the entire shadowmap, instead of one per face
                }

            }

            // post update
            PostUpdate(frameId, cmd, curSlice, lights);
            if (!string.IsNullOrEmpty(m_ShaderKeyword))
            {
                cmd.BeginSample("Shadowmap.DisableShaderKeyword");
                cmd.DisableShaderKeyword(m_ShaderKeyword);
                cmd.EndSample("Shadowmap.DisableShaderKeyword");
            }

            cmd.SetGlobalFloat(m_ZClipId, 1.0f); // Re-enable zclip globally
            m_ActiveEntriesCount = 0;

            //profilingSample.Dispose();
        }

        new protected bool Layout()
        {
            VectorArray<CachedEntry> tmp = m_EntryCache.Subrange(0, m_ActiveEntriesCount);
            tmp.Sort();

            float curx = 0, cury = 0, curh = 0, xmax = m_Width, ymax = m_Height;
            uint curslice = 0;

            for (uint i = 0; i < m_ActiveEntriesCount; ++i)
            {
                // shadow atlas layouting
                CachedEntry ce = m_EntryCache[i];
                Rect vp = ce.current.viewport;
                curh = curh >= vp.height ? curh : vp.height;

                if (curx + vp.width > xmax)
                {
                    curx = 0;
                    cury += curh;
                    curh = vp.height;
                }
                if (curx + vp.width > xmax || cury + curh > ymax)
                {
                    curslice++;
                    curx = 0;
                    cury = 0;
                    curh = vp.height;
                }
                if (curx + vp.width > xmax || cury + curh > ymax || curslice == m_Slices)
                {
                    Debug.LogWarning("Shadow atlasing has failed.");
                    return false;
                }
                vp.x = curx;
                vp.y = cury;
                ce.current.viewport = vp;
                ce.current.slice = curslice;
                m_EntryCache[i] = ce;
                curx += vp.width;
            }
            return true;
        }

        override public bool ReserveFinalize(FrameId frameId, ref VectorArray<ShadowData> entries, ref VectorArray<ShadowPayload> payload)
        {
            if (Layout())
            {
                // patch up the shadow data contents with the result of the layouting step
                for (uint i = 0; i < m_ActiveEntriesCount; ++i)
                {
                    CachedEntry ce = m_EntryCache[i];

                    ShadowData sd = entries[ce.key.shadowDataIdx];
                    if(sd.slice >= 0)
                    {
                        // update the shadow data with the actual result of the layouting step
                        sd.scaleOffset = new Vector4(ce.current.viewport.width * m_WidthRcp, ce.current.viewport.height * m_HeightRcp, ce.current.viewport.x * m_WidthRcp, ce.current.viewport.y * m_HeightRcp);
                        sd.PackShadowmapId(m_TexSlot, m_SampSlot);
                        sd.slice = ce.current.slice;
                        // write back the correct results
                        entries[ce.key.shadowDataIdx] = sd;
                    }
                    else
                    {
                        ce.current.slice = 4;
                    }
                }
                m_EntryCache.Purge(m_EntryCache.Count() - m_ActiveEntriesCount, m_Cleanup);
                return true;
            }
            m_ActiveEntriesCount = 0;
            m_EntryCache.Reset(m_Cleanup);
            return false;
        }

        protected override void PostUpdate(FrameId frameId, CommandBuffer cb, uint rendertargetSlice, List<VisibleLight> lights)
        {
            if (rendertargetSlice == uint.MaxValue)
            {
                base.PostUpdate(frameId, cb, rendertargetSlice, lights);
                return;
            }

            uint cnt = m_EntryCache.Count();
            uint i = 0;
            while (i < cnt && m_EntryCache[i].current.slice < rendertargetSlice)
                i++;

            if (i >= cnt || m_EntryCache[i].current.slice > rendertargetSlice)
                return;

            cb.BeginSample("Exp blur");

            //Set blur compute shader params
            //-------------------------------

            while (i < cnt && m_EntryCache[i].current.slice == rendertargetSlice)
            {
                AdditionalShadowData asd = lights[m_EntryCache[i].key.visibleIdx].light.GetComponent<AdditionalShadowData>();
                int shadowDataFormat;
                int[] shadowData = asd.GetShadowData(out shadowDataFormat);

                ShadowAlgorithm algo;
                ShadowVariant vari;
                ShadowPrecision prec;
                ShadowUtils.Unpack(m_EntryCache[i].current.shadowAlgo, out algo, out vari, out prec);

                //if (algo == ShadowAlgorithm.ESM)
                //{
                //    //cb.SetComputeTextureParam(m_BlurCS, m_KernelBlur_Linear_9_H, "DestinationTexture", m_Shadowmap);
                //    //Rect viewport = m_EntryCache[i].current.viewport;
                //    //Vector4 sizeOffset = new Vector4(viewport.width, viewport.height, viewport.x, viewport.y);
                //    //cb.SetComputeVectorParam(m_BlurCS, "SizeOffset", sizeOffset);
                //    //int threadX = (int)viewport.width / 8;
                //    //int threadY = (int)viewport.height / 8;
                //    //cb.DispatchCompute(m_BlurCS, m_KernelBlur_Linear_9_H, threadX, threadY, 1);

                //    //cb.SetComputeTextureParam(m_BlurCS, m_KernelBlur_Linear_9_V, "DestinationTexture", m_Shadowmap);
                //    //cb.DispatchCompute(m_BlurCS, m_KernelBlur_Linear_9_V, threadX, threadY, 1);
                //}
                //else
                //{
                //    Debug.LogError("Unkown shadow algorithm selected for exponential shadow maps");
                //    continue;
                //}

                // TODO: Need a check here whether the shadowmap actually got updated, but right now that's queried on the cullResults.
                Rect r = m_EntryCache[i].current.viewport;
                //Set blur compute shader params

                i++;
            }

            base.PostUpdate(frameId, cb, rendertargetSlice, lights);
            cb.EndSample("Exp blur");
        }

        public void PostBlurExpShadows(CommandBuffer cb, int blurMethod)
        {
            for(uint i = 0; i < m_EntryCache.Count(); i++)
            {
                Rect viewport = m_EntryCache[i].current.viewport;
                if (viewport.width <= 0 || viewport.height <= 0 || m_EntryCache[i].current.slice > 1)
                    continue;

                //m_KernelBlur_Tent7x7 = m_BlurCS.FindKernel("TexturBlur_Tent_7x7_Read");
                int threadX = (int)viewport.width / 8;
                int threadY = (int)viewport.height / 8;
                if(threadX > 0 && threadY > 0)
                {
                    int actualKernel = m_KernelBlur_Tent7x7;
                    switch(3)
                    {
                        case 0:
                            actualKernel = m_KernelBlur_Tent7x7;
                            break;
                        case 1:
                            actualKernel = m_KernelBlur_Tent7X7_Read;
                            break;
                        case 2:
                            actualKernel = m_KernelBlur_Tent7X7_LDS;
                            break;
                        case 3:
                            actualKernel = m_KernelBlur_Box_PASS1;
                            break;
                        default:
                            break;

                    }
                    Vector4 sizeOffset = new Vector4(viewport.width, viewport.height, viewport.x, viewport.y);
                    cb.SetComputeVectorArrayParam(m_BlurCS, ClusterShaderIDs._Tent7X7_UV_Weights, fetchesUVWeight);
                    cb.SetComputeVectorParam(m_BlurCS, "SizeOffset", sizeOffset);
                    
                    cb.SetComputeTextureParam(m_BlurCS, actualKernel, "SourceTexture", m_TempShadowMap);
                    cb.SetComputeTextureParam(m_BlurCS, actualKernel, "DestinationTexture", m_Shadowmap);
                    cb.DispatchCompute(m_BlurCS, actualKernel, threadX, threadY, 1);
                    if(blurMethod == 3)
                    {
                        cb.SetComputeTextureParam(m_BlurCS, m_KernelBlur_Box_PASS2, "DestinationTexture", m_Shadowmap);
                        cb.DispatchCompute(m_BlurCS, m_KernelBlur_Box_PASS2, threadX, threadY, 1);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (m_Shadowmap != null)
                m_Shadowmap.Release();

            if (m_TempShadowMap != null)
                m_TempShadowMap.Release();
        }
    }

    public class CachedShadowManager : ShadowManagerBase
    {
        protected class CachedShadowContextAccess : ShadowContext
        {
            public CachedShadowContextAccess(ref ShadowContext.CtxtInit initializer) : base(ref initializer) { }

            public VectorArray<ShadowData> shadowDatas { get { return m_ShadowDatas; } set { m_ShadowDatas = value; } }
            public VectorArray<ShadowPayload> payloads { get { return m_Payloads; } set { m_Payloads = value; } }
        }

        private const int k_MaxShadowmapPerType = 4;
        private CachedShadowSettings m_ShadowSettings;
        private ShadowmapBase[] m_Shadowmaps;
        private ShadowmapBase[,] m_ShadowmapsPerType = new ShadowmapBase[(int)GPUShadowType.MAX, k_MaxShadowmapPerType];
        private CachedShadowContextAccess m_ShadowCtxt;
        private int[,] m_MaxShadows = new int[(int)GPUShadowType.MAX, 2];

        private VectorArray<long> m_TmpSortKeys = new VectorArray<long>(0, false);
        private ShadowRequestVector m_TmpRequests = new ShadowRequestVector(0, false);

        private ShadowIndicesVector m_ShadowIndices = new ShadowIndicesVector(0, false);

        public override uint GetShadowMapCount()
        {
            return (uint)m_Shadowmaps.Length;
        }

        public override uint GetShadowMapSliceCount(uint shadowMapIndex)
        {
            if (shadowMapIndex >= m_Shadowmaps.Length)
                return 0;

            return m_Shadowmaps[shadowMapIndex].slices;
        }

        public override uint GetShadowRequestCount()
        {
            return m_TmpRequests.Count();
        }

        public override uint GetShadowRequestFaceCount(uint requestIndex)
        {
            if (requestIndex >= (int)m_TmpRequests.Count())
                return 0;
            else
                return m_TmpRequests[requestIndex].facecount;
        }

        public override int GetShadowRequestIndex(Light light)
        {
            for (int i = 0; i < m_TmpRequests.Count(); ++i)
            {
                if (m_TmpRequests[(uint)i].instanceId == light.GetInstanceID())
                    return i;
            }

            return -1;
        }

        public CachedShadowManager(CachedShadowSettings shadowSettings, ref ShadowContext.CtxtInit ctxtInitializer, ShadowmapBase[] shadowmaps)
        {
            m_ShadowSettings = shadowSettings;
            m_ShadowCtxt = new CachedShadowContextAccess(ref ctxtInitializer);

            Debug.Assert(shadowmaps != null && shadowmaps.Length > 0);
            m_Shadowmaps = shadowmaps;
            foreach (var sm in shadowmaps)
            {
                sm.CreateShadowmap();
                sm.Register(this);
                sm.ReserveSlots(m_ShadowCtxt);
                ShadowmapBase.ShadowSupport smsupport = sm.QueryShadowSupport();
                for (int i = 0, bit = 1; i < (int)GPUShadowType.MAX; ++i, bit <<= 1)
                {
                    if (((int)smsupport & bit) == 0)
                        continue;

                    for (int idx = 0; i < k_MaxShadowmapPerType; ++idx)
                    {
                        if (m_ShadowmapsPerType[i, idx] == null)
                        {
                            m_ShadowmapsPerType[i, idx] = sm;
                            break;
                        }
                    }
                    Debug.Assert(m_ShadowmapsPerType[i, k_MaxShadowmapPerType - 1] == null || m_ShadowmapsPerType[i, k_MaxShadowmapPerType - 1] == sm,
                        "Only up to " + k_MaxShadowmapPerType + " are allowed per light type. If more are needed then increase ShadowManager.k_MaxShadowmapPerType");
                }
            }

            m_MaxShadows[(int)GPUShadowType.Point, 0] = m_MaxShadows[(int)GPUShadowType.Point, 1] = 4;
            m_MaxShadows[(int)GPUShadowType.Spot, 0] = m_MaxShadows[(int)GPUShadowType.Spot, 1] = 8;
            m_MaxShadows[(int)GPUShadowType.Directional, 0] = m_MaxShadows[(int)GPUShadowType.Directional, 1] = 2;

#if UNITY_EDITOR
            AdditionalShadowDataEditor.SetRegistry(this);
#endif
        }

        public override void UpdateCullingParameters(ref ScriptableCullingParameters cullingParams)
        {
            cullingParams.shadowDistance = Mathf.Min(m_ShadowSettings.MaxShadowDistance, cullingParams.shadowDistance);
        }

        public override void ProcessShadowRequests(FrameId frameId, CullResults cullResults, Camera camera, bool cameraRelativeRendering, List<VisibleLight> lights, ref uint shadowRequestsCount, int[] shadowRequests, out int[] shadowDataIndices)
        {
            shadowDataIndices = null;

            // TODO:
            // Cached the cullResults here so we don't need to pass them around.
            // Allocate needs to pass them to the shadowmaps, as the ShadowUtil functions calculating view/proj matrices need them to call into C++ land.
            // Ideally we can get rid of that at some point, then we wouldn't need to cache them here, anymore.
            foreach (var sm in m_Shadowmaps)
            {
                sm.Assign(cullResults);
            }

            if (shadowRequestsCount == 0 || lights == null || shadowRequests == null)
            {
                shadowRequestsCount = 0;
                return;
            }

            //first sort the shadow casters according to some priority
            PrioritizeShadowCasters(camera, lights, shadowRequestsCount, shadowRequests);

            //next prune them based on some logic
            VectorArray<int> requestedShadows = new VectorArray<int>(shadowRequests, 0, shadowRequestsCount, false);
            m_TmpRequests.Reset(shadowRequestsCount);
            uint totalGranted;
            PruneShadowCasters(camera, lights, ref requestedShadows, ref m_TmpRequests, out totalGranted);

            //if there are no shadow casters at this point -> bail
            if (totalGranted == 0)
            {
                shadowRequestsCount = 0;
                return;
            }

            // TODO: Now would be a good time to kick off the culling jobs for the granted requests - but there's no way to control that at the moment.

            // finally go over the lights deemed shadow casters and try to fit them into the shadow map
            // shadowmap allocation must succeed at this point.
            m_ShadowCtxt.ClearData();
            ShadowDataVector shadowVector = m_ShadowCtxt.shadowDatas;
            ShadowPayloadVector payloadVector = m_ShadowCtxt.payloads;
            m_ShadowIndices.Reset(m_TmpRequests.Count());
            if (!AllocateShadows(frameId, camera, cameraRelativeRendering, lights, totalGranted, ref m_TmpRequests, ref m_ShadowIndices, ref shadowVector, ref payloadVector))
            {
                shadowRequestsCount = 0;
                return;
            }
            Debug.Assert(m_TmpRequests.Count() == m_ShadowIndices.Count());
            m_ShadowCtxt.shadowDatas = shadowVector;
            m_ShadowCtxt.payloads = payloadVector;

            //and set the output parameters
            uint offset;
            shadowDataIndices = m_ShadowIndices.AsArray(out offset, out shadowRequestsCount);
        }

        public class SortReverter : System.Collections.Generic.IComparer<long>
        {
            public int Compare(long lhs, long rhs)
            {
                return rhs.CompareTo(lhs);
            }
        }

        public void UpdateShadowSettings(CachedShadowSettings shadowSettings)
        {
            m_ShadowSettings = shadowSettings;
            foreach(var sm in m_Shadowmaps)
            {
                if(sm is ShadowAtlasExp)
                {
                    (sm as ShadowAtlasExp).UpdateStaticShadowmapSettings(m_ShadowSettings);
                }
            }
        }

        protected override void PrioritizeShadowCasters(Camera camera, List<VisibleLight> lights, uint shadowRequestsCount, int[] shadowRequests)
        {
            // this function simply looks at the projected area on the screen, ignoring all light types and shapes
            m_TmpSortKeys.Reset(shadowRequestsCount);

            for (int i = 0; i < shadowRequestsCount; ++i)
            {
                int vlidx = shadowRequests[i];
                VisibleLight vl = lights[vlidx];
                Light l = vl.light;

                //use the screen rect as a measure of importance
                float area = vl.screenRect.width * vl.screenRect.height;
                long val = ShadowUtils.Asint(area);
                val <<= 32;
                val |= (uint)vlidx;
                m_TmpSortKeys.AddUnchecked(val);
            }

            m_TmpSortKeys.Sort(new SortReverter());
            m_TmpSortKeys.ExtractTo(shadowRequests, 0, out shadowRequestsCount, delegate (long key) { return (int)(key & 0xffffffff); });
        }

        protected override void PruneShadowCasters(Camera camera, List<VisibleLight> lights, ref ShadowIndicesVector shadowRequests, ref ShadowRequestVector requestsGranted, out uint totalRequestCount)
        {
            Debug.Assert(shadowRequests.Count() > 0);
            //at this point the array is sorted in order of some importance determined by the prioritize function
            requestsGranted.Reserve(shadowRequests.Count());
            totalRequestCount = 0;
            Vector3 campos = new Vector3(camera.transform.position.x, camera.transform.position.y, camera.transform.position.z);

            ShadowmapBase.ShadowRequest sreq = new ShadowmapBase.ShadowRequest();
            uint totalSlots = ResetMaxShadows();
            //there is a 1:1 mapping between the index in the shadow requests array and the element in requestsGranted at the same index.
            //if the prune function skips requests it must make sure that the array is still compact
            m_TmpSortKeys.Reset(shadowRequests.Count());
            for (uint i = 0, count = shadowRequests.Count(); i < count && totalSlots > 0; ++i)
            {
                int requestIdx = shadowRequests[i];
                VisibleLight vl = lights[requestIdx];
                int faceCount = 0;
                GPUShadowType shadowType = GPUShadowType.Point;

                AdditionalShadowData asd = vl.light.GetComponent<AdditionalShadowData>();
                Vector3 lpos = vl.light.transform.position;
                float distToCam = (campos - lpos).magnitude;
                bool add = (distToCam < asd.shadowFadeDistance || vl.lightType == LightType.Directional)/* && m_ShadowSettings.enabled*/;

                if (add)
                {
                    switch (vl.lightType)
                    {
                        case LightType.Directional:
                            add = --m_MaxShadows[(int)GPUShadowType.Directional, 0] >= 0;
                            shadowType = GPUShadowType.Directional;
                            faceCount = asd.cascadeCount;
                            break;
                        case LightType.Point:
                            add = --m_MaxShadows[(int)GPUShadowType.Point, 0] >= 0;
                            shadowType = GPUShadowType.Point;
                            faceCount = 6;
                            break;
                        case LightType.Spot:
                            add = --m_MaxShadows[(int)GPUShadowType.Spot, 0] >= 0;
                            shadowType = GPUShadowType.Spot;
                            faceCount = 1;
                            break;
                    }
                }

                if (add)
                {
                    sreq.instanceId = vl.light.GetInstanceID();
                    sreq.index = requestIdx;
                    sreq.facemask = (uint)(1 << faceCount) - 1;
                    sreq.shadowType = shadowType;

                    int sa, sv, sp;
                    asd.GetShadowAlgorithm(out sa, out sv, out sp);
                    sreq.shadowAlgorithm = ShadowUtils.Pack((ShadowAlgorithm)sa, (ShadowVariant)sv, (ShadowPrecision)sp);
                    totalRequestCount += (uint)faceCount;
                    requestsGranted.AddUnchecked(sreq);
                    totalSlots--;
                }
                else
                    m_TmpSortKeys.AddUnchecked(requestIdx);
            }

            shadowRequests.Reset();
            requestsGranted.ExtractTo(ref shadowRequests, (ShadowmapBase.ShadowRequest request) => { return (int)request.index; });
            m_TmpSortKeys.ExtractTo(ref shadowRequests, (long idx) => { return (int)idx; });
        }

        protected override bool AllocateShadows(FrameId frameId, Camera camera, bool cameraRelativeRendering, List<VisibleLight> lights, uint totalGranted, ref ShadowRequestVector grantedRequests, ref ShadowIndicesVector shadowIndices, ref ShadowDataVector shadowmapDatas, ref ShadowPayloadVector shadowmapPayload)
        {
            ShadowData sd = new ShadowData();
            shadowmapDatas.Reserve(totalGranted);
            shadowIndices.Reserve(grantedRequests.Count());
            for (uint i = 0, cnt = grantedRequests.Count(); i < cnt; i++)
            {
                Light l = lights[grantedRequests[i].index].light;
                AdditionalShadowData asd = l.GetComponent<AdditionalShadowData>();

                //set light specific values that are not related to the shadow map
                GPUShadowType shadowType = GetShadowLightType(l);

                shadowIndices.AddUnchecked((int)shadowmapDatas.Count());

                int smidx = 0;
                while (smidx < k_MaxShadowmapPerType)
                {
                    if (m_ShadowmapsPerType[(int)shadowType, smidx] != null && m_ShadowmapsPerType[(int)shadowType, smidx].Reserve(frameId, camera, cameraRelativeRendering, ref sd, grantedRequests[i], (uint)asd.shadowResolution, (uint)asd.shadowResolution, ref shadowmapDatas, ref shadowmapPayload, lights))
                        break;
                    smidx++;
                }
                if (smidx == k_MaxShadowmapPerType)
                {
                    Debug.LogError("The requested shadows do not fit into any shadowmap.");
                    return false;
                }
            }

            foreach (var sm in m_Shadowmaps)
            {
                if (!sm.ReserveFinalize(frameId, ref shadowmapDatas, ref shadowmapPayload))
                {
                    Debug.LogError("Shadow allocation failed in the ReserveFinalize step");
                    return false;
                }
            }

            return true;
        }

        public override void RenderShadows(FrameId frameId, ScriptableRenderContext renderContext, CommandBuffer cmd, CullResults cullResults, List<VisibleLight> lights)
        {
            using (new ProfilingSample(cmd, "Render Shadows"))
            {
                foreach (var sm in m_Shadowmaps)
                    sm.Update(frameId, renderContext, cmd, cullResults, lights);
            }
        }

        public void PostBlurExpShadows(CommandBuffer cmd, int blurMethod)
        {
            foreach (var sm in m_Shadowmaps)
            {
                ShadowAtlasExp smExp = sm as ShadowAtlasExp;
                if (smExp != null)
                    smExp.PostBlurExpShadows(cmd, blurMethod);
            }
        }

        public override void DisplayShadow(CommandBuffer cmd, Material debugMaterial, int shadowRequestIndex, uint faceIndex, float screenX, float screenY, float screenSizeX, float screenSizeY, float minValue, float maxValue, bool flipY)
        {
            if (m_ShadowIndices.Count() == 0)
                return;

            uint index = Math.Max(0, Math.Min((uint)(m_ShadowIndices.Count() - 1), (uint)shadowRequestIndex));
            int offset = (m_TmpRequests[index].facecount > 1) ? 1 : 0;
            VectorArray<ShadowData> shadowDatas = m_ShadowCtxt.shadowDatas;
            ShadowData faceData = shadowDatas[(uint)(m_ShadowIndices[index] + offset + faceIndex)];
            uint texID, samplerID;
            faceData.UnpackShadowmapId(out texID, out samplerID);
            m_Shadowmaps[texID].DisplayShadowMap(cmd, debugMaterial, faceData.scaleOffset, (uint)faceData.slice, screenX, screenY, screenSizeX, screenSizeY, minValue, maxValue, flipY);
        }

        public override void DisplayShadowMap(CommandBuffer cmd, Material debugMaterial, uint shadowMapIndex, uint sliceIndex, float screenX, float screenY, float screenSizeX, float screenSizeY, float minValue, float maxValue, bool flipY)
        {
            if (m_Shadowmaps.Length == 0)
                return;

            uint index = Math.Max(0, Math.Min((uint)(m_Shadowmaps.Length - 1), shadowMapIndex));
            m_Shadowmaps[index].DisplayShadowMap(cmd, debugMaterial, new Vector4(1.0f, 1.0f, 0.0f, 0.0f), sliceIndex, screenX, screenY, screenSizeX, screenSizeY, minValue, maxValue, flipY);
        }

        public override void SyncData()
        {
            m_ShadowCtxt.SyncData();
        }

        public override void BindResources(CommandBuffer cmd, ComputeShader computeShader, int computeKernel)
        {
            foreach (var sm in m_Shadowmaps)
                sm.Fill(m_ShadowCtxt);
            cmd.BeginSample("Bind Resources to GPU");
            m_ShadowCtxt.BindResources(cmd, computeShader, computeKernel);
            cmd.EndSample("Bind Resources to GPU");
        }

        private uint ResetMaxShadows()
        {
            int total = 0;
            for (int i = 0; i < (int)GPUShadowType.MAX; ++i)
            {
                m_MaxShadows[i, 0] = m_MaxShadows[i, 1];
                total += m_MaxShadows[i, 1];
            }

            return total > 0 ? (uint)total : 0;
        }
    }
}
