// 인벤토리 배경용 Kawase 블러. Graphics.Blit 으로 여러 번 통과시켜 흐림 정도를 올린다.
// URP에서도 그냥 언릿 블릿 셰이더라 문제없이 동작한다.
Shader "Nibo/UIBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Blur Offset", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Offset;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy * _Offset;

                fixed4 c  = tex2D(_MainTex, i.uv + float2(-t.x, -t.y));
                c        += tex2D(_MainTex, i.uv + float2( t.x, -t.y));
                c        += tex2D(_MainTex, i.uv + float2(-t.x,  t.y));
                c        += tex2D(_MainTex, i.uv + float2( t.x,  t.y));

                return c * 0.25;
            }
            ENDCG
        }
    }

    Fallback Off
}
