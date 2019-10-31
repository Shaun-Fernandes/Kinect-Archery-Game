// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Kinect/Color2BodyShader" {
    Properties {
        _BodyTex ("Body (RGB)", 2D) = "white" {}
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
			
			uniform float _ColorResX;
			uniform float _ColorResY;
			uniform float _DepthResX;
			uniform float _DepthResY;

			StructuredBuffer<float2> _DepthCoords;

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
				int cx = (int)(i.uv.x * _ColorResX);
				int cy = (int)(i.uv.y * _ColorResY);
				int ci = (int)(cx + cy * _ColorResX);
				
				if (!isinf(_DepthCoords[ci].x) && !isinf(_DepthCoords[ci].y))
				{
					float di_index, di_length;
					di_index = _DepthCoords[ci].x + (_DepthCoords[ci].y * _DepthResX);
					di_length = _DepthResX * _DepthResY;
				
					if(di_index >= 0 && di_index < di_length)
					{
						float2 di_uv;
						di_uv.x = _DepthCoords[ci].x / _DepthResX;
						di_uv.y = _DepthCoords[ci].y / _DepthResY;

						return tex2D(_BodyTex, di_uv);
					}
				}
				
				return float4(0, 0, 0, 0);
			}

			ENDCG
		}
	}

	Fallback Off
}