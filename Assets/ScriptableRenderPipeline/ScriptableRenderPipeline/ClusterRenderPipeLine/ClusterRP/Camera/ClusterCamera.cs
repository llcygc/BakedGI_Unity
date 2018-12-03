using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Experimental.Rendering;

#if UNITY_2017_2_OR_NEWER
using XRSettings = UnityEngine.XR.XRSettings;
#elif UNITY_5_6_OR_NEWER
    using XRSettings = UnityEngine.VR.VRSettings;
#endif

namespace Viva.Rendering.RenderGraph.ClusterPipeline
{
    public class RGCamera
    {
        static Dictionary<Camera, RGCamera> s_Cameras = new Dictionary<Camera, RGCamera>();
        static List<Camera> s_Cleanup = new List<Camera>(); // Recycled to reduce GC pressure

        Camera m_Camera;
        public Camera camera
        {
            get { return m_Camera; }
        }

        private int m_CameraWidth = 0;
        public int CameraWidth
        {
            get { return m_CameraWidth; }
        }

        private int m_CameraHeight = 0;
        public int CameraHeight
        {
            get { return m_CameraHeight; }
        }

        private int m_ClusterFrustumWidth = 0;
        public int ClusterFrustumWidth
        {
            get { return m_ClusterFrustumWidth; }
        }

        private int m_ClusterFrustumHeight = 0;
        public int ClusterFrustumHeight
        {
            get { return m_ClusterFrustumHeight; }
        }
        
        public Matrix4x4 viewMatrix;
        public Matrix4x4 projMatrix;

        public float StereoCamDist;
        public float NearClipPlane;
        public float FarClipPlane;
        public Vector4 ClusterCameraPos;
        public Matrix4x4 MVPMatrix;
        public Matrix4x4 MVPMatrixLinearZ;

        public Matrix4x4[] viewMatrixStereo = new Matrix4x4[2];
        public Matrix4x4[] projMatrixStereo = new Matrix4x4[2];
        Matrix4x4[] viewProjStereo = new Matrix4x4[2];
        Matrix4x4[] invViewStereo = new Matrix4x4[2];
        Matrix4x4[] invProjStereo = new Matrix4x4[2];
        Matrix4x4[] invViewProjStereo = new Matrix4x4[2];
        Matrix4x4[] prevViewProjMatrixStereo = new Matrix4x4[2];
        Matrix4x4[] nonJitteredViewProjMatrixStereo = new Matrix4x4[2];

        private Vector4[] cameraBottomLeftCorner = new Vector4[2];
        private Vector4 cameraUpDir;
        private Vector4 cameraRightDir;

        private Vector4 clusterBottomLeftCorner;
        private Vector4 clusterUpDir;
        private Vector4 clusterRightDir;

        public Vector4 centerEyeTranslationOffset;

        public Matrix4x4 nonJitteredProjMatrix;

        // View-projection matrix from the previous frame (non-jittered).
        public Matrix4x4 prevViewProjMatrix;

        // We need to keep track of these when camera relative rendering is enabled so we can take
        // camera translation into account when generating camera motion vectors
        public Vector3 cameraPos;
        public Vector3 prevCameraPos;
        public Vector4 worldSpaceCameraPos;
        public float detViewMatrix;
        public Vector4 screenSize;
        public Vector4 screenParam;
        public Vector4 zBufferParams;
        public Vector4 projectionParams;
        public Vector4 unity_OrthoParams;
        uint taaFrameIndex;
        Vector2 taaFrameRotation;

        public Vector4 viewportScaleBias = new Vector4(1, 1, 0, 0);

        public Vector4 doubleBufferedViewportScale { get { return new Vector4(1, 1, 1, 1); } }

        public TextureDimension RenderTextureDimension;

        public PostProcessRenderContext postProcessRenderContext;

        public bool TaaEnabled;
        public bool StereoEnabled;
        public bool IsMainCamera;
        public bool CameraResolutionChanged = true;
        public bool ClusterResolutionChanged = true;

        int m_LastFrameActive;
        public bool isFirstFrame { get; private set; }
        public bool isFirstFrameStereo { get; private set; }

        public Matrix4x4 viewProjMatrix
        {
            get { return projMatrix * viewMatrix; }
        }

        public Matrix4x4 nonJitteredViewProjMatrix
        {
            get { return nonJitteredProjMatrix * viewMatrix; }
        }

