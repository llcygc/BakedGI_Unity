#include "GI_Data.hlsl"

TEXTURE2D_ARRAY(RadMapOctan);
TEXTURE2D_ARRAY(DistMapOctan);
TEXTURE2D_ARRAY(NormalMapOctan);

#define RED half3(1, 0, 0)
#define GREEN half3(0, 1, 0)
#define BLUE half3(0, 0, 1)

const float minThickness = 0.03; // meters
const float maxThickness = 0.50; // meters

//TRACE_HIT 0
//TRACE_MISS 1
//TRACE_UNKNONW 2
uint TraceSingleProbe(uint index, float3 worldPos, float3 dir, inout float tMin, inout float tMax, out float2 hitUV, out float3 hitNormal, out half3 debugColor)
{
    ProbeData pData = ProbeDataBuffer[index];
    float3 localPos = worldPos - pData.position;
    debugColor = 0;

    float boundaryTs[5];

    boundaryTs[0] = 0.0f;

    float3 t = localPos * -(1.0f / dir);
    sort(t);

    for (int i = 0; i < 3; ++i)
    {
        boundaryTs[i + 1] = clamp(t[i], 0.0f, 1000.0f);
    }

    boundaryTs[4] = 1000.0f;

    const float degenerateEpsilon = 0.001f;

    for (int i = 0; i < 4; i++)
    {
        float t0 = boundaryTs[i];
        float t1 = boundaryTs[i + 1];
        if (abs(t0 - t1) >= degenerateEpsilon)
        {
            float3 startPoint = localPos + dir * (t0 + rayBumpEpsilon);
            float3 endPoint = localPos + dir * (t1 + rayBumpEpsilon);

            if (sqrLength(startPoint) < 0.001)
                startPoint = dir;

            startPoint.y *= -1;
            endPoint.y *= -1;

            float2 startUV = octEncode(normalize(startPoint)) * 0.5 + 0.5;
            float2 endUV = octEncode(normalize(endPoint)) * 0.5 + 0.5;
            

            float2 startCoord = startUV * CubeOctanResolution.x;
            float2 endCoord = endUV * CubeOctanResolution.x;

            endUV += sqrLength(startUV - endUV) > 0.0001 ? 0.01 : 0.0;
            float2 delta = endUV - startUV;
            float2 traceDir = normalize(delta);
            float dist = length(delta);

            float traceStep = dist / max(abs(delta.x * CubeOctanResolution.x), abs(delta.y * CubeOctanResolution.y));
            
            float3 dirBefore = octDecode((startUV) * 2.0 - 1.0);
            dirBefore.y *= -1;
            float distBefore = max(0.0, distanceToIntersection(worldPos, dir, dirBefore));

            float traceDist = 0;
            while (traceDist < dist)
            {                
                float2 currentUV = startUV + traceDir * min(traceDist + traceStep * 0.5, dist);
                int2 currentCoord = (int2) (currentUV * CubeOctanResolution.xy);
                float sceneDist = LOAD_TEXTURE2D_ARRAY(DistMapOctan, currentCoord, index) * ProbeProjectonParam.y;
                                
                float2 afterUV = startUV + traceDir * min(traceDist + traceStep, dist);
                float3 dirAfter = octDecode(currentUV * 2.0 - 1.0);
                dirAfter.y *= -1;
                float distAfter = max(0.0, distanceToIntersection(worldPos, dir, dirAfter));

                float maxRayDist = max(distBefore, distAfter);
                if(maxRayDist >= sceneDist)
                {                
                    float minRayDist = min(distBefore, distAfter);
                    
                    float distanceFromProbeToRay = (minRayDist + maxRayDist) * 0.5;                    
                    float3 directionFromProbe = octDecode(currentUV * 2.0 - 1.0);
                    directionFromProbe.y *= -1;

                    float3 probeSpaceHitPoint = sceneDist * directionFromProbe;
                    float distAlongRay = dot(probeSpaceHitPoint - worldPos, dir);

                    float3 normal = LOAD_TEXTURE2D_ARRAY(NormalMapOctan, currentCoord, index);
                    // Only extrude towards and away from the view ray, not perpendicular to it
                    // Don't allow extrusion TOWARDS the viewer, only away
                    float surfaceThickness = minThickness
                    + (maxThickness - minThickness) *

                    // Alignment of probe and view ray
                    max(dot(dir, directionFromProbe), 0.0) *

                    // Alignment of probe and normal (glancing surfaces are assumed to be thicker because they extend into the pixel)
                    (2 - abs(dot(dir, normal))) *

                    // Scale with distance along the ray
                    clamp(distAlongRay * 0.1, 0.05, 1.0);
                                        
                    if ((minRayDist < sceneDist/* + surfaceThickness*/) /*&& (dot(normal, dir) < 0)*/)
                    {
                        debugColor = maxRayDist / ProbeProjectonParam.y; //half3(currentUV, 0);
                        // Two-sided hit
                        // Use the probe's measure of the point instead of the ray distance, since
                        // the probe is more accurate (floating point precision vs. ray march iteration/oct resolution)
                        tMax = distAlongRay;
                        hitUV = currentUV;
                        hitNormal = normal;
                        return TRACE_HIT;
                    }
                    else
                    {
                        // "Unknown" case. The ray passed completely behind a surface. This should trigger moving to another
                        // probe and is distinguished from "I successfully traced to infinity"
                
                        // Back up conservatively so that we don't set tMin too large
                        float3 probeSpaceHitPointBefore = distBefore * dirBefore;
                        float distAlongRayBefore = dot(probeSpaceHitPointBefore - worldPos, dir);
                
                        // Max in order to disallow backing up along the ray (say if beginning of this texel is before tMin from probe switch)
                        // distAlongRayBefore in order to prevent overstepping
                        // min because sometimes distAlongRayBefore > distAlongRay
                        tMin = max(tMin, min(distAlongRay, distAlongRayBefore));

                        return TRACE_UNKNONW;
                    }
                }

                distBefore = distAfter;
                dirBefore = dirAfter;
                traceDist += traceStep;
            }
        }
    }

    return TRACE_MISS;
}

