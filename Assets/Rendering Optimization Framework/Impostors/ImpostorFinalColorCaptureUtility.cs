using UnityEngine;

/// <summary>
/// Utility functions for correct impostor colour extraction.
/// Use this inside your baker instead of reading a transparent render only.
/// </summary>
public static class ImpostorFinalColorCaptureUtility
{
    /// <summary>
    /// Reconstructs straight-alpha colour from white and black background renders.
    /// This avoids the common white/grey impostor issue caused by transparent RT capture.
    /// </summary>
    public static Color32[] BuildAlbedoFromWhiteBlack(Color[] blackPixels, Color[] whitePixels)
    {
        int len = blackPixels.Length;
        Color32[] output = new Color32[len];

        for (int i = 0; i < len; i++)
        {
            Color cb = blackPixels[i];
            Color cw = whitePixels[i];

            float alphaR = 1f - Mathf.Clamp01(cw.r - cb.r);
            float alphaG = 1f - Mathf.Clamp01(cw.g - cb.g);
            float alphaB = 1f - Mathf.Clamp01(cw.b - cb.b);
            float alpha = Mathf.Clamp01((alphaR + alphaG + alphaB) / 3f);

            Color rgb = alpha > 0.0001f ? cb / alpha : Color.clear;
            rgb.r = Mathf.Clamp01(rgb.r);
            rgb.g = Mathf.Clamp01(rgb.g);
            rgb.b = Mathf.Clamp01(rgb.b);
            rgb.a = alpha;

            output[i] = rgb;
        }

        return output;
    }

    public static Color32[] BuildAlphaOnlyFromWhiteBlack(Color[] blackPixels, Color[] whitePixels)
    {
        int len = blackPixels.Length;
        Color32[] output = new Color32[len];

        for (int i = 0; i < len; i++)
        {
            Color cb = blackPixels[i];
            Color cw = whitePixels[i];

            float alphaR = 1f - Mathf.Clamp01(cw.r - cb.r);
            float alphaG = 1f - Mathf.Clamp01(cw.g - cb.g);
            float alphaB = 1f - Mathf.Clamp01(cw.b - cb.b);
            float alpha = Mathf.Clamp01((alphaR + alphaG + alphaB) / 3f);

            output[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
        }

        return output;
    }
}