        public Matrix4x4 GetViewProjMatrixStereo(uint eyeIndex)
        {
            return (projMatrixStereo[eyeIndex] * viewMatrixStereo[eyeIndex]);
        }

        public RGCamera(Camera cam)
        {
            m_Camera = cam;

            postProcessRenderContext = new PostProcessRenderContext();

            Reset();
        }

        public void Reset()
        {
            m_LastFrameActive = -1;
            isFirstFrame = true;
            isFirstFrameStereo = true;
        }

        public static void CleanUnUsed()
        {
            int frameCheck = Time.frameCount - 1;

            foreach (var kvp in s_Cameras)
            {
                if (kvp.Value.m_LastFrameActive != frameCheck)
                    s_Cleanup.Add(kvp.Key);
            }

            foreach (var cam in s_Cleanup)
                s_Cameras.Remove(cam);

            s_Cleanup.Clear();
        }

        public static RGCamera Get(Camera camera, PostProcessLayer postProcessLayer, int alignedClusterSize, bool enablePostProcess)
        {
            RGCamera rgCam;

            if (!s_Cameras.TryGetValue(camera, out rgCam))
            {
                rgCam = new RGCamera(camera);
                s_Cameras.Add(camera, rgCam);
            }

            rgCam.Update(postProcessLayer, alignedClusterSize, enablePostProcess);

            return rgCam;
        }

        public void Update(PostProcessLayer postProcessLayer, int alignedClusterSize, bool enablePostProcess)
        {
            GetActualClusterCamParams(alignedClusterSize);

            TaaEnabled = camera.cameraType == CameraType.Game &&
                CoreUtils.IsTemporalAntialiasingActive(postProcessLayer) &&
                enablePostProcess;

            var nonJitteredCameraProj = camera.projectionMatrix;
            var cameraProj = (TaaEnabled && !StereoEnabled)
                ? postProcessLayer.temporalAntialiasing.GetJitteredProjectionMatrix(camera)
                : nonJitteredCameraProj;

            // The actual projection matrix used in shaders is actually massaged a bit to work across all platforms
            // (different Z value ranges etc.)
            var gpuProj = GL.GetGPUProjectionMatrix(cameraProj, true); // Had to change this from 'false'
            var gpuView = camera.worldToCameraMatrix;
            var gpuNonJitteredProj = GL.GetGPUProjectionMatrix(nonJitteredCameraProj, true);

            var gpuVP = gpuNonJitteredProj * gpuView;
            // In stereo, this corresponds to the center eye position
            var pos = camera.transform.position;
            worldSpaceCameraPos = pos;

            // A camera could be rendered multiple times per frame, only updates the previous view proj & pos if needed
            if (m_LastFrameActive != Time.frameCount)
            {
                if (isFirstFrame)
                {
                    prevCameraPos = pos;
                    prevViewProjMatrix = gpuVP;
                }
                else
                {
                    prevCameraPos = cameraPos;
                    prevViewProjMatrix = nonJitteredViewProjMatrix;
                }

                isFirstFrame = false;
            }

            taaFrameIndex = TaaEnabled ? (uint)postProcessLayer.temporalAntialiasing.sampleIndex : 0;
            taaFrameRotation = new Vector2(Mathf.Sin(taaFrameIndex * (0.5f * Mathf.PI)),
                                           Mathf.Cos(taaFrameIndex * (0.5f * Mathf.PI)));

            viewMatrix = gpuView;
            projMatrix = gpuProj;
            nonJitteredProjMatrix = gpuNonJitteredProj;

            float n = camera.nearClipPlane;
            float f = camera.farClipPlane;
            // Analyze the projection matrix.
            // p[2][3] = (reverseZ ? 1 : -1) * (depth_0_1 ? 1 : 2) * (f * n) / (f - n)
            float scale = projMatrix[2, 3] / (f * n) * (f - n);
            bool depth_0_1 = Mathf.Abs(scale) < 1.5f;
            bool reverseZ = scale > 0;
            bool flipProj = projMatrix.inverse.MultiplyPoint(new Vector3(0, 1, 0)).y < 0;

            // http://www.humus.name/temp/Linearize%20depth.txt
            if (reverseZ)
            {
                zBufferParams = new Vector4(-1 + f / n, 1, -1 / f + 1 / n, 1 / f);
            }
            else
            {
                zBufferParams = new Vector4(1 - f / n, f / n, 1 / f - 1 / n, 1 / n);
            }

            projectionParams = new Vector4(flipProj ? -1 : 1, n, f, 1.0f / f);

            float orthoHeight = camera.orthographic ? 2 * camera.orthographicSize : 0;
            float orthoWidth = orthoHeight * camera.aspect;
            unity_OrthoParams = new Vector4(orthoWidth, orthoHeight, 0, camera.orthographic ? 1 : 0);

            screenSize = new Vector4(CameraWidth, CameraHeight, 1.0f / CameraWidth, 1.0f / CameraHeight);
            screenParam = new Vector4(screenSize.x, screenSize.y, 1 + screenSize.z, 1 + screenSize.w);

            Vector3[] corners = new Vector3[4];
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, corners);

