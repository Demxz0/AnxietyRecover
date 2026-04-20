using UnityEditor;
using UnityEngine;

public class ShaderChanger
{
    [MenuItem("Tools/Change Selected Materials To URP Lit")]
    public static void ChangeShaders()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        Object[] selectedMaterials = Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets);

        foreach (Object obj in selectedMaterials)
        {
            Material mat = (Material)obj;

            Texture baseMap = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Texture normalMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            Texture metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            Texture occlusionMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;

            Color baseColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

            float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;

            Vector2 textureScale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : new Vector2(1, 1);
            Vector2 textureOffset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : new Vector2(0, 0);

            float renderMode = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0f;
            float cullMode = mat.HasProperty("_Cull") ? mat.GetFloat("_Cull") : 2f;

            mat.shader = urpLit;

            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);

            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (metallicMap != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallicMap);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (emissionMap != null)
            {
                mat.SetTexture("_EmissionMap", emissionMap);
                mat.EnableKeyword("_EMISSION");
            }

            if (occlusionMap != null) mat.SetTexture("_OcclusionMap", occlusionMap);

            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_EmissionColor", emissionColor);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTextureScale("_BaseMap", textureScale);
                mat.SetTextureOffset("_BaseMap", textureOffset);
            }

            mat.SetFloat("_Cull", cullMode);

            if (renderMode == 1f)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = 2450;
            }
            else if (renderMode == 2f || renderMode == 3f)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
            }
        }
    }
}
