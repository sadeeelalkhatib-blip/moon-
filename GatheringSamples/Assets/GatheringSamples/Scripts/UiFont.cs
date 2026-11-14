using TMPro;
using UnityEngine;

namespace GatheringSamples
{
    public static class UiFont
    {
        private static TMP_FontAsset font;

        public static TMP_FontAsset Get()
        {
            if (font != null) return font;

            font = Resources.Load<TMP_FontAsset>("Fonts/Inter-Variable SDF");
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
            }
            return font;
        }
    }
}
