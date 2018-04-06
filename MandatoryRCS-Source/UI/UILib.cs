/* 
 * This file and all code it contains is released in the public domain
 */

using System.IO;
using UnityEngine;

namespace MandatoryRCS.UI
{
    public static class UILib
    {
        public static Sprite GetSprite(string textureName)
        {
            Texture2D texture = GetTexture(textureName);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(texture.width * 0.5f, texture.height * 0.5f));
        }

        public static Texture2D GetTexture(string textureName)
        {
            Debug.Log("loading : " + "MandatoryRCS/Resources/" + textureName);
            Texture2D texture = GameDatabase.Instance.GetTexture("MandatoryRCS/Resources/" + textureName, false);

            texture.filterMode = FilterMode.Bilinear; // FilterMode.Trilinear is too blurry
            return texture;
        }

        public static Texture2D LoadTexture(string FilePath)
        {

            // Load a PNG or JPG file from disk to a Texture2D
            // Returns null if load fails

            Texture2D Tex2D;
            byte[] FileData;

            if (File.Exists(FilePath))
            {
                FileData = File.ReadAllBytes(FilePath);
                Tex2D = new Texture2D(2, 2);           // Create new "empty" texture
                if (Tex2D.LoadImage(FileData))           // Load the imagedata into the texture (size is set automatically)
                    return Tex2D;                 // If data = readable -> return texture
            }
            return null;                     // Return null if load failed
        }

        // Get a texture2D, bypassing Unity asset read limitations
        public static Texture2D GetReadOnlyTexture(Texture2D source)
        {
            source.filterMode = FilterMode.Point;
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height);
            rt.filterMode = FilterMode.Point;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            Texture2D nTex = new Texture2D(source.width, source.height);
            nTex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            nTex.Apply();
            RenderTexture.active = null;
            return nTex;

        }

        static KSP.UI.TooltipTypes.Tooltip_Text _tooltipPrefab = null;
        public static KSP.UI.TooltipTypes.Tooltip_Text tooltipPrefab
        {
            get
            {
                if (_tooltipPrefab == null)
                {
                    _tooltipPrefab = AssetBase.GetPrefab<KSP.UI.TooltipTypes.Tooltip_Text>("Tooltip_Text");
                }
                return _tooltipPrefab;
            }
        }
    }
}
