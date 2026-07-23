/*

That shader draws a Light2DVolume shape mesh into the coverage mask render target.
Vertex color RGB carries the coverage value (how much Light2D applies inside the region) and
vertex alpha carries the shape falloff; the mask is alpha-blended so higher-priority volumes,
drawn last, win in their solid interior and feather across their blend distance. _MainTex.a
lets a sprite-shaped volume mask by its texture (box/circle bind no texture so alpha stays 1).

*/


Shader "Light2D/Internal/Coverage Mask" {
Properties {
	_MainTex ("Texture", 2D) = "white" {}
}

SubShader {
	Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
	Cull Off
	ZWrite Off
	Blend SrcAlpha OneMinusSrcAlpha
	Pass {
		Name "CoverageMask"
		Tags { "LightMode"="SRPDefaultUnlit" }
		HLSLPROGRAM
		#pragma vertex Light2DCoverageMaskVert
		#pragma fragment Light2DCoverageMaskFrag
		#include "Light2DURPCommon.hlsl"

		struct Light2DCoverageMaskAttributes
		{
			float4 positionOS : POSITION;
			float2 uv : TEXCOORD0;
			half4 color : COLOR0;
		};

		struct Light2DCoverageMaskVaryings
		{
			float4 positionCS : SV_POSITION;
			float2 uv : TEXCOORD0;
			half4 color : COLOR0;
		};

		Light2DCoverageMaskVaryings Light2DCoverageMaskVert(Light2DCoverageMaskAttributes input)
		{
			Light2DCoverageMaskVaryings output;
			output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
			output.uv = TRANSFORM_TEX(input.uv, _MainTex);
			output.color = input.color;
			return output;
		}

		half4 Light2DCoverageMaskFrag(Light2DCoverageMaskVaryings input) : SV_Target
		{
			half texAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
			return half4(input.color.rgb, input.color.a * texAlpha);
		}
		ENDHLSL
	}
}
SubShader {
	Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
	LOD 100

	Cull Off
	Lighting Off
	ZWrite Off
	Blend SrcAlpha OneMinusSrcAlpha

	Pass {
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				fixed4 color : COLOR;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
				fixed4 color : COLOR;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				o.color = v.color;
				return o;
			}

			half4 frag (v2f i) : SV_Target
			{
				half texAlpha = tex2D(_MainTex, i.texcoord).a;
				return half4(i.color.rgb, i.color.a * texAlpha);
			}
		ENDCG
	}
}

}
