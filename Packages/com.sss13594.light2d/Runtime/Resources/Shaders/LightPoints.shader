/*

Collapsed ray-marched light shader. Replaces the eight per-count Light20/30/40/50/60/70/80/90
shaders (and the retired Light 8/9 stub). The path-tracking step count is driven by the material
uniform _PathTrackingSamples (a bounded [loop] in LightBase.cginc / LightBaseURP.hlsl, enabled by
LIGHT2D_UNIFORM_SAMPLES) instead of a compile-time PATH_TRACKING_SAMPLES define, so one shader
serves every sample count and a MaterialPropertyBlock can drive per-light quality.

The Built-in (CGPROGRAM) SubShader is intentionally FIRST and the URP (UniversalPipeline) SubShader
SECOND: these light passes draw as geometry, so URP disambiguates by the RenderPipeline tag and the
ordering is free — the collapsed shader standardizes on Built-in-first.

Perf: the uniform loop was chosen over unrolled keyword variants. If it regresses on the min-spec
mobile A/B, the pre-approved fallback is a single shader with #pragma multi_compile of byte-identical
unrolled _SAMPLES_20.._SAMPLES_90 variants (quality then global, not per-light).

*/

Shader "Light2D/Light Points" {
Properties {
	_MainTex ("Light texture", 2D) = "white" {}
	_ObstacleMul ("Obstacle Mul", Float) = 500
	_EmissionColorMul ("Emission color mul", Float) = 1
	_PathTrackingSamples ("Path tracking samples", Float) = 40
}
SubShader {
	Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}

	LOD 100
	Blend OneMinusDstColor One
	Cull Off
	ZWrite Off
	Lighting Off

	Pass {
		CGPROGRAM
			#define LIGHT2D_UNIFORM_SAMPLES
			#pragma target 3.0
			#pragma multi_compile ORTHOGRAPHIC_CAMERA PERSPECTIVE_CAMERA
			#pragma multi_compile LIGHT2D_XY_PLANE LIGHT2D_XZ_PLANE

			#include "UnityCG.cginc"
			#include "LightBase.cginc" // path tracking code is here

			#pragma vertex light2d_fixed_vert
			#pragma fragment light2_fixed_frag
		ENDCG
	}
}
SubShader {
	Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
	Blend OneMinusDstColor One
	Cull Off
	ZWrite Off
	Pass {
		Name "LightPoints"
		Tags { "LightMode"="SRPDefaultUnlit" }
		HLSLPROGRAM
		#define LIGHT2D_UNIFORM_SAMPLES
		#pragma target 3.0
		#pragma vertex Light2DFixedVert
		#pragma fragment Light2DFixedFrag
		#pragma multi_compile ORTHOGRAPHIC_CAMERA PERSPECTIVE_CAMERA
		#pragma multi_compile LIGHT2D_XY_PLANE LIGHT2D_XZ_PLANE
		#include "LightBaseURP.hlsl"
		ENDHLSL
	}
}

Fallback "Light2D/Light 1 Point"

}
