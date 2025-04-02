Shader "Unlit/Water"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float _amp;
            float _swell;
            float _normalStrength;
            float _xLengthScale;
            float _zLengthScale;
            float _foamCutoff;

            sampler2D _heightmap;
            sampler2D _xSlopeTexture;
            sampler2D _zSlopeTexture;
            sampler2D _xDisplacementTexture;
            sampler2D _zDisplacementTexture;
            sampler2D _xDxDisplacementTexture;
            sampler2D _zDzDisplacementTexture;
            sampler2D _xDzDisplacementtexture;

            samplerCUBE _envMap;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float2 worldUV : TEXCOORD2;
                float3 wPos : TEXCOORD3;
                float4 vertex : SV_POSITION;
            };

            float pow5(float a)
            {
                return a*a*a*a*a;
            }

            v2f vert (appdata v)
            {
                v2f o;

                float4 wPos = mul(unity_ObjectToWorld, v.vertex);

                float2 worldUV = float2(wPos.x / _xLengthScale, wPos.z / _zLengthScale);

                float height = _amp * tex2Dlod(_heightmap, float4(worldUV,0,0)).x;
                float xDisp = _amp * tex2Dlod(_xDisplacementTexture, float4(worldUV, 0, 0)).x;
                float zDisp = _amp * tex2Dlod(_zDisplacementTexture, float4(worldUV, 0, 0)).x;

                wPos += float4(_swell * xDisp, height, _swell * zDisp, 0);

                o.vertex = mul(UNITY_MATRIX_VP, wPos);

                o.wPos = wPos;
                o.uv = v.uv;
                o.normal = v.normal;
                o.worldUV = worldUV;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 waterCol = float3(0,0.5,0.75);
                float3 foamCol = float3(0.9,0.9,1);

                // read texture data
                float xSlope = _amp * tex2D(_xSlopeTexture, i.worldUV).x;
                float zSlope = _amp * tex2D(_zSlopeTexture, i.worldUV).x;
                float xDxDisp = _swell * _amp * tex2D(_xDxDisplacementTexture, i.worldUV).x;
                float zDzDisp = _swell * _amp * tex2D(_zDzDisplacementTexture, i.worldUV).x;
                float xDzDisp = _swell * _amp * tex2D(_xDzDisplacementtexture, i.worldUV).x;

                float jacobian = (1 + xDxDisp) * (1 + zDzDisp) - pow(xDzDisp, 2);
                bool foam = jacobian <= _foamCutoff;

                float3 normal = float3(- _normalStrength * xSlope / (1 + xDxDisp), 1, 
                    - _normalStrength * zSlope / (1 + zDzDisp));
                normal = normalize(normal);

                // Directions for lighting
                float3 sunDir = _WorldSpaceLightPos0.xyz;
                float3 viewDir = normalize(-i.wPos + _WorldSpaceCameraPos); // wPos to camera
                float3 reflectedViewDir = reflect(-viewDir, normal);
                float3 halfwayDir = normalize(sunDir + normal);

                // Schlick fresnel
                float R0 = (0.33 / 2.33) * (0.33 / 2.33);
                float RTheta = R0 + (1 - R0) * pow5(1 - dot(viewDir, normal));

                // sampling environment map
                float3 envColor = texCUBE(_envMap, reflectedViewDir).rgb;
                envColor *= RTheta;

                float diffuse = saturate(dot(normal, sunDir));
                waterCol *= diffuse;
                float3 surfaceColor = lerp(envColor, foamCol, foam);
                float4 output = float4(surfaceColor, 1);

                return output;
            }
            ENDCG
        }
    }
}
