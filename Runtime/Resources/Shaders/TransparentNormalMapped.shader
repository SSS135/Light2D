/*

That shader is usually used to draw light obstacles.
Have main texture and multiplicative color.

*/


Shader "Light2D/Transparent Normal Mapped" {
Properties {
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	_NormalTex ("Normalmap", 2D) = "bump" {}
}

SubShader {
	Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "LightObstacle"="True" }
	Cull Off
	ZWrite Off
	Blend SrcAlpha OneMinusSrcAlpha
	Pass {
		Name "Obstacle"
		Tags { "LightMode"="SRPDefaultUnlit" }
		HLSLPROGRAM
		#pragma vertex Light2DRendererVert
		#pragma fragment Light2DTransparentFrag
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
				float2 texcoord : TEXCOORD0;
				float4 color : COLOR0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
				float4 color : COLOR0;
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

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.texcoord);
				return col*i.color;
			}
		ENDCG
	}
}

}
