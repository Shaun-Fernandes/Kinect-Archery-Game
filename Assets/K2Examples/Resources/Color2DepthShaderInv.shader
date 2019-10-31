// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Kinect/Color2DepthShaderInv" {
    Properties {
        _BodyTex ("Body (RGB)", 2D) = "white" {}
        _ColorTex ("Color (RGB)", 2D) = "white" {}
    }
    
	SubShader {
		Pass {
			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }
		
			CGPROGRAM
			#pragma target 5.0
			//#pragma enable_d3d11_debug_symbols

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			uniform sampler2D _BodyTex;
			uniform sampler2D _ColorTex;

			struct v2f {
				float4 pos : SV_POSITION;
			    float2 uv : TEXCOORD0;
			};

			v2f vert (appdata_base v)
			{
				v2f o;
				
				o.pos = UnityObjectToClipPos (v.vertex);
				o.uv = v.texcoord;
				
				return o;
			}

			float4 frag (v2f i) : COLOR
			{
				float player = tex2D(_BodyTex, i.uv).w;
				float4 clr = tex2D (_ColorTex, i.uv);
				clr.w = player < 0.1 ? 1 : 0; // (1 - player);

				return clr;
			}

			ENDCG
		}
	}

	Fallback Off
}