            for (int i = 0; i < 4; i++)
            {
                corners[i] = camera.transform.TransformVector(corners[i]);
            }

            cameraBottomLeftCorner[0] = corners[0];
            cameraUpDir = corners[1] - corners[0];
            cameraRightDir = corners[3] - corners[0];

            if (StereoEnabled)
                UpdateStereoMatrices(postProcessLayer);

            m_LastFrameActive = Time.frameCount;
        }

        private void UpdateStereoMatrices(PostProcessLayer postProcessLayer)
        {
            postProcessRenderContext.Reset();
            postProcessRenderContext.camera = camera;

            if (TaaEnabled)
                postProcessLayer.temporalAntialiasing.ConfigureStereoJitteredProjectionMatrices(postProcessRenderContext);

            for (uint eyeIndex = 0; eyeIndex < 2; eyeIndex++)
            {                
                projMatrixStereo[eyeIndex] = camera.GetStereoProjectionMatrix((Camera.StereoscopicEye)eyeIndex);
                projMatrixStereo[eyeIndex] = GL.GetGPUProjectionMatrix(projMatrixStereo[eyeIndex], true);

                var nonJitteredStereoPojMatrix = camera.GetStereoNonJitteredProjectionMatrix((Camera.StereoscopicEye)eyeIndex);
                nonJitteredStereoPojMatrix = GL.GetGPUProjectionMatrix(nonJitteredStereoPojMatrix, true);

                viewMatrixStereo[eyeIndex] = camera.GetStereoViewMatrix((Camera.StereoscopicEye)eyeIndex);
                var gpuVP = nonJitteredStereoPojMatrix * viewMatrixStereo[eyeIndex];
                // A camera could be rendered multiple times per frame, only updates the previous view proj & pos if needed
                if (m_LastFrameActive != Time.frameCount)
                {
                    if (isFirstFrameStereo)
                    {
                        prevViewProjMatrixStereo[eyeIndex] = gpuVP;
                    }
                    else
                    {
                        prevViewProjMatrixStereo[eyeIndex] = nonJitteredViewProjMatrixStereo[eyeIndex];
                    }
                    
                    isFirstFrameStereo = false;
                }

                nonJitteredViewProjMatrixStereo[eyeIndex] = gpuVP;// viewMatrixStereo[eyeIndex];
            }
        }

