﻿#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Viva.Rendering.RenderGraph.ClusterPipeline;
#endif

namespace UnityEditor.Experimental.Rendering.ClusterPipeline
{
#if UNITY_EDITOR
    public static class ClusterLightEditorUtilities
    {
        public static void DrawSpotlightGizmo(Light spotLight, bool selected)
        {
            var flatRadiusAtRange = spotLight.range * Mathf.Tan(spotLight.spotAngle * Mathf.Deg2Rad * 0.5f);

            var vectorLineUp = Vector3.Normalize(spotLight.gameObject.transform.position + spotLight.gameObject.transform.forward * spotLight.range + spotLight.gameObject.transform.up * flatRadiusAtRange - spotLight.gameObject.transform.position);
            var vectorLineDown = Vector3.Normalize(spotLight.gameObject.transform.position + spotLight.gameObject.transform.forward * spotLight.range + spotLight.gameObject.transform.up * -flatRadiusAtRange - spotLight.gameObject.transform.position);
            var vectorLineRight = Vector3.Normalize(spotLight.gameObject.transform.position + spotLight.gameObject.transform.forward * spotLight.range + spotLight.gameObject.transform.right * flatRadiusAtRange - spotLight.gameObject.transform.position);
            var vectorLineLeft = Vector3.Normalize(spotLight.gameObject.transform.position + spotLight.gameObject.transform.forward * spotLight.range + spotLight.gameObject.transform.right * -flatRadiusAtRange - spotLight.gameObject.transform.position);

            var rangeDiscDistance = Mathf.Cos(Mathf.Deg2Rad * spotLight.spotAngle / 2) * spotLight.range;
            var rangeDiscRadius = spotLight.range * Mathf.Sin(spotLight.spotAngle * Mathf.Deg2Rad * 0.5f);
            var nearDiscDistance = Mathf.Cos(Mathf.Deg2Rad * spotLight.spotAngle / 2) * spotLight.shadowNearPlane;
            var nearDiscRadius = spotLight.shadowNearPlane * Mathf.Sin(spotLight.spotAngle * Mathf.Deg2Rad * 0.5f);

            //Draw Range Disc
            Handles.Disc(spotLight.gameObject.transform.rotation, spotLight.gameObject.transform.position + spotLight.gameObject.transform.forward * rangeDiscDistance, spotLight.gameObject.transform.forward, rangeDiscRadius, false, 1);

            //Draw Lines
            Gizmos.DrawLine(spotLight.gameObject.transform.position, spotLight.gameObject.transform.position + vectorLineUp * spotLight.range);
            Gizmos.DrawLine(spotLight.gameObject.transform.position, spotLight.gameObject.transform.position + vectorLineDown * spotLight.range);
            Gizmos.DrawLine(spotLight.gameObject.transform.position, spotLight.gameObject.transform.position + vectorLineRight * spotLight.range);
            Gizmos.DrawLine(spotLight.gameObject.transform.position, spotLight.gameObject.transform.position + vectorLineDown * spotLight.range);

            if(selected)
            {
                //Draw Range Arcs
                Handles.DrawWireArc(spotLight.gameObject.transform.position, spotLight.gameObject.transform.right, vectorLineUp, spotLight.spotAngle, spotLight.range);
                Handles.DrawWireArc(spotLight.gameObject.transform.position, spotLight.gameObject.transform.up, vectorLineLeft, spotLight.spotAngle, spotLight.range);

                //Draw Near Plane Disc
                if (spotLight.shadows != LightShadows.None) Handles.Disc(spotLight.gameObject.transform.rotation, spotLight.gameObject.transform.position + spotLight.gameObject.transform.forward * nearDiscDistance, spotLight.gameObject.transform.forward, nearDiscRadius, false, 1);

                //Inner Cone
                var additionalLightData = spotLight.GetComponent<ClusterAdditionalLightData>();
                DrawInnerCone(spotLight, additionalLightData);
            }
        }

