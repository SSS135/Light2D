#ifndef LIGHT2D_URP_FULLSCREEN_INCLUDED
#define LIGHT2D_URP_FULLSCREEN_INCLUDED

#include "Light2DURPCommon.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScaling.hlsl"

float4 _BlitScaleBias;

struct Light2DFullscreenAttributes
{
    uint vertexID : SV_VertexID;
};

struct Light2DClipSpaceAttributes
{
    float4 positionCS : POSITION;
    half4 color : COLOR0;
};

struct Light2DFullscreenVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR0;
};

struct Light2DOverlayVaryings
{
    float4 positionCS : SV_POSITION;
    float2 gameUV : TEXCOORD0;
    float2 lightUV : TEXCOORD1;
    float2 ambientUV : TEXCOORD2;
};

struct Light2DNormalLightVaryings
{
    float4 positionCS : SV_POSITION;
    float2 normalUV : TEXCOORD0;
    float2 lightUV : TEXCOORD1;
    float3 lightPosition : TEXCOORD2;
};

Light2DFullscreenVaryings Light2DFullscreenVert(Light2DFullscreenAttributes input)
{
    Light2DFullscreenVaryings output;
    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
    output.uv = DYNAMIC_SCALING_APPLY_SCALEBIAS(GetFullScreenTriangleTexCoord(input.vertexID));
    output.color = half4(1.0h, 1.0h, 1.0h, 1.0h);
    return output;
}

Light2DFullscreenVaryings Light2DClipSpaceVert(Light2DClipSpaceAttributes input)
{
    Light2DFullscreenVaryings output;
    output.positionCS = input.positionCS;
    output.uv = 0;
    output.color = input.color;
    return output;
}

half4 Light2DCopyFrag(Light2DFullscreenVaryings input) : SV_Target
{
    return SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, input.uv);
}

half4 Light2DObstaclePostFrag(Light2DFullscreenVaryings input) : SV_Target
{
    clip(input.color.a - 0.01h);
    return input.color;
}

half4 Light2DFastBlurFrag(Light2DFullscreenVaryings input) : SV_Target
{
    half2 distanceUV = _Distance / _ScreenParams.xy;
    half diagonal = distanceUV.y * 0.707h;
    half4 sum = SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, input.uv);
    sum += SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, input.uv + half2(distanceUV.x, 0));
    sum += SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, input.uv + half2(-distanceUV.x, 0));
    sum += SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, input.uv + half2(0, distanceUV.x));
    sum += SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, input.uv + half2(0, -distanceUV.x));
    sum += SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, input.uv + half2(diagonal, diagonal));
    sum += SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, input.uv + half2(diagonal, -diagonal));
    sum += SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, input.uv + half2(-diagonal, diagonal));
    sum += SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, input.uv + half2(-diagonal, -diagonal));
    return sum / 9.0h;
}

half4 Light2DAmbientFrag(Light2DFullscreenVaryings input) : SV_Target
{
    half4 emission = SAMPLE_TEXTURE2D(_Light2DURPLightSourcesTex, sampler_Light2DURPLightSourcesTex, input.uv);
    half4 obstacle = SAMPLE_TEXTURE2D(_Light2DURPObstacleTex, sampler_Light2DURPObstacleTex, input.uv);
    obstacle = saturate((1.0h - obstacle) * obstacle.a * _ObstacleMul + _ObstacleAdd);
    float2 uv = input.uv + _Shift;
    half4 oldLight = SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, uv);
    half4 maxLight = SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, uv + half2(_SamplingDist, 0));
    maxLight = max(maxLight, SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, uv + half2(-_SamplingDist, 0)));
    maxLight = max(maxLight, SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, uv + half2(0, -_SamplingDist)));
    maxLight = max(maxLight, SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, uv + half2(0, _SamplingDist)));
    half diagonal = _SamplingDist * 0.707h;
    maxLight = max(maxLight, SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, uv + half2(diagonal, diagonal)));
    maxLight = max(maxLight, SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, uv + half2(diagonal, -diagonal)));
    maxLight = max(maxLight, SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, uv + half2(-diagonal, diagonal)));
    maxLight = max(maxLight, SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, uv + half2(-diagonal, -diagonal)));
    emission.rgb *= emission.a * _EmissionColorMul;
    half4 color = (maxLight + emission) * (1.0h - obstacle);
    return lerp(oldLight, color, 0.2h);
}

Light2DOverlayVaryings Light2DOverlayVert(Light2DFullscreenAttributes input)
{
    Light2DOverlayVaryings output;
    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
    output.gameUV = DYNAMIC_SCALING_APPLY_SCALEBIAS(GetFullScreenTriangleTexCoord(input.vertexID));
    output.lightUV = (output.gameUV - 0.5) * _Scale + 0.5 + _Offset;
    output.ambientUV = (output.lightUV - 0.5) * _ExtendedToSmallTextureScale + 0.5;
    return output;
}

half4 Light2DOverlayFrag(Light2DOverlayVaryings input) : SV_Target
{
    half4 game = SAMPLE_TEXTURE2D(_Light2DURPGameTex, sampler_Light2DURPGameTex, input.gameUV);
    half4 ambient = SAMPLE_TEXTURE2D(_Light2DURPAmbientLightTex, sampler_Light2DURPAmbientLightTex, input.ambientUV);
    half4 sources = SAMPLE_TEXTURE2D(_Light2DURPLightSourcesTex, sampler_Light2DURPLightSourcesTex, input.lightUV) * _LightSourcesMul;
    half4 light = ambient + sources;
    half3 bloom = (game.rgb + _AdditiveLightAdd) * pow(light.rgb, _AdditiveLightPow) * step(0.005h, _AdditiveLightPow);
    return half4(game.rgb * light.rgb * _LightMul + bloom, game.a);
}

Light2DNormalLightVaryings Light2DNormalLightVert(Light2DFullscreenAttributes input)
{
    Light2DNormalLightVaryings output;
    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
    output.normalUV = DYNAMIC_SCALING_APPLY_SCALEBIAS(GetFullScreenTriangleTexCoord(input.vertexID));
    output.lightUV = output.normalUV;
    output.lightPosition = _LightPos;
    return output;
}

half4 Light2DNormalLightFrag(Light2DNormalLightVaryings input) : SV_Target
{
    half4 light = SAMPLE_TEXTURE2D(_Light2DURPMainTex, sampler_Light2DURPMainTex, input.lightUV);
    half3 normal = normalize(SAMPLE_TEXTURE2D(_Light2DURPNormalsBuffer, sampler_Light2DURPNormalsBuffer, input.normalUV).xyz * 2.0h - 1.0h);
    float2 viewportDelta = input.lightPosition.xy - input.lightUV;
    float2 worldXYDisplacement = viewportDelta.x * _ViewportToWorldX + viewportDelta.y * _ViewportToWorldY;
    half3 reverseLightDirection = normalize(half3(worldXYDisplacement, input.lightPosition.z));
    half lightness = max(0, -dot(normal, reverseLightDirection));
    return half4(light.rgb * lightness, light.a);
}

#endif
