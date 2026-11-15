#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Fábrica inteligente de materiais toon (Distant Lands / URP compatível).
    /// Em vez de produzir um único material chapado por papel, a fábrica cria/reaproveita
    /// materiais persistentes respeitando a textura original, normal maps, transparência,
    /// cutoff, culling e configurações do perfil Soft Toon.
    /// </summary>
    public static class ToonMaterialFactory
    {
        public const string DefaultToonRoot = "Assets/Piaaria/Art/Pizzeria/Toon/Materials";

        [System.Serializable]
        public class MaterialManifestItem
        {
            public string role;
            public string sourceMaterialName;
            public string sourceMaterialGuid;
            public string targetMaterialPath;
            public string targetMaterialGuid;
            public string textureName;
            public string normalMapName;
            public string shaderUsed;
            public bool preserved;
            public string reason;
        }

        [System.Serializable]
        public class MaterialManifest
        {
            public string generatedAt;
            public string profileName;
            public List<MaterialManifestItem> items = new List<MaterialManifestItem>();
        }

        public static string GetSubfolderForRole(MaterialPaletteRole role)
        {
            switch (role)
            {
                case MaterialPaletteRole.ParedeClara:
                case MaterialPaletteRole.FachadaDestaque:
                case MaterialPaletteRole.PisoSalao:
                case MaterialPaletteRole.PisoCozinha:
                case MaterialPaletteRole.Escuro:
                case MaterialPaletteRole.Madeira:
                case MaterialPaletteRole.Vidro:
                    return "Architecture";

                case MaterialPaletteRole.FornoLenha:
                case MaterialPaletteRole.PortaForno:
                case MaterialPaletteRole.Geladeira:
                case MaterialPaletteRole.Armario:
                case MaterialPaletteRole.Balcao:
                case MaterialPaletteRole.Mesa:
                case MaterialPaletteRole.Cadeira:
                    return "Kitchen";

                case MaterialPaletteRole.Faca:
                case MaterialPaletteRole.Faqueiro:
                case MaterialPaletteRole.TabuaCorte:
                case MaterialPaletteRole.CortadorPizza:
                case MaterialPaletteRole.Espatula:
                case MaterialPaletteRole.FormaPizza:
                case MaterialPaletteRole.PanelaConcha:
                case MaterialPaletteRole.RecipienteMolho:
                case MaterialPaletteRole.CaixaRegistradora:
                case MaterialPaletteRole.CardapioPrancheta:
                case MaterialPaletteRole.Lixeira:
                case MaterialPaletteRole.Metal:
                case MaterialPaletteRole.DestaqueSecundario:
                    return "Props";

                case MaterialPaletteRole.PizzaPronta:
                case MaterialPaletteRole.CaixaPizza:
                case MaterialPaletteRole.DeliveryBag:
                    return "Food";

                default:
                    return "Props";
            }
        }

        /// <summary>
        /// Obtém ou cria um material toon estilizado determinístico para o slot e papel fornecidos.
        /// Se o material de origem contiver textura ou normal map, estes são transferidos
        /// para as propriedades Distant Lands (_Texture, _NormalMap).
        /// </summary>
        public static Material GetOrCreateToonMaterial(
            Material sourceMat,
            MaterialPaletteRole role,
            PizzeriaAppearanceProfile profile,
            MaterialManifest manifest = null)
        {
            if (role == MaterialPaletteRole.Preserve)
            {
                manifest?.items.Add(new MaterialManifestItem
                {
                    role = role.ToString(),
                    sourceMaterialName = sourceMat != null ? sourceMat.name : "(null)",
                    preserved = true,
                    reason = "MaterialPaletteRole.Preserve definido pelo autor"
                });
                return sourceMat;
            }

            profile.EnsureShaders();

            // Identifica shaders Distant Lands
            Shader opaqueShader = profile.opaqueShader != null 
                ? profile.opaqueShader 
                : Shader.Find("Distant Lands/Illustrate/Opaque");
            Shader transparentShader = Shader.Find("Distant Lands/Illustrate/Transparent");
            Shader alphaClippedShader = Shader.Find("Distant Lands/Illustrate/Alpha Clipped");

            if (opaqueShader == null)
            {
                opaqueShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            }

            // Seleciona subpasta de organização
            string subFolder = GetSubfolderForRole(role);
            string folderPath = $"{DefaultToonRoot}/{subFolder}";
            EnsureDirectory(folderPath);

            // Determina se a origem é transparente ou alpha clipped
            bool isTransparent = false;
            bool isAlphaClipped = false;
            Texture mainTex = null;
            Vector2 mainScale = Vector2.one;
            Vector2 mainOffset = Vector2.zero;
            Texture normalMap = null;
            Vector2 normalScale = Vector2.one;
            Vector2 normalOffset = Vector2.zero;
            Color sourceColor = Color.white;

            if (sourceMat != null)
            {
                // Extrai textura principal
                if (sourceMat.HasProperty("_BaseMap") && sourceMat.GetTexture("_BaseMap") != null)
                {
                    mainTex = sourceMat.GetTexture("_BaseMap");
                    mainScale = sourceMat.GetTextureScale("_BaseMap");
                    mainOffset = sourceMat.GetTextureOffset("_BaseMap");
                }
                else if (sourceMat.HasProperty("_MainTex") && sourceMat.GetTexture("_MainTex") != null)
                {
                    mainTex = sourceMat.GetTexture("_MainTex");
                    mainScale = sourceMat.GetTextureScale("_MainTex");
                    mainOffset = sourceMat.GetTextureOffset("_MainTex");
                }
                else if (sourceMat.HasProperty("_Texture") && sourceMat.GetTexture("_Texture") != null)
                {
                    mainTex = sourceMat.GetTexture("_Texture");
                    mainScale = sourceMat.GetTextureScale("_Texture");
                    mainOffset = sourceMat.GetTextureOffset("_Texture");
                }

                // Extrai normal map
                if (sourceMat.HasProperty("_BumpMap") && sourceMat.GetTexture("_BumpMap") != null)
                {
                    normalMap = sourceMat.GetTexture("_BumpMap");
                    normalScale = sourceMat.GetTextureScale("_BumpMap");
                    normalOffset = sourceMat.GetTextureOffset("_BumpMap");
                }
                else if (sourceMat.HasProperty("_NormalMap") && sourceMat.GetTexture("_NormalMap") != null)
                {
                    normalMap = sourceMat.GetTexture("_NormalMap");
                    normalScale = sourceMat.GetTextureScale("_NormalMap");
                    normalOffset = sourceMat.GetTextureOffset("_NormalMap");
                }

                if (sourceMat.HasProperty("_BaseColor"))
                    sourceColor = sourceMat.GetColor("_BaseColor");
                else if (sourceMat.HasProperty("_Color"))
                    sourceColor = sourceMat.GetColor("_Color");
                else if (sourceMat.HasProperty("_MainColor"))
                    sourceColor = sourceMat.GetColor("_MainColor");

                // Avalia transparência / clipping
                if (role == MaterialPaletteRole.Vidro ||
                    (sourceMat.HasProperty("_Surface") && Mathf.Approximately(sourceMat.GetFloat("_Surface"), 1f)) ||
                    (sourceMat.shader != null && sourceMat.shader.name.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    isTransparent = true;
                }
                else if ((sourceMat.HasProperty("_AlphaClip") && sourceMat.GetFloat("_AlphaClip") > 0.5f) ||
                         (sourceMat.shader != null && sourceMat.shader.name.IndexOf("Alpha", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    isAlphaClipped = true;
                }
            }
            else
            {
                if (role == MaterialPaletteRole.Vidro)
                    isTransparent = true;
            }

            // Escolha do shader final
            Shader finalShader = opaqueShader;
            if (isTransparent && transparentShader != null)
                finalShader = transparentShader;
            else if (isAlphaClipped && alphaClippedShader != null)
                finalShader = alphaClippedShader;

            // Determina nome determinístico único para o material
            string sourceGuid = "";
            string sourceName = "Default";
            if (sourceMat != null)
            {
                sourceName = sourceMat.name;
                string p = AssetDatabase.GetAssetPath(sourceMat);
                if (!string.IsNullOrEmpty(p))
                    sourceGuid = AssetDatabase.AssetPathToGUID(p);
            }

            string texPart = mainTex != null ? $"_{mainTex.name}" : "";
            string cleanSourceName = SanitizeFileName(sourceName);
            string cleanTexPart = SanitizeFileName(texPart);
            if (cleanTexPart.Length > 20) cleanTexPart = cleanTexPart.Substring(0, 20);

            string assetName = $"Mat_Toon_{role}_{cleanSourceName}{cleanTexPart}.mat";
            if (assetName.Length > 80)
            {
                string hash = ComputeShortHash($"{role}_{sourceName}_{texPart}");
                assetName = $"Mat_Toon_{role}_{hash}.mat";
            }

            string assetPath = $"{folderPath}/{assetName}";

            Material targetMat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            bool isNew = false;
            if (targetMat == null)
            {
                targetMat = new Material(finalShader);
                isNew = true;
            }
            else if (targetMat.shader != finalShader)
            {
                targetMat.shader = finalShader;
            }

            // Cor base a aplicar
            Color roleColor = profile.GetRoleColor(role);

            // Aplica propriedades no shader Distant Lands
            ConfigureToonProperties(targetMat, roleColor, mainTex, mainScale, mainOffset, normalMap, normalScale, normalOffset, isTransparent, isAlphaClipped);

            if (isNew)
            {
                AssetDatabase.CreateAsset(targetMat, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(targetMat);
            }

            manifest?.items.Add(new MaterialManifestItem
            {
                role = role.ToString(),
                sourceMaterialName = sourceName,
                sourceMaterialGuid = sourceGuid,
                targetMaterialPath = assetPath,
                targetMaterialGuid = AssetDatabase.AssetPathToGUID(assetPath),
                textureName = mainTex != null ? mainTex.name : "(none)",
                normalMapName = normalMap != null ? normalMap.name : "(none)",
                shaderUsed = finalShader != null ? finalShader.name : "(none)",
                preserved = false,
                reason = "Toon Material gerado com sucesso"
            });

            return targetMat;
        }

        private static void ConfigureToonProperties(
            Material mat,
            Color roleColor,
            Texture mainTex,
            Vector2 mainScale,
            Vector2 mainOffset,
            Texture normalMap,
            Vector2 normalScale,
            Vector2 normalOffset,
            bool isTransparent,
            bool isAlphaClipped)
        {
            // Cores
            if (mat.HasProperty("_MainColor"))
                mat.SetColor("_MainColor", roleColor);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", roleColor);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", roleColor);

            // Texturas
            if (mainTex != null)
            {
                if (mat.HasProperty("_Texture"))
                {
                    mat.SetTexture("_Texture", mainTex);
                    mat.SetTextureScale("_Texture", mainScale);
                    mat.SetTextureOffset("_Texture", mainOffset);
                }
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", mainTex);
                    mat.SetTextureScale("_BaseMap", mainScale);
                    mat.SetTextureOffset("_BaseMap", mainOffset);
                }
            }

            // Normais
            if (normalMap != null)
            {
                if (mat.HasProperty("_NormalMap"))
                {
                    mat.SetTexture("_NormalMap", normalMap);
                    mat.SetTextureScale("_NormalMap", normalScale);
                    mat.SetTextureOffset("_NormalMap", normalOffset);
                }
                if (mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", normalMap);
                    mat.SetTextureScale("_BumpMap", normalScale);
                    mat.SetTextureOffset("_BumpMap", normalOffset);
                }
            }

            // Shading limpo (Soft Toon)
            if (mat.HasProperty("_LightingMode"))
                mat.SetFloat("_LightingMode", 1f); // Lit

            if (mat.HasProperty("_PosterizeLight"))
                mat.SetFloat("_PosterizeLight", 1f); // Degraus toon de iluminação

            if (mat.HasProperty("_LightSteps"))
                mat.SetFloat("_LightSteps", 3f); // 3 níveis limpos de iluminação toon

            if (mat.HasProperty("_UseShadows"))
                mat.SetFloat("_UseShadows", 1f);

            if (mat.HasProperty("_ShadowColor"))
            {
                // Sombra fria/neutra derivada da cor base com atenuação suave
                Color shadow = Color.Lerp(roleColor * 0.55f, new Color(0.40f, 0.42f, 0.48f, 1f), 0.35f);
                mat.SetColor("_ShadowColor", shadow);
            }

            if (mat.HasProperty("_LightRamp"))
                mat.SetFloat("_LightRamp", 1.0f);

            if (mat.HasProperty("_LightRampOffset"))
                mat.SetFloat("_LightRampOffset", 0.5f);

            // Desativa perturbações em objetos estáticos
            if (mat.HasProperty("_UseWave"))
                mat.SetFloat("_UseWave", 0f);

            if (mat.HasProperty("_UseHSVVariation"))
                mat.SetFloat("_UseHSVVariation", 0f);

            if (mat.HasProperty("_UseTriplanar"))
                mat.SetFloat("_UseTriplanar", 0f);

            if (mat.HasProperty("_UseGlint"))
                mat.SetFloat("_UseGlint", 0f);

            if (mat.HasProperty("_PosterizeColors"))
                mat.SetFloat("_PosterizeColors", 0f); // Preserva cores de textura sem quantização destrutiva

            if (mat.HasProperty("_UseRimLighting"))
                mat.SetFloat("_UseRimLighting", 0f); // Rim suave apenas em personagens

            if (mat.HasProperty("_CullMode"))
                mat.SetFloat("_CullMode", 2f); // Back culling padrão

            if (isTransparent)
            {
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                if (mat.HasProperty("_MainColor"))
                {
                    Color glass = roleColor;
                    glass.a = 0.45f;
                    mat.SetColor("_MainColor", glass);
                }
            }
        }

        private static void EnsureDirectory(string folderPath)
        {
            if (Directory.Exists(folderPath)) return;
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (Array.IndexOf(invalid, c) >= 0 || c == ' ' || c == '.' || c == '(' || c == ')')
                    continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static string ComputeShortHash(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 8);
            }
        }
    }
}
#endif