        public static void DrawInnerCone(Light spotlight, ClusterAdditionalLightData additionalLightData)
        {
            if (additionalLightData == null) return;

            var flatRadiusAtRange = spotlight.range * Mathf.Tan(spotlight.spotAngle * additionalLightData.m_InnerSpotPercent * 0.01f * Mathf.Deg2Rad * 0.5f);

            var vectorLineUp = Vector3.Normalize(spotlight.gameObject.transform.position + spotlight.gameObject.transform.forward * spotlight.range + spotlight.gameObject.transform.up * flatRadiusAtRange - spotlight.gameObject.transform.position);
            var vectorLineDown = Vector3.Normalize(spotlight.gameObject.transform.position + spotlight.gameObject.transform.forward * spotlight.range + spotlight.gameObject.transform.up * -flatRadiusAtRange - spotlight.gameObject.transform.position);
            var vectorLineRight = Vector3.Normalize(spotlight.gameObject.transform.position + spotlight.gameObject.transform.forward * spotlight.range + spotlight.gameObject.transform.right * flatRadiusAtRange - spotlight.gameObject.transform.position);
            var vectorLineLeft = Vector3.Normalize(spotlight.gameObject.transform.position + spotlight.gameObject.transform.forward * spotlight.range + spotlight.gameObject.transform.right * -flatRadiusAtRange - spotlight.gameObject.transform.position);

            //Draw Lines

            Gizmos.DrawLine(spotlight.gameObject.transform.position, spotlight.gameObject.transform.position + vectorLineUp * spotlight.range);
            Gizmos.DrawLine(spotlight.gameObject.transform.position, spotlight.gameObject.transform.position + vectorLineDown * spotlight.range);
            Gizmos.DrawLine(spotlight.gameObject.transform.position, spotlight.gameObject.transform.position + vectorLineRight * spotlight.range);
            Gizmos.DrawLine(spotlight.gameObject.transform.position, spotlight.gameObject.transform.position + vectorLineLeft * spotlight.range);

            var innerAngle = spotlight.spotAngle * additionalLightData.GetInnerSpotPercent01();
            if (innerAngle > 0)
            {
                var innerDiscDistance = Mathf.Cos(Mathf.Deg2Rad * innerAngle * 0.5f) * spotlight.range;
                var innerDiscRadius = spotlight.range * Mathf.Sin(innerAngle * Mathf.Deg2Rad * 0.5f);
                //Draw Range disc
                Handles.Disc(spotlight.gameObject.transform.rotation, spotlight.gameObject.transform.position + spotlight.gameObject.transform.forward * innerDiscDistance, spotlight.gameObject.transform.forward, innerDiscRadius, false, 1);
            }
        }

        public static void DrawArealightGizmo(Light arealight)
        {

            var RectangleSize = new Vector3(arealight.areaSize.x, arealight.areaSize.y, 0);
            Gizmos.matrix = arealight.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, RectangleSize);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(arealight.transform.position, arealight.range);
        }

        public static void DrawPointlightGizmo(Light pointlight, bool selected)
        {
            if (pointlight.shadows != LightShadows.None && selected) Gizmos.DrawWireSphere(pointlight.transform.position, pointlight.shadowNearPlane);
            Gizmos.DrawWireSphere(pointlight.transform.position, pointlight.range);
        }

        public static void DrawSpherelightGizmo(Light spherelight)
        {
            var additionalLightData = spherelight.GetComponent<ClusterAdditionalLightData>();
            if (additionalLightData == null) return;
            Gizmos.DrawSphere(spherelight.transform.position, additionalLightData.shapeLength);
            if (spherelight.shadows != LightShadows.None) Gizmos.DrawWireSphere(spherelight.transform.position, spherelight.shadowNearPlane);
            Gizmos.DrawWireSphere(spherelight.transform.position, spherelight.range);
        }