        private void GetActualClusterCamParams(int alignedClusterSize)
        {
            bool isStereoEnabled = XRSettings.isDeviceActive && !(m_Camera.cameraType == CameraType.SceneView) && m_Camera.stereoEnabled;
            
            //Debug.Log("Stereo Enabled: " + isStereoEnabled.ToString());

            int currentScreenWidth = isStereoEnabled ? XRSettings.eyeTextureWidth : m_Camera.pixelWidth;
            int currentScreenHeight = isStereoEnabled ? XRSettings.eyeTextureHeight : m_Camera.pixelHeight;

            if (StereoEnabled != isStereoEnabled || currentScreenWidth != CameraWidth || currentScreenHeight != CameraHeight)
            {
                CameraResolutionChanged = true;
                m_CameraWidth = currentScreenWidth;
                m_CameraHeight = currentScreenHeight;
                StereoEnabled = isStereoEnabled;
            }
            else
                CameraResolutionChanged = false;

            m_ClusterFrustumWidth = (int)Mathf.Ceil((float)CameraWidth / (float)alignedClusterSize) * alignedClusterSize;
            m_ClusterFrustumHeight = (int)Mathf.Ceil((float)CameraHeight / (float)alignedClusterSize) * alignedClusterSize;

            if (CameraResolutionChanged)
                ClusterResolutionChanged = true;
            else
                ClusterResolutionChanged = false;

            float clusterCamAspect = (float)ClusterFrustumWidth / (float)ClusterFrustumHeight;
            float dist = CameraHeight * 0.5f / Mathf.Tan(m_Camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float clusterFOV = Mathf.Atan(ClusterFrustumHeight * 0.5f / dist) * Mathf.Rad2Deg * 2.0f;

            if (StereoEnabled)
            {
                Vector3 leftCamPos = m_Camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse.MultiplyPoint(new Vector3(0, 0, 0));
                Vector3 rightCamPos = m_Camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse.MultiplyPoint(new Vector3(0, 0, 0));

                Vector3 rightPos = m_Camera.ScreenToWorldPoint(new Vector3(1, 0.5f, 500.0f), Camera.MonoOrStereoscopicEye.Right);

                float stereoCamDist = (leftCamPos - rightCamPos).magnitude;
                float height = Mathf.Tan(m_Camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * m_Camera.farClipPlane * 2.0f;
                float width = CameraWidth * height / CameraHeight + stereoCamDist;
                float cameraWidthPixel = ClusterFrustumHeight * width / height;

                m_ClusterFrustumWidth = (int)Mathf.Ceil(cameraWidthPixel / (float)alignedClusterSize) * alignedClusterSize;

                float camBackDist = m_Camera.farClipPlane / (width / stereoCamDist - 1);
                Vector3 centerCamPos = (leftCamPos + rightCamPos) / 2.0f;
                Vector3 clusterCamPos = centerCamPos - camBackDist * m_Camera.transform.forward;

                float clusterNear = m_Camera.nearClipPlane + camBackDist;
                float clusterFar = m_Camera.farClipPlane + camBackDist;

                float clusterHeight = 0.5f * height * m_ClusterFrustumHeight / m_CameraHeight;

                float tempTanHalfFOV = 0.5f * height / clusterFar;
                clusterFOV = Mathf.Atan(clusterHeight / clusterFar) * Mathf.Rad2Deg * 2.0f;
                clusterCamAspect = (float)ClusterFrustumWidth / (float)ClusterFrustumHeight;

                Matrix4x4 clusterCamViewMat = Matrix4x4.LookAt(clusterCamPos, clusterCamPos - m_Camera.transform.forward, m_Camera.transform.up).inverse;
                Matrix4x4 clusterProjMatrix = GL.GetGPUProjectionMatrix(Matrix4x4.Perspective(clusterFOV, clusterCamAspect, clusterNear, clusterFar), true);

                NearClipPlane = clusterNear;
                FarClipPlane = clusterFar;

                ClusterCameraPos = m_Camera.transform.position;
                MVPMatrix = clusterProjMatrix * clusterCamViewMat;
                clusterProjMatrix.m22 = -1;
                clusterProjMatrix.m23 = 0;
                MVPMatrixLinearZ = clusterProjMatrix * clusterCamViewMat;

                //UnityEngine.Debug.Log("Cam back dist: " + camBackDist.ToString() +
                //    "\nCamera width: " + CameraWidth.ToString() +
                //    "\nCamera width: " + m_CameraWidth.ToString() +
                //    "\nCamera aspect: " + m_Camera.aspect.ToString() +
                //    "\nCamera forward: " + m_Camera.transform.forward.ToString() +
                //    "\nCamera fov: " + m_Camera.fieldOfView.ToString() +
                //    "\nLeft camera pos: " + leftCamPos.ToString() +
                //    "\nRight camera Pos: " + rightCamPos.ToString() +
                //    "\nRight edge pos: " + rightPos.ToString() +
                //    "\nRight edge clusterPos" + MVPMatrixLinearZ.MultiplyPoint(rightPos).ToString() +
                //    "\nCluster Camera aspect: " + clusterCamAspect.ToString() +
                //    "\nCluster Camera matrix: " + MVPMatrixLinearZ.ToString() +
                //    "\nCamera width: " + cameraWidthPixel.ToString() +
                //    "\nStereo cam dist: " + stereoCamDist.ToString() +
                //    "\nCam near width: " + width.ToString() +
                //    "\nCam near: " + m_Camera.nearClipPlane.ToString() +
                //    "\nCluster far: " + clusterFar.ToString() +
                //    "\nCluster width: " + ClusterFrustumWidth.ToString() +
                //    "\nCluster height: " + ClusterFrustumHeight.ToString() +
                //    "\nCluster cam pos: " + clusterCamPos.ToString());

                StereoCamDist = stereoCamDist;

                RenderTextureDimension = XRSettings.eyeTextureDesc.dimension;
            }
            else
            {
                NearClipPlane = m_Camera.nearClipPlane;
                FarClipPlane = m_Camera.farClipPlane;// Mathf.Min(m_Camera.farClipPlane, 500.0f);
                ClusterCameraPos = m_Camera.transform.position;
                Matrix4x4 clusterCamViewMat = Matrix4x4.LookAt(m_Camera.transform.position, m_Camera.transform.position - m_Camera.transform.forward, m_Camera.transform.up).inverse;
                Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(Matrix4x4.Perspective(clusterFOV, clusterCamAspect, NearClipPlane, FarClipPlane), true);
                MVPMatrix = projectionMatrix * clusterCamViewMat;
                projectionMatrix.m22 = -1;
                projectionMatrix.m23 = 0;
                MVPMatrixLinearZ = projectionMatrix * clusterCamViewMat;

                Vector3 clusterBottomLeft = MVPMatrix.inverse.MultiplyPoint(new Vector3(-1, -1, 0));
                Vector3 clusterUpLeft = MVPMatrix.inverse.MultiplyPoint(new Vector3(-1, 1, 0));
                Vector3 clusterBottomRight = MVPMatrix.inverse.MultiplyPoint(new Vector3(1, -1, 0));

                clusterBottomLeftCorner = clusterBottomLeft;

                Vector3 clusterUpVector = (clusterUpLeft - clusterBottomLeft);
                Vector3 clusterRightVector = (clusterBottomRight - clusterBottomLeft);

                clusterUpDir = camera.transform.up;
                clusterUpDir.w = clusterUpVector.magnitude;

                clusterRightDir = camera.transform.right;
                clusterRightDir.w = clusterRightVector.magnitude;

                StereoCamDist = 0;
                RenderTextureDimension = TextureDimension.Tex2D;

            }
        }

        public void SetupGlobalParams(CommandBuffer cmd, float time, float lastTime)
        {
            cmd.SetGlobalMatrix(ClusterShaderIDs._ViewMatrix, viewMatrix);
            cmd.SetGlobalMatrix(ClusterShaderIDs._InvViewMatrix, viewMatrix.inverse);
            cmd.SetGlobalMatrix(ClusterShaderIDs._ProjMatrix, projMatrix);
            cmd.SetGlobalMatrix(ClusterShaderIDs._InvProjMatrix, projMatrix.inverse);
            cmd.SetGlobalMatrix(ClusterShaderIDs._ViewProjMatrix, viewProjMatrix);
            cmd.SetGlobalMatrix(ClusterShaderIDs._InvViewProjMatrix, viewProjMatrix.inverse);
            cmd.SetGlobalMatrix(ClusterShaderIDs._NonJitteredViewProjMatrix, nonJitteredViewProjMatrix);
            cmd.SetGlobalMatrix(ClusterShaderIDs._PrevViewProjMatrix, prevViewProjMatrix);
            cmd.SetGlobalVector(ClusterShaderIDs._WorldSpaceCameraPos, worldSpaceCameraPos);
            cmd.SetGlobalFloat(ClusterShaderIDs._DetViewMatrix, detViewMatrix);
            cmd.SetGlobalVector(ClusterShaderIDs._ScreenSize, screenSize);
            cmd.SetGlobalVector(ClusterShaderIDs._ScreenToTargetScale, doubleBufferedViewportScale);
            cmd.SetGlobalVector(ClusterShaderIDs._ZBufferParams, zBufferParams);
            cmd.SetGlobalVector(ClusterShaderIDs._ProjectionParams, projectionParams);
            cmd.SetGlobalVector(ClusterShaderIDs.unity_OrthoParams, unity_OrthoParams);
            cmd.SetGlobalVector(ClusterShaderIDs._ScreenParams, screenParam);
            cmd.SetGlobalVector(ClusterShaderIDs._TaaFrameRotation, taaFrameRotation);
            //cmd.SetGlobalVectorArray(ClusterShaderIDs._FrustumPlanes, frustumPlaneEquations);

            // Time is also a part of the UnityPerView CBuffer.
            // Different views can have different values of the "Animated Materials" setting.
            bool animateMaterials = CoreUtils.AreAnimatedMaterialsEnabled(camera);

            float ct = animateMaterials ? time : 0;
            float pt = animateMaterials ? lastTime : 0;
            float dt = Time.deltaTime;
            float sdt = Time.smoothDeltaTime;

            Vector4 timeVector = new Vector4((float)ct * 0.05f, ct, ct * 2.0f, ct * 3.0f);

            cmd.SetGlobalVector(ClusterShaderIDs._Time, timeVector);
            cmd.SetGlobalVector(ClusterShaderIDs._LastTime, new Vector4(pt * 0.05f, pt, pt * 2.0f, pt * 3.0f));
            cmd.SetGlobalVector(ClusterShaderIDs.unity_DeltaTime, new Vector4(dt, 1.0f / dt, sdt, 1.0f / sdt));
            cmd.SetGlobalVector(ClusterShaderIDs._SinTime, new Vector4(Mathf.Sin(ct * 0.125f), Mathf.Sin(ct * 0.25f), Mathf.Sin(ct * 0.5f), Mathf.Sin(ct)));
            cmd.SetGlobalVector(ClusterShaderIDs._CosTime, new Vector4(Mathf.Cos(ct * 0.125f), Mathf.Cos(ct * 0.25f), Mathf.Cos(ct * 0.5f), Mathf.Cos(ct)));

            cmd.SetGlobalMatrix("Cluster_Matrix_LinearZ", MVPMatrixLinearZ);
            cmd.SetGlobalMatrix("matrix_MVPInv", MVPMatrix.inverse);
            cmd.SetGlobalVector("ClusterProjParams", new Vector4(NearClipPlane, FarClipPlane, 0, 0));
            cmd.SetGlobalVector("ClusterScreenParams", new Vector4(ClusterFrustumWidth, ClusterFrustumHeight, 0, 0));

            cmd.SetGlobalVectorArray("CameraBottomLeft", cameraBottomLeftCorner);
            cmd.SetGlobalVector("CameraUpDirLength", cameraUpDir);
            cmd.SetGlobalVector("CameraRightDirLength", cameraRightDir);
            cmd.SetGlobalVector("ClusterBottomLeft", clusterBottomLeftCorner);
            cmd.SetGlobalVector("ClusterUpDirLength", clusterUpDir);
            cmd.SetGlobalVector("ClusterRightDirLength", clusterRightDir);

        }

        public void SetupGlobalStereoParams(CommandBuffer cmd)
        {
            for(uint eyeIndex = 0; eyeIndex < 2; eyeIndex++)
            {
                var proj = projMatrixStereo[eyeIndex];
                invProjStereo[eyeIndex] = proj.inverse;

                var view = viewMatrixStereo[eyeIndex];
                invViewStereo[eyeIndex] = view.inverse;

                viewProjStereo[eyeIndex] = proj * view;
                invViewProjStereo[eyeIndex] = viewProjStereo[eyeIndex].inverse;
            }

            // corresponds to UnityPerPassStereo
            // TODO: Migrate the other stereo matrices to HDRP-managed UnityPerPassStereo?
            cmd.SetGlobalMatrixArray(ClusterShaderIDs._ViewMatrixStereo, viewMatrixStereo);
            cmd.SetGlobalMatrixArray(ClusterShaderIDs._ViewProjMatrixStereo, viewProjStereo);
            cmd.SetGlobalMatrixArray(ClusterShaderIDs._InvViewMatrixStereo, invViewStereo);
            cmd.SetGlobalMatrixArray(ClusterShaderIDs._InvProjMatrixStereo, invProjStereo);
            cmd.SetGlobalMatrixArray(ClusterShaderIDs._InvViewProjMatrixStereo, invViewProjStereo);
            cmd.SetGlobalMatrixArray(ClusterShaderIDs._PrevViewProjMatrixStereo, prevViewProjMatrixStereo);
            cmd.SetGlobalMatrixArray(ClusterShaderIDs._NonJitteredViewProjMatrixStereo, nonJitteredViewProjMatrixStereo);
        }

        public void SetupComputeShader(ComputeShader cs, CommandBuffer cmd)
        {
            cmd.SetComputeMatrixParam(cs, "Cluster_Matrix_LinearZ", MVPMatrixLinearZ);
            cmd.SetComputeMatrixParam(cs, "matrix_MVPInv", MVPMatrix.inverse);
            cmd.SetComputeVectorParam(cs, "ClusterProjParams", new Vector4(NearClipPlane, FarClipPlane, 0, 0));
            cmd.SetComputeVectorParam(cs, "ClusterScreenParams", new Vector4(ClusterFrustumWidth, ClusterFrustumHeight, 0, 0));
            cmd.SetComputeVectorParam(cs, "CameraPos", ClusterCameraPos);
            cmd.SetComputeVectorParam(cs, ClusterShaderIDs._ScreenParams, screenParam);

            if (StereoEnabled)
                cmd.SetComputeMatrixArrayParam(cs, "_InvViewProjMatrixStereo", invViewProjStereo);
            else
                cmd.SetComputeMatrixParam(cs, "_InvViewProjMatrix", viewProjMatrix.inverse);


            cmd.SetComputeVectorArrayParam(cs, "CameraBottomLeft", cameraBottomLeftCorner);
            cmd.SetComputeVectorParam(cs, "CameraUpDirLength", cameraUpDir);
            cmd.SetComputeVectorParam(cs, "CameraRightDirLength", cameraRightDir);
            cmd.SetComputeVectorParam(cs, "ClusterBottomLeft", clusterBottomLeftCorner);
            cmd.SetComputeVectorParam(cs, "ClusterUpDirLength", clusterUpDir);
            cmd.SetComputeVectorParam(cs, "ClusterRightDirLength", clusterRightDir);

            //cmd.SetComputeMatrixParam(cs, ClusterShaderIDs._ViewMatrix, viewMatrix);
            //cmd.SetComputeMatrixParam(cs, ClusterShaderIDs._InvViewMatrix, viewMatrix.inverse);
            //cmd.SetComputeMatrixParam(cs, ClusterShaderIDs._ProjMatrix, projMatrix);
            //cmd.SetComputeMatrixParam(cs, ClusterShaderIDs._InvProjMatrix, projMatrix.inverse);
            //cmd.SetComputeMatrixParam(cs, ClusterShaderIDs._NonJitteredViewProjMatrix, nonJitteredViewProjMatrix);
            //cmd.SetComputeMatrixParam(cs, ClusterShaderIDs._ViewProjMatrix, viewProjMatrix);
            //cmd.SetComputeMatrixParam(cs, ClusterShaderIDs._InvViewProjMatrix, viewProjMatrix.inverse);
            //cmd.SetComputeVectorParam(cs, ClusterShaderIDs._InvProjParam, invProjParam);
            //cmd.SetComputeVectorParam(cs, ClusterShaderIDs._ScreenSize, screenSize);
            //cmd.SetComputeMatrixParam(cs, ClusterShaderIDs._PrevViewProjMatrix, prevViewProjMatrix);
            //cmd.SetComputeVectorArrayParam(cs, ClusterShaderIDs._FrustumPlanes, frustumPlaneEquations);
            //// Copy values set by Unity which are not configured in scripts.
            //cmd.SetComputeVectorParam(cs, ClusterShaderIDs.unity_OrthoParams, Shader.GetGlobalVector(ClusterShaderIDs.unity_OrthoParams));
            //cmd.SetComputeVectorParam(cs, ClusterShaderIDs._ProjectionParams, Shader.GetGlobalVector(ClusterShaderIDs._ProjectionParams));
            //cmd.SetComputeVectorParam(cs, ClusterShaderIDs._ScreenParams, Shader.GetGlobalVector(ClusterShaderIDs._ScreenParams));
            //cmd.SetComputeVectorParam(cs, ClusterShaderIDs._ZBufferParams, Shader.GetGlobalVector(ClusterShaderIDs._ZBufferParams));
            //cmd.SetComputeVectorParam(cs, ClusterShaderIDs._WorldSpaceCameraPos, Shader.GetGlobalVector(ClusterShaderIDs._WorldSpaceCameraPos));
        }
    }
}
