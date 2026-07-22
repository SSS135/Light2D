/*

That shader is used to compute iterative ambient lighting.
Similiar to FastBlur shader.

*/


Shader "Light2D/Ambient Light Computer" {
Properties {
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	_ObstacleMul ("Obstacle Mul", Float) = 1.5
	_ObstacleAdd ("Obstacle add", Float) = 0.1
	_EmissionColorMul ("Emission color mul", Float) = 0.1
	_SamplingDist ("Average sampling distance", Float) = 0.01
	_AmbientBounceTint ("Colored bounce tint", Range(0, 1)) = 0
	_AmbientAdaptSpeed ("Ambient Adapt Speed", Range(0,1)) = 0.2
}
SubShader {	
	Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}

	LOD 100
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
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
			};

			sampler2D _MainTex; // previous iterative ambient light texture
			sampler2D _ObstacleTex; // light obstacles texture
			sampler2D _LightSourcesTex; // light sources texture
			half _PixelsPerBlock;
			half2 _Shift; 
			half _ObstacleMul;
			half _EmissionColorMul;
			half _SamplingDist;
			half _ObstacleAdd;
			half _AmbientBounceTint;
			half _AmbientAdaptSpeed;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = v.texcoord;
				return o;
			}


			half4 frag (v2f i) : SV_Target
			{
				const half addMul = 0.25;
				
				half4 emission = tex2D(_LightSourcesTex, i.texcoord);
				half4 obstacle = tex2D(_ObstacleTex, i.texcoord);
				half3 obstacleAlbedo = obstacle.rgb; // colored bounce: obstacle color before it becomes attenuation
				obstacle = saturate(((1 - obstacle)*obstacle.a*_ObstacleMul + _ObstacleAdd));

				half2 uv = i.texcoord + _Shift;

				half4 oldLight = tex2D(_MainTex, uv);

				// computing average value of near pixels
				half4 maxLight =		 tex2D(_MainTex, uv + half2(_SamplingDist, 0));
				maxLight = max(maxLight, tex2D(_MainTex, uv + half2(-_SamplingDist, 0)));
				maxLight = max(maxLight, tex2D(_MainTex, uv + half2(0, -_SamplingDist)));
				maxLight = max(maxLight, tex2D(_MainTex, uv + half2(0, _SamplingDist)));
				half dist45 = _SamplingDist*0.707;
				maxLight = max(maxLight, tex2D(_MainTex, uv + half2(dist45, dist45)));
				maxLight = max(maxLight, tex2D(_MainTex, uv + half2(dist45, -dist45)));
				maxLight = max(maxLight, tex2D(_MainTex, uv + half2(-dist45, dist45)));
				maxLight = max(maxLight, tex2D(_MainTex, uv + half2(-dist45, -dist45)));

				emission.rgb *= emission.a*_EmissionColorMul;

				half4 col = (maxLight + emission)*(half4(1,1,1,1) - obstacle);
				// Tint the propagated (bounced) light by the obstacle's albedo so a colored wall tints
				// the indirect fill. _AmbientBounceTint defaults to 0, so lerp(...,0) is a bit-exact
				// no-op and existing scenes are visually unchanged.
				col.rgb = lerp(col.rgb, col.rgb * obstacleAlbedo, _AmbientBounceTint);

				return lerp(oldLight, col, _AmbientAdaptSpeed);
			}
		ENDCG
	}
}
SubShader {
	Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
	ZWrite Off
	Pass {
		Name "AmbientLight"
		Tags { "LightMode"="Light2DFullscreen" }
		HLSLPROGRAM
		#pragma vertex Light2DFullscreenVert
		#pragma fragment Light2DAmbientFrag
		#include "Light2DURPFullscreen.hlsl"
		ENDHLSL
	}
}

}
