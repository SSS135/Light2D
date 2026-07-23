/*

Used to create normal map buffer.

*/

Shader "Light2D/Internal/Normal Map Drawer" {
Properties {
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	_NormalTex ("Normalmap", 2D) = "bump" {}
}

SubShader {
	Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "LightObstacle"="True" }
	Blend SrcAlpha OneMinusSrcAlpha
	Cull Off
	ZWrite Off
	Pass {
		Name "NormalMap"
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
	Blend SrcAlpha OneMinusSrcAlpha
	Cull Off
	ZWrite Off
	Lighting Off

	Pass {  
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				float4 tangent : TANGENT;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 mainTexcoord : TEXCOORD0;
				half2 normalTexcoord : TEXCOORD1;
				half3 utangent : TEXCOORD2;
				half3 vtangent : TEXCOORD3;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
		    sampler2D _NormalTex;
		    float4 _NormalTex_ST;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.mainTexcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				o.normalTexcoord = TRANSFORM_TEX(v.texcoord, _NormalTex);
				o.utangent = normalize(UnityObjectToWorldDir(float3(v.tangent.xy, 0)));
				o.vtangent = normalize(UnityObjectToWorldDir(float3(v.tangent.y, -v.tangent.x, 0)));
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.mainTexcoord);
				fixed3 unpackedNormal = normalize(UnpackNormal(tex2D(_NormalTex, i.normalTexcoord)));
				fixed2 bumpMap = unpackedNormal.xy;
				bumpMap = -bumpMap;
				fixed2 bump = (bumpMap.x*i.utangent + bumpMap.y*i.vtangent).xy;
				fixed2 bumpOut = bump*0.5 + 0.5;
                return fixed4(bumpOut, 0, col.a);
			}
		ENDCG
	}
}

}
