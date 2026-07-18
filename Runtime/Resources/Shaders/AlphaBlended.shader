Shader "Light2D/Internal/Alpha Blended"
{
	Properties
	{
		_MainTex ("Sprite Texture", 2D) = "white" {}
	}

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
		}

		Cull Off
		Lighting Off
		ZWrite Off
		Blend One OneMinusSrcAlpha

		Pass
		{
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata_t
			{
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
			};

			v2f vert(appdata_t input)
			{
				v2f output;
				output.vertex = UnityObjectToClipPos(input.vertex);
				output.texcoord = input.texcoord;
				return output;
			}

			sampler2D _MainTex;

			fixed4 frag(v2f input) : SV_Target
			{
				fixed4 color = tex2D(_MainTex, input.texcoord);
				color.rgb *= color.a;
				return color;
			}
		ENDCG
		}
	}
}
