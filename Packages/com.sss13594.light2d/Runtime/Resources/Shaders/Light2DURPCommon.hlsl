#ifndef LIGHT2D_URP_COMMON_INCLUDED
#define LIGHT2D_URP_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_NormalTex);
SAMPLER(sampler_NormalTex);
TEXTURE2D(_Light2DURPMainTex);
SAMPLER(sampler_Light2DURPMainTex);
TEXTURE2D(_Light2DURPObstacleTex);
SAMPLER(sampler_Light2DURPObstacleTex);
TEXTURE2D(_Light2DURPGameTex);
SAMPLER(sampler_Light2DURPGameTex);
TEXTURE2D(_Light2DURPAmbientLightTex);
SAMPLER(sampler_Light2DURPAmbientLightTex);
TEXTURE2D(_Light2DURPLightSourcesTex);
SAMPLER(sampler_Light2DURPLightSourcesTex);
TEXTURE2D(_Light2DURPNormalsBuffer);
SAMPLER(sampler_Light2DURPNormalsBuffer);

float4 _MainTex_ST;
float4 _NormalTex_ST;
float4 _Light2DURPGameTex_TexelSize;
float4 _Light2DURPMainTex_TexelSize;
float2 _Scale;
float2 _Offset;
float2 _ExtendedToSmallTextureScale;
float2 _Shift;
float2 _PosOffset;
float3 _LightPos;
float2 _ViewportToWorldX;
float2 _ViewportToWorldY;
half _Distance;
half _ObstacleMul;
half _ObstacleAdd;
half _EmissionColorMul;
half _SamplingDist;
half _LightMul;
half _LightSourcesMul;
half _AdditiveLightPow;
half _AdditiveLightAdd;
half _StepCountMul;
half _PixelsPerBlock;

float4 Light2DEncodeFloatRGBA(float value)
{
    float4 encoded = frac(value * float4(1.0, 255.0, 65025.0, 16581375.0));
    encoded -= encoded.yzww * float4(1.0 / 255.0, 1.0 / 255.0, 1.0 / 255.0, 0.0);
    return encoded;
}

#endif
