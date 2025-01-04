Shader "EditorWindowTexture" {
	Properties {
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
		_Color ("Color", Color) = (1,1,1,1)
	}

	SubShader {
	Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
	LOD 100
	
	ZWrite Off
	Blend SrcAlpha OneMinusSrcAlpha 
	
		Pass {  // SINGLE LINE
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 2.0
				#include "UnityCG.cginc"

				struct appdata_t {
					float4 vertex : POSITION;
					float4 color : COLOR;
					float2 texcoord : TEXCOORD0;
				};

				struct v2f {
					float4 vertex : SV_Position;
					fixed4 color : COLOR;
					half2 texcoord : TEXCOORD0;
					float2 clipUV : TEXCOORD1;
				};

				sampler2D _MainTex;
				float4 _MainTex_ST;
				float4 _Color;
				uniform float4x4 unity_GUIClipTextureMatrix;
				sampler2D _GUIClipTexture;

				v2f vert (appdata_t v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.color = v.color * _Color;
					o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
					float3 eyePos = UnityObjectToViewPos( v.vertex );
					o.clipUV = mul( unity_GUIClipTextureMatrix, float4( eyePos.xy, 0, 1.0 ) );
					return o;
				}
				
				fixed4 frag( v2f i ) : SV_Target
				{
					float4 l1 = tex2D( _MainTex, i.texcoord);
					float clipAlpha = tex2D( _GUIClipTexture, i.clipUV ).a;
					l1.rgba *= i.color.rgba;
					l1.a *= clipAlpha;
					return l1;
				}
			ENDCG
		}

		Pass {  // MULTI LINE
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 2.0
				#include "UnityCG.cginc"

				struct appdata_t {
					float4 vertex : POSITION;
					float4 color : COLOR;
					float2 texcoord : TEXCOORD0;
				};

				struct v2f {
					float4 vertex : SV_Position;
					fixed4 color : COLOR;
					half2 texcoord : TEXCOORD0;
					float2 clipUV : TEXCOORD1;
				};

				sampler2D _MainTex;
				float4 _MainTex_ST;
				float4 _Color;
				uniform float4x4 unity_GUIClipTextureMatrix;
				sampler2D _GUIClipTexture;
				float _InvertedZoom;

				v2f vert (appdata_t v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.color = v.color * _Color;
					o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
					float3 eyePos = UnityObjectToViewPos( v.vertex );
					o.clipUV = mul( unity_GUIClipTextureMatrix, float4( eyePos.xy, 0, 1.0 ) );
					return o;
				}
				
				fixed4 frag( v2f i ) : SV_Target
				{
					float4 l1 = tex2D( _MainTex, i.texcoord);
					float clipAlpha = tex2D( _GUIClipTexture, i.clipUV ).a;
					l1.rgba *= i.color.rgba;
					l1.a *= clipAlpha;
					return l1;
				}
			ENDCG
		}
	}
}
