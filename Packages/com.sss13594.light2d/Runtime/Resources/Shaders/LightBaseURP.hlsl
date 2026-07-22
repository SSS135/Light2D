#ifndef LIGHT_BASE_URP_INCLUDED
#define LIGHT_BASE_URP_INCLUDED

#include "Light2DURPCommon.hlsl"

struct Light2DFixedAttributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR0;
    float2 center : TEXCOORD1;
};

struct Light2DFixedVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR0;
    #if defined(PERSPECTIVE_CAMERA)
    float2 center : TEXCOORD1;
    float4 projectedPosition : TEXCOORD2;
    float worldZ : TEXCOORD3;
    #else
    float2 position : TEXCOORD1;
    float2 centerPosition : TEXCOORD2;
    #endif
    half2 aspect : TEXCOORD4;
};

float2 Light2DScaledScreenPosition(float4 positionCS)
{
    float4 screenPosition = ComputeScreenPos(positionCS);
    return (screenPosition.xy / screenPosition.w - 0.5) * _ExtendedToSmallTextureScale + 0.5;
}

Light2DFixedVaryings Light2DFixedVert(Light2DFixedAttributes input)
{
    Light2DFixedVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = input.uv;
    #if defined(PERSPECTIVE_CAMERA)
    output.center = input.center;
    output.projectedPosition = output.positionCS;
    output.worldZ = TransformObjectToWorld(input.positionOS.xyz).z;
    #else
    output.position = Light2DScaledScreenPosition(output.positionCS);
    #if defined(LIGHT2D_XZ_PLANE)
    float3 centerWS = float3(input.center.x, 0, input.center.y);
    #else
    float3 centerWS = float3(input.center, 0);
    #endif
    output.centerPosition = Light2DScaledScreenPosition(TransformWorldToHClip(centerWS));
    #if defined(UNITY_HALF_TEXEL_OFFSET)
    output.position += _PosOffset;
    output.centerPosition += _PosOffset;
    #endif
    #endif
    output.color = input.color;
    output.aspect = half2(_ScreenParams.x / _ScreenParams.y, 1);
    return output;
}

void Light2DFixedPositions(Light2DFixedVaryings input, out float2 position, out float2 centerPosition)
{
    #if defined(PERSPECTIVE_CAMERA)
    position = Light2DScaledScreenPosition(input.projectedPosition);
    centerPosition = Light2DScaledScreenPosition(TransformWorldToHClip(float3(input.center, input.worldZ)));
    #if defined(UNITY_HALF_TEXEL_OFFSET)
    position += _PosOffset;
    centerPosition += _PosOffset;
    #endif
    #else
    position = input.position;
    centerPosition = input.centerPosition;
    #endif
}

#if defined(PATH_TRACKING_SAMPLES) || defined(LIGHT2D_UNIFORM_SAMPLES)

// Mirror of LightBase.cginc: constant PATH_TRACKING_SAMPLES for the retained define path, or a
// coherent material uniform for the collapsed "Light2D/Light Points" shader (LIGHT2D_UNIFORM_SAMPLES).
// The uniform float is cast to int for a coherent [loop] bound; the define path keeps [unroll].
#if defined(LIGHT2D_UNIFORM_SAMPLES)
float _PathTrackingSamples;
#define LIGHT2D_SAMPLE_COUNT ((int)_PathTrackingSamples)
#define LIGHT2D_STEP_SIZE (1.0h / _PathTrackingSamples)
#define LIGHT2D_LOOP_ATTR [loop]
#else
#define LIGHT2D_SAMPLE_COUNT PATH_TRACKING_SAMPLES
#define LIGHT2D_STEP_SIZE (1.0h / PATH_TRACKING_SAMPLES)
#define LIGHT2D_LOOP_ATTR [unroll]
#endif

half4 Light2DFixedFrag(Light2DFixedVaryings input) : SV_Target
{
    half4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
    float2 position;
    float2 centerPosition;
    Light2DFixedPositions(input, position, centerPosition);
    half stepSize = LIGHT2D_STEP_SIZE;
    half rayLength = length((position - centerPosition) * input.aspect);
    half obstacleMultiplier = _ObstacleMul * stepSize * rayLength;
    half4 color = input.color * textureColor * textureColor.a * input.color.a;
    half rayPosition = 0;
    LIGHT2D_LOOP_ATTR
    for (int sampleIndex = 0; sampleIndex < LIGHT2D_SAMPLE_COUNT; sampleIndex++)
    {
        rayPosition += stepSize;
        half4 obstacle = SAMPLE_TEXTURE2D_LOD(_Light2DURPObstacleTex, sampler_Light2DURPObstacleTex,
            lerp(centerPosition, position, rayPosition), 0);
        color *= saturate(1.0h - (1.0h - obstacle) * obstacle.a * obstacleMultiplier);
    }
    color.rgb *= _EmissionColorMul;
    return color;
}
#endif

half4 Light2DAutoFrag(Light2DFixedVaryings input) : SV_Target
{
    half4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
    float2 position;
    float2 centerPosition;
    Light2DFixedPositions(input, position, centerPosition);
    half rayLength = length((position - centerPosition) * input.aspect);
    int stepCount = max(1, (int)round(_StepCountMul * rayLength * _ScreenParams.y));
    half stepSize = 1.0h / stepCount;
    half obstacleMultiplier = _ObstacleMul * stepSize * rayLength;
    half4 color = input.color * textureColor * textureColor.a * input.color.a;
    half rayPosition = 0;
    [loop]
    for (int sampleIndex = 0; sampleIndex < stepCount; sampleIndex++)
    {
        rayPosition += stepSize;
        half4 obstacle = SAMPLE_TEXTURE2D_LOD(_Light2DURPObstacleTex, sampler_Light2DURPObstacleTex,
            lerp(centerPosition, position, rayPosition), 0);
        color *= saturate(1.0h - (1.0h - obstacle) * obstacle.a * obstacleMultiplier);
    }
    color.rgb *= _EmissionColorMul;
    return color;
}

#endif
