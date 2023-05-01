Shader "Hidden/LiquidImpact" {
    Properties {
		_Parameters ("Parameters", Vector) = (0.5, 0.5, 0.2, 0.2)
		_Strength ("Strength", float) = 1.0
    }
    SubShader {
		Tags{"PreviewType"="Plane"}

        // Write only into the height value (R) not the velocity (B)
		ColorMask R

		Blend One One

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct vertOut {
                float2 texcoord : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            half4 _Parameters;
			half _Strength;

            vertOut vert (float4 vertex : POSITION, float2 texcoord : TEXCOORD0) {
                vertOut output;

                output.vertex = UnityObjectToClipPos(vertex);
                output.texcoord = texcoord;

                return output;
            }

            fixed4 frag (vertOut input) : SV_Target {
                // Get radial falloff around the position
                float mask = length((input.texcoord - _Parameters.xy) / _Parameters.zw) * 2.0f;
				
				half alpha = saturate(1 - (mask * mask));

				return float4(alpha * _Strength, 0, 0, 0);
            }
            ENDCG
        }
    }
}
