#ifndef PERLIN_NOISE_INCLUDE
#define PERLIN_NOISE_INCLUDE

float hash(float3 p)  // replace this by something better
{
	p = frac(p*0.3183099 + .1);
	p *= 17.0;
	return frac(p.x*p.y*p.z*(p.x + p.y + p.z));
}

float noise(in float3 x)
{
	float3 p = floor(x);
	float3 f = frac(x);
	f = f*f*(3.0 - 2.0*f);

	return lerp(lerp(lerp(hash(p + float3(0, 0, 0)),
		hash(p + float3(1, 0, 0)), f.x),
		lerp(hash(p + float3(0, 1, 0)),
			hash(p + float3(1, 1, 0)), f.x), f.y),
		lerp(lerp(hash(p + float3(0, 0, 1)),
			hash(p + float3(1, 0, 1)), f.x),
			lerp(hash(p + float3(0, 1, 1)),
				hash(p + float3(1, 1, 1)), f.x), f.y), f.z);
}


#endif // TILED_SHADING_INPUT_INCLUDE
