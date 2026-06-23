using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attach temporarily or run from context menu to inspect an impostor material.
/// This does not replace your baker. It helps verify whether the material is using a white atlas.
/// </summary>
public class ImpostorMaterialDebugFix : MonoBehaviour
{
    [SerializeField] private Material impostorMaterial;
    [SerializeField] private Texture2D expectedAlbedoAtlas;

    [ContextMenu("Debug Impostor Material")]
    public void DebugMaterial()
    {
        if (impostorMaterial == null)
        {
            Debug.LogError("[ImpostorMaterialDebugFix] No impostor material assigned.");
            return;
        }

        Texture baseMap = impostorMaterial.HasProperty("_BaseMap") ? impostorMaterial.GetTexture("_BaseMap") : null;
        Texture mainTex = impostorMaterial.HasProperty("_MainTex") ? impostorMaterial.GetTexture("_MainTex") : null;

        Debug.Log($"[ImpostorMaterialDebugFix] Material: {impostorMaterial.name}");
        Debug.Log($"[ImpostorMaterialDebugFix] Shader: {impostorMaterial.shader.name}");
        Debug.Log($"[ImpostorMaterialDebugFix] _BaseMap: {(baseMap == null ? "NULL" : baseMap.name)}");
        Debug.Log($"[ImpostorMaterialDebugFix] _MainTex: {(mainTex == null ? "NULL" : mainTex.name)}");

        Texture tex = baseMap != null ? baseMap : mainTex;
        if (tex is Texture2D texture2D)
            Debug.Log($"[ImpostorMaterialDebugFix] Atlas average colour: {GetAverageColor(texture2D)}");
    }

    [ContextMenu("Force Assign Atlas And Shader")]
    public void ForceAssignAtlasAndShader()
    {
        if (impostorMaterial == null)
        {
            Debug.LogError("[ImpostorMaterialDebugFix] No impostor material assigned.");
            return;
        }

        Shader fixedShader = Shader.Find("AAAOptimizer/Impostor/HDRPUnlitAtlasCutout");
#if UNITY_EDITOR
        if (fixedShader == null)
        {
            AssetDatabase.ImportAsset("Assets/AAAOptimizer/Shaders/AAAImpostorHDRPUnlitAtlasCutout_Fixed.shader", ImportAssetOptions.ForceUpdate);
            fixedShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/AAAOptimizer/Shaders/AAAImpostorHDRPUnlitAtlasCutout_Fixed.shader");
        }
#endif
        if (fixedShader == null)
        {
            Debug.LogError("[ImpostorMaterialDebugFix] Missing shader AAAOptimizer/Impostor/HDRPUnlitAtlasCutout.");
            return;
        }
        impostorMaterial.shader = fixedShader;

        if (expectedAlbedoAtlas != null)
        {
            impostorMaterial.SetTexture("_BaseMap", expectedAlbedoAtlas);
            impostorMaterial.SetTexture("_MainTex", expectedAlbedoAtlas);
        }

        impostorMaterial.SetColor("_BaseColor", Color.white);
        impostorMaterial.SetColor("_Color", Color.white);
        impostorMaterial.SetFloat("_Cutoff", 0.05f);

#if UNITY_EDITOR
        EditorUtility.SetDirty(impostorMaterial);
        AssetDatabase.SaveAssets();
#endif

        Debug.Log("[ImpostorMaterialDebugFix] Forced fixed shader and atlas assignment.");
    }

    private Color GetAverageColor(Texture2D tex)
    {
        if (tex == null)
            return Color.clear;

#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(tex);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        bool changed = false;

        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            changed = true;
            importer.SaveAndReimport();
        }
#endif

        Color[] pixels = tex.GetPixels();
        if (pixels == null || pixels.Length == 0)
            return Color.clear;

        Color total = Color.clear;
        int count = 0;

        foreach (Color c in pixels)
        {
            if (c.a <= 0.05f)
                continue;

            total += c;
            count++;
        }

        return count == 0 ? Color.clear : total / count;
    }
}