        // Same as Gizmo.DrawFrustum except that when aspect is below one, fov represent fovX instead of fovY
        // Use to match our light frustum pyramid behavior
        public static void DrawLightPyramidFrustum(Vector3 center, float fov, float maxRange, float minRange, float aspect)
        {
            fov = Mathf.Deg2Rad * fov * 0.5f;
            float tanfov = Mathf.Tan(fov);
            Vector3 farEnd = new Vector3(0, 0, maxRange);
            Vector3 endSizeX;
            Vector3 endSizeY;

            if (aspect >= 1.0f)
            {
                endSizeX = new Vector3(maxRange * tanfov * aspect, 0, 0);
                endSizeY = new Vector3(0, maxRange * tanfov, 0);
            }
            else
            {
                endSizeX = new Vector3(maxRange * tanfov, 0, 0);
                endSizeY = new Vector3(0, maxRange * tanfov / aspect, 0);
            }

            Vector3 s1, s2, s3, s4;
            Vector3 e1 = farEnd + endSizeX + endSizeY;
            Vector3 e2 = farEnd - endSizeX + endSizeY;
            Vector3 e3 = farEnd - endSizeX - endSizeY;
            Vector3 e4 = farEnd + endSizeX - endSizeY;
            if (minRange <= 0.0f)
            {
                s1 = s2 = s3 = s4 = center;
            }
            else
            {
                Vector3 startSizeX;
                Vector3 startSizeY;
                if (aspect >= 1.0f)
                {
                    startSizeX = new Vector3(minRange * tanfov * aspect, 0, 0);
                    startSizeY = new Vector3(0, minRange * tanfov, 0);
                }
                else
                {
                    startSizeY = new Vector3(minRange * tanfov / aspect, 0, 0);
                    startSizeX = new Vector3(0, minRange * tanfov, 0);
                }
                Vector3 startPoint = center;
                s1 = startPoint + startSizeX + startSizeY;
                s2 = startPoint - startSizeX + startSizeY;
                s3 = startPoint - startSizeX - startSizeY;
                s4 = startPoint + startSizeX - startSizeY;
                Gizmos.DrawLine(s1, s2);
                Gizmos.DrawLine(s2, s3);
                Gizmos.DrawLine(s3, s4);
                Gizmos.DrawLine(s4, s1);
            }

            Gizmos.DrawLine(e1, e2);
            Gizmos.DrawLine(e2, e3);
            Gizmos.DrawLine(e3, e4);
            Gizmos.DrawLine(e4, e1);

            Gizmos.DrawLine(s1, e1);
            Gizmos.DrawLine(s2, e2);
            Gizmos.DrawLine(s3, e3);
            Gizmos.DrawLine(s4, e4);
        }

        public static void DrawLightOrthoFrustum(Vector3 center, float width, float height, float maxRange, float minRange)
        {
            Vector3 farEnd = new Vector3(0, 0, maxRange);
            Vector3 endSizeX = new Vector3(width, 0, 0);
            Vector3 endSizeY = new Vector3(0, height, 0);

            Vector3 s1, s2, s3, s4;
            Vector3 e1 = farEnd + endSizeX + endSizeY;
            Vector3 e2 = farEnd - endSizeX + endSizeY;
            Vector3 e3 = farEnd - endSizeX - endSizeY;
            Vector3 e4 = farEnd + endSizeX - endSizeY;
            if (minRange <= 0.0f)
            {
                s1 = s2 = s3 = s4 = center;
            }
            else
            {
                Vector3 startSizeX = new Vector3(width, 0, 0);
                Vector3 startSizeY = new Vector3(0, height, 0);

                Vector3 startPoint = center;
                s1 = startPoint + startSizeX + startSizeY;
                s2 = startPoint - startSizeX + startSizeY;
                s3 = startPoint - startSizeX - startSizeY;
                s4 = startPoint + startSizeX - startSizeY;
                Gizmos.DrawLine(s1, s2);
                Gizmos.DrawLine(s2, s3);
                Gizmos.DrawLine(s3, s4);
                Gizmos.DrawLine(s4, s1);
            }

            Gizmos.DrawLine(e1, e2);
            Gizmos.DrawLine(e2, e3);
            Gizmos.DrawLine(e3, e4);
            Gizmos.DrawLine(e4, e1);

            Gizmos.DrawLine(s1, e1);
            Gizmos.DrawLine(s2, e2);
            Gizmos.DrawLine(s3, e3);
            Gizmos.DrawLine(s4, e4);
        }


