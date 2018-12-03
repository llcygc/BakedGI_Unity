//
// This file was automatically generated. Please don't edit by hand.
//

#ifndef CLUSTERPASS_CS_HLSL
#define CLUSTERPASS_CS_HLSL
//
// Viva.Experimental.Rendering.ClusterPipeline.ClusterPass.LightVolumeType:  static fields
//
#define LIGHTVOLUMETYPE_CONE (0)
#define LIGHTVOLUMETYPE_SPHERE (1)
#define LIGHTVOLUMETYPE_BOX (2)
#define LIGHTVOLUMETYPE_COUNT (3)

//
// Viva.Experimental.Rendering.ClusterPipeline.ClusterPass.LightCategory:  static fields
//
#define LIGHTCATEGORY_PUNCTUAL (0)
#define LIGHTCATEGORY_AREA (1)
#define LIGHTCATEGORY_ENV (2)
#define LIGHTCATEGORY_COUNT (3)

//
// Viva.Experimental.Rendering.ClusterPipeline.ClusterPass.LightFeatureFlags:  static fields
//
#define LIGHTFEATUREFLAGS_PUNCTUAL (4096)
#define LIGHTFEATUREFLAGS_AREA (8192)
#define LIGHTFEATUREFLAGS_DIRECTIONAL (16384)
#define LIGHTFEATUREFLAGS_ENV (32768)
#define LIGHTFEATUREFLAGS_SKY (65536)
#define LIGHTFEATUREFLAGS_SSREFRACTION (131072)
#define LIGHTFEATUREFLAGS_SSREFLECTION (262144)

//
// Viva.Experimental.Rendering.ClusterPipeline.ClusterPass.LightDefinitions:  static fields
//
#define MAX_NR_LIGHTS_PER_CAMERA (1024)
#define MAX_NR_BIG_TILE_LIGHTS_PLUS_ONE (512)
#define VIEW_PORT_SCALE_Z (1)
#define USE_LEFT_HAND_CAMERA_SPACE (1)
#define NUM_FEATURE_VARIANTS (27)
#define LIGHT_FEATURE_MASK_FLAGS (16773120)
#define LIGHT_FEATURE_MASK_FLAGS_OPAQUE (16510976)
#define LIGHT_FEATURE_MASK_FLAGS_TRANSPARENT (16510976)
#define MATERIAL_FEATURE_MASK_FLAGS (4095)


#endif
