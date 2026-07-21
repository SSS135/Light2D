#ifndef LIGHT2D_COVERAGE_BLEND_INCLUDED
#define LIGHT2D_COVERAGE_BLEND_INCLUDED

// Self-contained URP include for the coverage-blend fullscreen pass. It deliberately does NOT include
// Light2DURPCommon.hlsl / Light2DURPFullscreen.hlsl (which are frozen) and declares its own texture
// names so it never collides with or edits those files.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_Light2DURPGameTex);
SAMPLER(sampler_Light2DURPGameTex);
TEXTURE2D(_Light2DURPCompositeTex);
SAMPLER(sampler_Light2DURPCompositeTex);
TEXTURE2D(_Light2DURPCoverageTex);
SAMPLER(sampler_Light2DURPCoverageTex);

float4 _BlitScaleBias;
float2 _Scale;
float2 _Offset;
float2 _ExtendedToSmallTextureScale;

struct Light2DCoverageBlendAttributes
{
    uint vertexID : SV_VertexID;
};

struct Light2DCoverageBlendVaryings
{
    float4 positionCS : SV_POSITION;
    float2 gameUV : TEXCOORD0;
    float2 compositeUV : TEXCOORD1;
    float2 coverageUV : TEXCOORD2;
};

Light2DCoverageBlendVaryings Light2DCoverageBlendVert(Light2DCoverageBlendAttributes input)
{
    Light2DCoverageBlendVaryings output;
    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
    float2 fullscreenUV = GetFullScreenTriangleTexCoord(input.vertexID);
    output.gameUV = fullscreenUV * _BlitScaleBias.xy + _BlitScaleBias.zw;
    output.compositeUV = output.gameUV;
    float2 lightUV = (fullscreenUV - 0.5) * _Scale + 0.5 + _Offset;
    output.coverageUV = (lightUV - 0.5) * _ExtendedToSmallTextureScale + 0.5;
    return output;
}

half4 Light2DCoverageBlendFrag(Light2DCoverageBlendVaryings input) : SV_Target
{
    half t = SAMPLE_TEXTURE2D(_Light2DURPCoverageTex, sampler_Light2DURPCoverageTex, input.coverageUV).r;
    half4 game = SAMPLE_TEXTURE2D(_Light2DURPGameTex, sampler_Light2DURPGameTex, input.gameUV);
    half4 comp = SAMPLE_TEXTURE2D(_Light2DURPCompositeTex, sampler_Light2DURPCompositeTex, input.compositeUV);
    return half4(lerp(game.rgb, comp.rgb, t), comp.a);
}

#endif
