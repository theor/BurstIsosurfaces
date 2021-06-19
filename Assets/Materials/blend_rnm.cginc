//UNITY_SHADER_NO_UPGRADE
#ifndef MYHLSLINCLUDE_INCLUDED
#define MYHLSLINCLUDE_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
half3 blend_rnm(half3 n1, half3 n2)
{
    n1.z += 1;
    n2.xy = -n2.xy;

    return n1 * dot(n1, n2) / n1.z - n2;
}


// void triplanar_float(TEXTURE2D_PARAM(_BumpMap, sampler_BumpMap), TEXTURE2D_PARAM(_MainTex,sampler_MainTex), float3 worldPos, float3 worldNormal, out float3 outNormal, out float3 outAlbedo)
void triplanar_float(UnityTexture2D _BumpMap, UnityTexture2D _MainTex, float4 mainTilingOffset, UnityTexture2D _SideBump, UnityTexture2D _SideTex, float4 sideTilingOffset, float3 worldPos, float3 worldNormal, float blendingPower, out float3 outNormal, out float3 outAlbedo)
{
    half3 triblend = saturate(pow(abs(worldNormal), blendingPower));
    triblend /= max(dot(triblend, half3(1,1,1)), 0.0001); 
    // calculate triplanar uvs
    // applying texture scale and offset values ala TRANSFORM_TEX macro
    float2 uvX = worldPos.zy * sideTilingOffset.xy + sideTilingOffset.zy;
    float2 uvY = worldPos.xz * mainTilingOffset.xy + mainTilingOffset.zy;
    float2 uvZ = worldPos.xy * sideTilingOffset.xy + sideTilingOffset.zy;
                                                                  
      // offset UVs to prevent obvious mirroring
#if defined(TRIPLANAR_UV_OFFSET)
      uvY += 0.33;
      uvZ += 0.67;
#endif

    // minor optimization of sign(). prevents return value of 0
    half3 axisSign = worldNormal < 0 ? -1 : 1;
    // albedo textures
    float4 colX =  tex2D(_SideTex, uvX); // float4(1,0,0,1);//
    float4 colY = tex2D(_MainTex, uvY); // float4(0,1,0,1);//
    float4 colZ = tex2D(_SideTex, uvZ); // float4(0,0,1,1);//
    float4 col = colX * triblend.x + colY * triblend.y + colZ * triblend.z;

    // occlusion textures
    // half occX = tex2D(_OcclusionMap, uvX).g;
    // half occY = tex2D(_OcclusionMap, uvY).g;
    // half occZ = tex2D(_OcclusionMap, uvZ).g;
    // half occ = LerpOneTo(occX * triblend.x + occY * triblend.y + occZ * triblend.z, _OcclusionStrength);

    // tangent space normal maps 
    half3 tnormalX = UnpackNormal(tex2D(_SideBump, uvX));
    half3 tnormalY = UnpackNormal(tex2D(_BumpMap, uvY));
    half3 tnormalZ = UnpackNormal(tex2D(_SideBump, uvZ));

    // flip normal maps' x axis to account for flipped UVs
#if defined(TRIPLANAR_CORRECT_PROJECTED_U)
            tnormalX.x *= axisSign.x;
            tnormalY.x *= axisSign.y;
            tnormalZ.x *= -axisSign.z;
        #endif

    half3 absVertNormal = abs(worldNormal);

    // swizzle world normals to match tangent space and apply reoriented normal mapping blend
    tnormalX = blend_rnm(half3(worldNormal.zy, absVertNormal.x), tnormalX);
    tnormalY = blend_rnm(half3(worldNormal.xz, absVertNormal.y), tnormalY);
    tnormalZ = blend_rnm(half3(worldNormal.xy, absVertNormal.z), tnormalZ);

    // apply world space sign to tangent space Z
    tnormalX.z *= axisSign.x;
    tnormalY.z *= axisSign.y;
    tnormalZ.z *= axisSign.z;

    // sizzle tangent normals to match world normal and blend together
    worldNormal = normalize(
        tnormalX.zyx * triblend.x +
        tnormalY.xzy * triblend.y +
        tnormalZ.xyz * triblend.z
        );
        
    outNormal = worldNormal;
    outAlbedo = col.rgb;

    //// set surface ouput properties
    //o.Albedo = col.rgb;
    //o.Metallic = _Metallic;
    //o.Smoothness = _Glossiness;
    //o.Occlusion = occ;
//
            //// convert world space normals into tangent normals
            //o.Normal = WorldToTangentNormalVector(IN, worldNormal);
}

#endif //MYHLSLINCLUDE_INCLUDED