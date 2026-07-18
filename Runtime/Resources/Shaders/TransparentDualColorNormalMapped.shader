/*

That shader is usually used to draw light obstacles.
Have main texture, additive color and multiplicative color.
First color is multipicative. It's grabbed from vertex color.
Second color is additive (RGB) and partially multiplicative (A). It's encoded in TEXCOORD1.

*/


Shader "Light2D/Transparent Dual Color Normal Mapped" {
Properties {
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	_NormalTex ("Normalmap", 2D) = "bump" {}
}

SubShader {
	Tags {
		"Queue"="Transparent"
		"IgnoreProjector"="True"
		"RenderType"="Transparent"
		"LightObstacle"="True"
	}
	LOD 100

	Cull Off
	ZWrite Off
	Lighting Off
	Blend SrcAlpha OneMinusSrcAlpha

	Pass {
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord0 : TEXCOORD0;
				float4 color : COLOR0;
				float2 texcoord1 : TEXCOORD1;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
				float4 color0 : COLOR0;
				float4 color1 : COLOR1;
			};

			sampler2D _MainTex;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = v.texcoord0;
				o.color0 = v.color;
				o.color1 = float4(EncodeFloatRGBA(v.texcoord1.x).xyz, EncodeFloatRGBA(v.texcoord1.y).x);

				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.texcoord);
				return col*i.color0 + fixed4(i.color1.rgb, i.color1.a*i.color1.a*col.a*10);
			}
		ENDCG
	}
}
SubShader {
	Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "LightObstacle"="True" }
	Cull Off
	ZWrite Off
	Blend SrcAlpha OneMinusSrcAlpha
	Pass {
		Name "ObstacleDualColor"
		Tags { "LightMode"="SRPDefaultUnlit" }
		HLSLPROGRAM
		#pragma vertex Light2DDualColorVert
		#pragma fragment Light2DDualColorFrag
		#include "Light2DURPRenderer.hlsl"
		ENDHLSL
	}
	Pass {
		Name "Light2DNormals"
		Tags { "LightMode"="Light2DNormals" }
		HLSLPROGRAM
		#pragma vertex Light2DNormalMapVert
		#pragma fragment Light2DNormalMapFrag
		#include "Light2DURPRenderer.hlsl"
		ENDHLSL
	}
}

}
