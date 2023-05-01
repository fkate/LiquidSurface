Shader "Hidden/LiquidSpread" {
    Properties {
		_MainTex ("MainTex", 2D) = "grey" 
		_TexSize ("TexSize", Vector) = (64, 64, 0, 0)
		_Parameters ("Parameters", Vector) = (1, 1, 1, 1)
		_DeltaTime ("DeltaTime", float) = 1
    }
    SubShader {
		Tags{"PreviewType"="Plane"}

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct vertOut {
                float2 texcoord : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

			sampler2D _MainTex;

			half4 _TexSize;
			half4 _Parameters;
			float _DeltaTime;

            vertOut vert (float4 vertex : POSITION, float2 texcoord : TEXCOORD0) {
                vertOut output;

                output.vertex = UnityObjectToClipPos(vertex);
                output.texcoord = texcoord;

                return output;
            }

            fixed4 frag (vertOut input) : SV_Target {
				// Spring values
				float targetLength = _Parameters.x;
				float tension = _Parameters.y;
				float dampening = _Parameters.z;
				float spread = _Parameters.w;

				float2 frameTex = tex2D(_MainTex, input.texcoord).xy;
				float position = frameTex.x;
				float velocity = frameTex.y;

				// Check neighbour pixels for their values
                float2 offset = (1.0 / _TexSize);

				half4 directions;
				directions.x = spread * (tex2D(_MainTex, input.texcoord + float2(-offset.x, 0)).x - position);
				directions.y = spread * (tex2D(_MainTex, input.texcoord + float2(offset.x, 0)).x - position);
				directions.z = spread * (tex2D(_MainTex, input.texcoord + float2(0, -offset.y)).x - position);
				directions.w = spread * (tex2D(_MainTex, input.texcoord + float2(0, offset.y)).x - position);

				directions.x *= input.texcoord.x - offset.x <= 0 ? 0 : 1;
				directions.y *= input.texcoord.x + offset.x >= 1 ? 0 : 1;
				directions.z *= input.texcoord.y - offset.y <= 0 ? 0 : 1;
				directions.w *= input.texcoord.y + offset.y >= 1 ? 0 : 1;

				// Propaginate the wave
				half propagation = directions.x + directions.y + directions.z + directions.w;

				float acceleration = (targetLength - position) * tension - velocity * dampening;

				velocity += acceleration + propagation;				
				position += velocity;

				return half4(position, velocity, 0, 1);
            }
            ENDCG
        }
    }
}
