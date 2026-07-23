#ifndef LIGHT2D_URP_RENDERER_INCLUDED
#define LIGHT2D_URP_RENDERER_INCLUDED

#include "Light2DURPCommon.hlsl"

struct Light2DRendererAttributes
{
    float4 positionOS : POSITION;
    float4 uv : TEXCOORD0;
    float2 data : TEXCOORD1;
    half4 color : COLOR0;
    float4 tangentOS : TANGENT;
};

struct Light2DRendererVaryings
{
    float4 positionCS : SV_POSITION;
    float4 uv : TEXCOORD0;
    half4 color : COLOR0;
    float4 data : TEXCOORD1;
    float3 tangentU : TEXCOORD2;
    float3 tangentV : TEXCOORD3;
};

Light2DRendererVaryings Light2DRendererVert(Light2DRendererAttributes input)
{
    Light2DRendererVaryings output;
    ZERO_INITIALIZE(Light2DRendererVaryings, output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv.xy = TRANSFORM_TEX(input.uv.xy, _MainTex);
    output.color = input.color;
    return output;
}

half4 Light2DTransparentFrag(Light2DRendererVaryings input) : SV_Target
{
    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy) * input.color * _Light2DObstacleGlobalMul;
}

Light2DRendererVaryings Light2DDualColorVert(Light2DRendererAttributes input)
{
    Light2DRendererVaryings output = Light2DRendererVert(input);
    float4 encodedX = Light2DEncodeFloatRGBA(input.data.x);
    float4 encodedY = Light2DEncodeFloatRGBA(input.data.y);
    output.data = float4(encodedX.xyz, encodedY.x);
    return output;
}

half4 Light2DDualColorFrag(Light2DRendererVaryings input) : SV_Target
{
    half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy);
    return color * input.color * _Light2DObstacleGlobalMul + half4(input.data.rgb, input.data.a * input.data.a * color.a * 10.0h);
}

Light2DRendererVaryings Light2DNormalMapVert(Light2DRendererAttributes input)
{
    Light2DRendererVaryings output = Light2DRendererVert(input);
    output.uv.zw = TRANSFORM_TEX(input.uv.xy, _NormalTex);
    output.tangentU = normalize(TransformObjectToWorldDir(float3(input.tangentOS.xy, 0)));
    output.tangentV = normalize(TransformObjectToWorldDir(float3(input.tangentOS.y, -input.tangentOS.x, 0)));
    return output;
}

half4 Light2DNormalMapFrag(Light2DRendererVaryings input) : SV_Target
{
    half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy);
    half3 unpackedNormal = normalize(UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, input.uv.zw)));
    half2 normal = -unpackedNormal.xy;
    half2 mappedNormal = (normal.x * input.tangentU + normal.y * input.tangentV).xy;
    return half4(mappedNormal * 0.5h + 0.5h, 0, color.a);
}

half4 Light2DIgnoreObstaclesFrag(Light2DRendererVaryings input) : SV_Target
{
    half4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy);
    half4 color = input.color * textureColor * textureColor.a * input.color.a;
    color.rgb *= _EmissionColorMul;
    return color;
}

struct Light2DOnePointVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 screenPosition : TEXCOORD1;
    half4 color : COLOR0;
};

Light2DOnePointVaryings Light2DOnePointVert(Light2DRendererAttributes input)
{
    Light2DOnePointVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = input.uv.xy;
    output.screenPosition = ComputeScreenPos(output.positionCS);
    output.screenPosition.xy =
        (output.screenPosition.xy - 0.5 * output.screenPosition.w) * _ExtendedToSmallTextureScale +
        0.5 * output.screenPosition.w;
    output.color = input.color;
    return output;
}

half4 Light2DOnePointFrag(Light2DOnePointVaryings input) : SV_Target
{
    half2 position = input.screenPosition.xy / input.screenPosition.w;
    half4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
    half4 color = input.color * textureColor * textureColor.a * input.color.a;
    half4 obstacle = SAMPLE_TEXTURE2D(_Light2DURPObstacleTex, sampler_Light2DURPObstacleTex, position);
    color *= saturate(1.0h - (1.0h - obstacle) * obstacle.a * _ObstacleMul);
    color.rgb *= _EmissionColorMul;
    return color;
}

#endif
