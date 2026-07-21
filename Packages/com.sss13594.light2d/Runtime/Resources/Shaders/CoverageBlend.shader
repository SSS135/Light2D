/*

That shader is the coverage-blend fullscreen post pass. It runs after the light overlay only when
volumes exist and composites final = lerp(rawGameImage, light2dComposite, coverage). The raw game
image (_GameTex) is sampled with the overlay's vertical-flip convention; the package-authored
composite and coverage render targets are sampled straight, and the coverage UV is mapped like the
overlay's ambient UV via _Scale/_Offset/_ExtendedToSmallTextureScale.

*/


Shader "Light2D/Coverage Blend" {
Properties {
	_GameTex ("Game", 2D) = "white" {}
	_CompositeTex ("Composite", 2D) = "black" {}
	_CoverageTex ("Coverage", 2D) = "white" {}
	_Scale ("Scale", Vector) = (1,1,0,0)
	_Offset ("Offset", Vector) = (0,0,0,0)
	_ExtendedToSmallTextureScale ("Extended To Small Texture Scale", Vector) = (1,1,0,0)
}

SubShader {
	Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
	LOD 100

	ZWrite Off

	Pass {
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoordGame : TEXCOORD0;
				half2 texcoordComposite : TEXCOORD1;
				half2 texcoordCoverage : TEXCOORD2;
			};

			uniform sampler2D _GameTex;
			uniform half4 _GameTex_TexelSize;
			uniform sampler2D _CompositeTex;
			uniform sampler2D _CoverageTex;
			uniform float2 _Scale;
			uniform float2 _Offset;
			uniform float2 _ExtendedToSmallTextureScale;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoordGame = v.texcoord;
				o.texcoordComposite = v.texcoord;
				half2 texcoordLight = (v.texcoord - 0.5)*_Scale + 0.5 + _Offset;
				o.texcoordCoverage = (texcoordLight - 0.5)*_ExtendedToSmallTextureScale + 0.5;

				#if UNITY_UV_STARTS_AT_TOP
				if (_GameTex_TexelSize.y < 0)
					o.texcoordGame = half2(o.texcoordGame.x, 1 - o.texcoordGame.y);
				#endif

				return o;
			}

			half4 frag (v2f i) : SV_Target
			{
				half t = tex2D(_CoverageTex, i.texcoordCoverage).r;
				half4 game = tex2D(_GameTex, i.texcoordGame);
				half4 comp = tex2D(_CompositeTex, i.texcoordComposite);
				return half4(lerp(game.rgb, comp.rgb, t), comp.a);
			}
		ENDCG
	}
}
SubShader {
	Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
	ZWrite Off
	Pass {
		Name "CoverageBlend"
		Tags { "LightMode"="Light2DFullscreen" }
		HLSLPROGRAM
		#pragma vertex Light2DCoverageBlendVert
		#pragma fragment Light2DCoverageBlendFrag
		#include "CoverageBlend.hlsl"
		ENDHLSL
	}
}

}