        public static void DrawFrustumlightGizmo(Light frustumlight)
        {
            var additionalLightData = frustumlight.GetComponent<ClusterAdditionalLightData>();
            if (additionalLightData == null) return;

            Matrix4x4 matrix = new Matrix4x4(frustumlight.transform.right, frustumlight.transform.up, frustumlight.transform.forward, frustumlight.transform.position);
            Gizmos.matrix = matrix;
            if (additionalLightData.spotLightShape == SpotLightShape.Pyramid)
            {
                DrawLightPyramidFrustum(Vector3.zero, frustumlight.spotAngle, frustumlight.range, 0.0f, additionalLightData.aspectRatio);
            }
            else // Ortho frustum
            {
                //DrawLightOrthoFrustum(Vector3.zero, additionalLightData.shapeLength, additionalLightData.shapeWidth, frustumlight.range, 0.0f);

                Vector3 frustumCenter = new Vector3(0.0f, 0.0f, 0.5f * frustumlight.range);
                Vector3 frustumsize = new Vector3(additionalLightData.shapeLength, additionalLightData.shapeWidth, frustumlight.range);
                Gizmos.DrawWireCube(frustumCenter, frustumsize);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }

        public static void DrawDirectionalLightGizmo(Light directionalLight)
        {
            var gizmoSize = 0.2f;
            Handles.Disc(directionalLight.transform.rotation, directionalLight.transform.position, directionalLight.gameObject.transform.forward, gizmoSize, false, 1);
            Gizmos.DrawLine(directionalLight.transform.position, directionalLight.transform.position + directionalLight.transform.forward);
            Gizmos.DrawLine(directionalLight.transform.position + directionalLight.transform.up * gizmoSize, directionalLight.transform.position + directionalLight.transform.up * gizmoSize + directionalLight.transform.forward);
            Gizmos.DrawLine(directionalLight.transform.position + directionalLight.transform.up * -gizmoSize, directionalLight.transform.position + directionalLight.transform.up * -gizmoSize + directionalLight.transform.forward);
            Gizmos.DrawLine(directionalLight.transform.position + directionalLight.transform.right * gizmoSize, directionalLight.transform.position + directionalLight.transform.right * gizmoSize + directionalLight.transform.forward);
            Gizmos.DrawLine(directionalLight.transform.position + directionalLight.transform.right * -gizmoSize, directionalLight.transform.position + directionalLight.transform.right * -gizmoSize + directionalLight.transform.forward);
        }

        public static void DrawDirectionalStaticShadowBoxGizmo(Light directionalLight, bool selected)
        {
            if(selected)
            {
                Gizmos.color = new Color(0, 0.3f, 1.0f, 0.5f);
                Gizmos.DrawCube(directionalLight.transform.position, directionalLight.transform.localScale);
            }
            Gizmos.color = new Color(1.0f, 1.0f, 0.0f, 0.8f);
            Gizmos.DrawWireCube(directionalLight.transform.position, directionalLight.transform.localScale);
        }

        public static void DrawCross(Transform m_transform)
        {
            var gizmoSize = 0.25f;
            Gizmos.DrawLine(m_transform.position, m_transform.position + m_transform.TransformVector(m_transform.root.forward * gizmoSize / m_transform.localScale.z));
            Gizmos.DrawLine(m_transform.position, m_transform.position + m_transform.TransformVector(m_transform.root.forward * -gizmoSize / m_transform.localScale.z));
            Gizmos.DrawLine(m_transform.position, m_transform.position + m_transform.TransformVector(m_transform.root.up * gizmoSize / m_transform.localScale.y));
            Gizmos.DrawLine(m_transform.position, m_transform.position + m_transform.TransformVector(m_transform.root.up * -gizmoSize / m_transform.localScale.y));
            Gizmos.DrawLine(m_transform.position, m_transform.position + m_transform.TransformVector(m_transform.root.right * gizmoSize / m_transform.localScale.x));
            Gizmos.DrawLine(m_transform.position, m_transform.position + m_transform.TransformVector(m_transform.root.right * -gizmoSize / m_transform.localScale.x));
        }
    }
#endif
}
