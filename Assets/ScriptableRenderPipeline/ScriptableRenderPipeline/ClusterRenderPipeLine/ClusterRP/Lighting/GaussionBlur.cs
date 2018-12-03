using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Viva.Rendering.RenderGraph
{
    public class GaussionBlur
    {

        // Assuming a isoceles right angled triangle of height "triangleHeight" (as drawn below).
        // This function return the area of the triangle above the first texel.
        //
        // |\      <-- 45 degree slop isosceles right angled triangle
        // | \
        // ----    <-- length of this side is "triangleHeight"
        // _ _ _ _ <-- texels
        static float SampleShadow_GetTriangleTexelArea(float triangleHeight)
        {
            return triangleHeight - 0.5f;
        }


        // Assuming a isoceles triangle of 1.5 texels height and 3 texels wide lying on 4 texels.
        // This function return the area of the triangle above each of those texels.
        //    |    <-- offset from -0.5 to 0.5, 0 meaning triangle is exactly in the center
        //   / \   <-- 45 degree slop isosceles triangle (ie tent projected in 2D)
        //  /   \
        // _ _ _ _ <-- texels
        // X Y Z W <-- result indices (in computedArea.xyzw and computedAreaUncut.xyzw)
        static void SampleShadow_GetTexelAreas_Tent_3x3(float offset, out Vector4 computedArea, out Vector4 computedAreaUncut)
        {
            // Compute the exterior areas
            float offset01SquaredHalved = (offset + 0.5f) * (offset + 0.5f) * 0.5f;
            computedAreaUncut.x = computedArea.x = offset01SquaredHalved - offset;
            computedAreaUncut.w = computedArea.w = offset01SquaredHalved;

            // Compute the middle areas
            // For Y : We find the area in Y of as if the left section of the isoceles triangle would
            // intersect the axis between Y and Z (ie where offset = 0).
            computedAreaUncut.y = SampleShadow_GetTriangleTexelArea(1.5f - offset);
            // This area is superior to the one we are looking for if (offset < 0) thus we need to
            // subtract the area of the triangle defined by (0,1.5-offset), (0,1.5+offset), (-offset,1.5).
            float clampedOffsetLeft = Mathf.Min(offset, 0);
            float areaOfSmallLeftTriangle = clampedOffsetLeft * clampedOffsetLeft;
            computedArea.y = computedAreaUncut.y - areaOfSmallLeftTriangle;

            // We do the same for the Z but with the right part of the isoceles triangle
            computedAreaUncut.z = SampleShadow_GetTriangleTexelArea(1.5f + offset);
            float clampedOffsetRight = Mathf.Max(offset, 0);
            float areaOfSmallRightTriangle = clampedOffsetRight * clampedOffsetRight;
            computedArea.z = computedAreaUncut.z - areaOfSmallRightTriangle;
        }

        // Assuming a isoceles triangle of 3.5 texel height and 7 texels wide lying on 8 texels.
        // This function return the weight of each texels area relative to the full triangle area.
        //  /           \
        // _ _ _ _ _ _ _ _ <-- texels
        // 0 1 2 3 4 5 6 7 <-- computed area indices (in texelsWeights[])
        static void SampleShadow_GetTexelWeights_Tent_7x7(float offset, out Vector4 texelsWeightsA, out Vector4 texelsWeightsB)
        {
            // See _UnityInternalGetAreaPerTexel_3TexelTriangleFilter for details.
            Vector4 computedArea_From3texelTriangle = Vector4.zero;
            Vector4 computedAreaUncut_From3texelTriangle = Vector4.zero;
            SampleShadow_GetTexelAreas_Tent_3x3(offset, out computedArea_From3texelTriangle, out computedAreaUncut_From3texelTriangle);

            // Triangle slope is 45 degree thus we can almost reuse the result of the 3 texel wide computation.
            // the 7 texel wide triangle can be seen as the 3 texel wide one but shifted up by two unit/texel.
            // 0.081632 is 1/(the triangle area)
            texelsWeightsA.x = 0.081632f * (computedArea_From3texelTriangle.x);
            texelsWeightsA.y = 0.081632f * (computedAreaUncut_From3texelTriangle.y);
            texelsWeightsA.z = 0.081632f * (computedAreaUncut_From3texelTriangle.y + 1);
            texelsWeightsA.w = 0.081632f * (computedArea_From3texelTriangle.y + 2);
            texelsWeightsB.x = 0.081632f * (computedArea_From3texelTriangle.z + 2);
            texelsWeightsB.y = 0.081632f * (computedAreaUncut_From3texelTriangle.z + 1);
            texelsWeightsB.z = 0.081632f * (computedAreaUncut_From3texelTriangle.z);
            texelsWeightsB.w = 0.081632f * (computedArea_From3texelTriangle.w);
        }

        // 7x7 Tent filter (45 degree sloped triangles in U and V)
        public static void SampleShadow_ComputeSamples_Tent_7x7(Vector4 shadowMapTexture_TexelSize, out Vector4[] fetchesUVWeights)
        {
            // tent base is 7x7 base thus covering from 49 to 64 texels, thus we need 16 bilinear PCF fetches
            //Vector2 centerOfFetchesInTexelSpace = Mathf.Floor(tentCenterInTexelSpace + new Vector2(0.5f, 0.5f));
            Vector2 offsetFromTentCenterToCenterOfFetches = Vector2.zero; //tentCenterInTexelSpace - centerOfFetchesInTexelSpace;

            // find the weight of each texel based on the area of a 45 degree slop tent above each of them.
            Vector4 texelsWeightsU_A = Vector2.zero, texelsWeightsU_B = Vector2.zero;
            Vector4 texelsWeightsV_A = Vector2.zero, texelsWeightsV_B = Vector2.zero;
            SampleShadow_GetTexelWeights_Tent_7x7(offsetFromTentCenterToCenterOfFetches.x, out texelsWeightsU_A, out texelsWeightsU_B);
            SampleShadow_GetTexelWeights_Tent_7x7(offsetFromTentCenterToCenterOfFetches.y, out texelsWeightsV_A, out texelsWeightsV_B);

            // each fetch will cover a group of 2x2 texels, the weight of each group is the sum of the weights of the texels
            Vector4 fetchesWeightsU = new Vector4(texelsWeightsU_A.x, texelsWeightsU_A.z, texelsWeightsU_B.x, texelsWeightsU_B.z) + new Vector4(texelsWeightsU_A.y, texelsWeightsU_A.w, texelsWeightsU_B.y, texelsWeightsU_B.w);
            Vector4 fetchesWeightsV = new Vector4(texelsWeightsV_A.x, texelsWeightsV_A.z, texelsWeightsV_B.x, texelsWeightsV_B.z) + new Vector4(texelsWeightsV_A.y, texelsWeightsV_A.w, texelsWeightsV_B.y, texelsWeightsV_B.w);

            // move the PCF bilinear fetches to respect texels weights
            Vector4 fetchesOffsetsU = new Vector4(texelsWeightsU_A.y / fetchesWeightsU.x, texelsWeightsU_A.w / fetchesWeightsU.y, texelsWeightsU_B.y / fetchesWeightsU.z, texelsWeightsV_B.w / fetchesWeightsU.w) + new Vector4(-3.5f, -1.5f, 0.5f, 2.5f);
            Vector4 fetchesOffsetsV = new Vector4(texelsWeightsV_A.y / fetchesWeightsV.x, texelsWeightsV_A.w / fetchesWeightsV.y, texelsWeightsV_B.y / fetchesWeightsV.z, texelsWeightsV_B.w / fetchesWeightsV.w) + new Vector4(-3.5f, -1.5f, 0.5f, 2.5f);
            fetchesOffsetsU *= shadowMapTexture_TexelSize.x;
            fetchesOffsetsV *= shadowMapTexture_TexelSize.y;

            Vector2[] fetchesUV = new Vector2[16];
            float[] fetchesWeights = new float[16];

            fetchesUV[0] = new Vector2(fetchesOffsetsU.x, fetchesOffsetsV.x);
            fetchesUV[1] = new Vector2(fetchesOffsetsU.y, fetchesOffsetsV.x);
            fetchesUV[2] = new Vector2(fetchesOffsetsU.z, fetchesOffsetsV.x);
            fetchesUV[3] = new Vector2(fetchesOffsetsU.w, fetchesOffsetsV.x);
            fetchesUV[4] = new Vector2(fetchesOffsetsU.x, fetchesOffsetsV.y);
            fetchesUV[5] = new Vector2(fetchesOffsetsU.y, fetchesOffsetsV.y);
            fetchesUV[6] = new Vector2(fetchesOffsetsU.z, fetchesOffsetsV.y);
            fetchesUV[7] = new Vector2(fetchesOffsetsU.w, fetchesOffsetsV.y);
            fetchesUV[8] = new Vector2(fetchesOffsetsU.x, fetchesOffsetsV.z);
            fetchesUV[9] = new Vector2(fetchesOffsetsU.y, fetchesOffsetsV.z);
            fetchesUV[10] = new Vector2(fetchesOffsetsU.z, fetchesOffsetsV.z);
            fetchesUV[11] = new Vector2(fetchesOffsetsU.w, fetchesOffsetsV.z);
            fetchesUV[12] = new Vector2(fetchesOffsetsU.x, fetchesOffsetsV.w);
            fetchesUV[13] = new Vector2(fetchesOffsetsU.y, fetchesOffsetsV.w);
            fetchesUV[14] = new Vector2(fetchesOffsetsU.z, fetchesOffsetsV.w);
            fetchesUV[15] = new Vector2(fetchesOffsetsU.w, fetchesOffsetsV.w);

            fetchesWeights[0] = fetchesWeightsU.x * fetchesWeightsV.x;
            fetchesWeights[1] = fetchesWeightsU.y * fetchesWeightsV.x;
            fetchesWeights[2] = fetchesWeightsU.z * fetchesWeightsV.x;
            fetchesWeights[3] = fetchesWeightsU.w * fetchesWeightsV.x;
            fetchesWeights[4] = fetchesWeightsU.x * fetchesWeightsV.y;
            fetchesWeights[5] = fetchesWeightsU.y * fetchesWeightsV.y;
            fetchesWeights[6] = fetchesWeightsU.z * fetchesWeightsV.y;
            fetchesWeights[7] = fetchesWeightsU.w * fetchesWeightsV.y;
            fetchesWeights[8] = fetchesWeightsU.x * fetchesWeightsV.z;
            fetchesWeights[9] = fetchesWeightsU.y * fetchesWeightsV.z;
            fetchesWeights[10] = fetchesWeightsU.z * fetchesWeightsV.z;
            fetchesWeights[11] = fetchesWeightsU.w * fetchesWeightsV.z;
            fetchesWeights[12] = fetchesWeightsU.x * fetchesWeightsV.w;
            fetchesWeights[13] = fetchesWeightsU.y * fetchesWeightsV.w;
            fetchesWeights[14] = fetchesWeightsU.z * fetchesWeightsV.w;
            fetchesWeights[15] = fetchesWeightsU.w * fetchesWeightsV.w;

            Vector4[] outFetchesUVWeights = new Vector4[16];
            for (int i = 0; i < 16; i++)
            {
                outFetchesUVWeights[i] = new Vector4(fetchesUV[i].x, fetchesUV[i].y, fetchesWeights[i], 0);
            }

            fetchesUVWeights = outFetchesUVWeights;
        }
    }
}