half3 GlobalIllumination_Trace(BRDFData_Direct brdfData, half3 normalWS, half3 tangent, half3 binormal, half3 viewDirectionWS, float3 worldPos)
{
	uint4 indices1 = 0;
	uint4 indices2 = 0;

	FetchProbeIndices(worldPos, indices1, indices2);
	uint indices[8] = { indices1.x, indices1.y, indices1.z, indices1.w, indices2.x, indices2.y, indices2.z, indices2.w };

	half3 color = 0;

	int count = 0;
	float step = 0.5f;
    for (int i = 0; i < 1/*SAMPLE_COUNT*/; i++)
	{
		float divX = floor(i / SAMPLE_DIM) + 1;
		float divY = i % SAMPLE_DIM + 1;
		float theta = 2 * PI * (divX / (SAMPLE_DIM + 1));
		float phi = 0.5 * PI * (divY / (SAMPLE_DIM + 1));

		float x = sin(phi) * cos(theta);
		float y = sin(phi) * sin(theta);
		float z = cos(phi);

		float3 dir = normalWS/* * z + binormal * y + tangent * x*/;
        half3 skyColor;
        
        SortProbes(worldPos, dir, indices);
        
        //TRACE_HIT 0
        //TRACE_MISS 1
        //TRACE_UNKNONW 2
        uint result = TRACE_UNKNONW;
        bool traceComplete = false;
        half3 radColor = 0;

        float tMin = 0.0f;
        float tMax = 1000.0f;
        float2 hitUV = 0;
        float3 normal = 0;
        half3 debugColor = 0;

        uint hitIndex;
        
        for (uint j = 0; j < 8; j++)
        {
            hitIndex = indices[j];
            result = TraceSingleProbe(indices[j], worldPos, dir, tMin, tMax, hitUV, normal, debugColor);

            if (result != TRACE_UNKNONW)
                break;
        }

        if (result == TRACE_HIT)
        {
            color += debugColor;//LOAD_TEXTURE2D_ARRAY(RadMapOctan, hitUV * CubeOctanResolution.xy, hitIndex);
        }
        
        //uint destIndex = indices[1];
        //ProbeData pData = ProbeDataBuffer[destIndex];
        //float3 localPos = worldPos - pData.position;
        //localPos.y *= -1;
        //float2 startUV = octEncode(normalize(localPos)) * 0.5 + 0.5;
        //float2 startCoord = startUV * CubeOctanResolution.xy;
        //color += LOAD_TEXTURE2D_ARRAY(RadMapOctan, startCoord, destIndex);

        //color = (float)j / 8;

  //      uint closestIndex = FetchClosestProbe(worldPos, dir, indices);

  //      ProbeData pData = ProbeDataBuffer[closestIndex];
		
		//bool hit = false;
		//float firstLength = 0;
		//float lastLength = 0;
		//float hitDist = 0;
		//for (int j = 1; j <= 64; j++)
		//{
  //          float3 localPos = (worldPos + dir * j * step) - pData.position;
  //          float dist = CubeMapDist(localPos);
		//	float3 sampleCoord = localPos;
		//	sampleCoord.y *= -1;

  //          half probeDist = SAMPLE_TEXTURECUBE_ARRAY(GI_DepthTexture, sampler_GI_DepthTexture, sampleCoord, closestIndex);
            
  //          if (probeDist < 1 && (dist > probeDist * 1000/* + 0.5*/))
		//	{
  //              //if (dist - probeDist * 1000 < step)
		//		{
		//			hit = true;
  //                  firstLength = j * step;
  //                  lastLength = (j - 1) * step;
		//			break;
		//		}
		//	}
		//}

		//half3 hitColor;
		//half4 hitNormalDist;
		//float3 pos;
		//float dist;
  //      if (hit && lastLength < firstLength)
		//{
		//	for (int j = 1; j <= 8; j++)
		//	{
  //              float3 localPos = (worldPos + dir * (lastLength + j * step / 8)) - pData.position;
		//		float tempdist = CubeMapDist(localPos);
		//		float3 sampleCoord = localPos;
  //              sampleCoord.y *= -1;
  //              half probeDist = SAMPLE_TEXTURECUBE_ARRAY(GI_DepthTexture, sampler_GI_DepthTexture, sampleCoord, closestIndex);
  //              if (tempdist > probeDist * 1000)
		//		{
		//			count++;
		//			hitColor = SAMPLE_TEXTURECUBE_ARRAY(GI_ProbeTexture, sampler_GI_ProbeTexture, sampleCoord, closestIndex);
		//			float3 hitNormal = SAMPLE_TEXTURECUBE_ARRAY(GI_NormalTexture, sampler_GI_NormalTexture, sampleCoord, closestIndex);
  //                  hitNormalDist = probeDist;
		//			dist = tempdist;
  //                  float distSqr = (lastLength + j * step / 8);
		//			//pos = worldPos + dir * j * (firstLength - lastLength) / 16;
  //                  color += saturate(dot(dir, normalWS)) * saturate(dot(-dir, hitNormal)) * hitColor / (2 * PI * distSqr * distSqr);
		//			//color = probeDist;// lastLength; // half3(0.3, 0.5, 1.0);
		//			break;
		//		}
		//	}
  //      }

		//color = IndexToCoord(closestIndex);
    }
	
	return (color) /** brdfData.diffuse*/;
}
