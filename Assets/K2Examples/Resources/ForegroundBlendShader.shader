// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/ForegroundBlendShader" 
{
	Properties
	{
		_MainTex ("MainTex", 2D) = "white" {}
        _BodyTex ("Body (RGB)", 2D) = "white" {}
        _ColorTex ("Color (RGB)", 2D) = "white" {}
        _ColorFlipH ("Horizontal Flip", Int) = 0
        _ColorFlipV ("Vertical Flip", Int) = 0
        _SwapTextures ("Swap background and foreground", Int) = 0
	}

	SubShader 
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }

		Pass
		{
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			//float4 _MainTex_ST;
			uniform float4 _MainTex_TexelSize;

			uniform sampler2D _BodyTex;
			uniform sampler2D _ColorTex;
			
			uniform int _ColorFlipH;
			uniform int _ColorFlipV;
			uniform int _SwapTextures;


			struct v2f 
			{
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

			half4 frag (v2f i) : COLOR
			{
				float2 c_uv;
				c_uv.x = _ColorFlipH ? 1.0 - i.uv.x : i.uv.x;
				c_uv.y = _ColorFlipV ? 1.0 - i.uv.y : i.uv.y;

				float2 c_uv2 = float2(i.uv.x, 1.0 - i.uv.y);
#if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0)
                {
                    c_uv.y = 1.0 - c_uv.y;
                    c_uv2.y = 1.0 - c_uv2.y;
                }
#endif

				float4 clrBack = tex2D (_MainTex, i.uv);
				float4 clrFore = tex2D (_ColorTex, c_uv);

				float w = tex2D(_BodyTex, c_uv2).w;
				float4 clrOut = _SwapTextures ? clrBack * w + clrFore * (1.0 - w) : clrFore * w + clrBack * (1.0 - w);

				return clrOut;
			}

			ENDCG
		}
	}

	FallBack "Diffuse"
}
