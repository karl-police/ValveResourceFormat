using System;
using System.Collections.Generic;
using System.Numerics;
using GUI.Utils;
using OpenTK.Graphics.OpenGL;
using ValveResourceFormat;
using ValveResourceFormat.ResourceTypes;
using VrfMaterial = ValveResourceFormat.ResourceTypes.Material;

namespace GUI.Types.Renderer
{
    public class MaterialLoader
    {
        private readonly Dictionary<string, RenderMaterial> Materials = new Dictionary<string, RenderMaterial>();
        private readonly VrfGuiContext VrfGuiContext;
        private int ErrorTextureID;
        public static int MaxTextureMaxAnisotropy { get; set; }

        public MaterialLoader(VrfGuiContext guiContext)
        {
            VrfGuiContext = guiContext;
        }

        public RenderMaterial GetMaterial(string name)
        {
            if (Materials.ContainsKey(name))
            {
                return Materials[name];
            }

            var resource = VrfGuiContext.LoadFileByAnyMeansNecessary(name + "_c");
            var mat = LoadMaterial(resource);

            Materials.Add(name, mat);

            return mat;
        }

        public RenderMaterial LoadMaterial(Resource resource)
        {
            if (resource == null)
            {
                var errorMat = new RenderMaterial(new VrfMaterial());
                errorMat.Textures["g_tColor"] = GetErrorTexture();
                errorMat.Material.ShaderName = "vrf.error";

                return errorMat;
            }

            var mat = new RenderMaterial((VrfMaterial)resource.DataBlock);

            foreach (var textureReference in mat.Material.TextureParams)
            {
                var key = textureReference.Key;

                mat.Textures[key] = LoadTexture(textureReference.Value);
            }

            if (mat.Material.IntParams.ContainsKey("F_SOLID_COLOR") && mat.Material.IntParams["F_SOLID_COLOR"] == 1)
            {
                var a = mat.Material.VectorParams["g_vColorTint"];

                mat.Textures["g_tColor"] = GenerateColorTexture(1, 1, new[] { a.X, a.Y, a.Z, a.W });
            }

            if (!mat.Textures.ContainsKey("g_tColor"))
            {
                mat.Textures["g_tColor"] = GetErrorTexture();
            }

            // Since our shaders only use g_tColor, we have to find at least one texture to use here
            if (mat.Textures["g_tColor"] == GetErrorTexture())
            {
                var namesToTry = new[] { "g_tColor2", "g_tColor1", "g_tColorA", "g_tColorB", "g_tColorC" };

                foreach (var name in namesToTry)
                {
                    if (mat.Textures.ContainsKey(name))
                    {
                        mat.Textures["g_tColor"] = mat.Textures[name];
                        break;
                    }
                }
            }

            // Set default values for scale and positions
            if (!mat.Material.VectorParams.ContainsKey("g_vTexCoordScale"))
            {
                mat.Material.VectorParams["g_vTexCoordScale"] = Vector4.One;
            }

            if (!mat.Material.VectorParams.ContainsKey("g_vTexCoordOffset"))
            {
                mat.Material.VectorParams["g_vTexCoordOffset"] = Vector4.Zero;
            }

            if (!mat.Material.VectorParams.ContainsKey("g_vColorTint"))
            {
                mat.Material.VectorParams["g_vColorTint"] = Vector4.One;
            }

            return mat;
        }

        public int LoadTexture(string name)
        {
            var textureResource = VrfGuiContext.LoadFileByAnyMeansNecessary(name + "_c");

            if (textureResource == null)
            {
                return GetErrorTexture();
            }

            return LoadTexture(textureResource);
        }

        public int LoadTexture(Resource textureResource)
        {
            var tex = (Texture)textureResource.DataBlock;

            var id = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, id);

            var textureReader = textureResource.Reader;
            textureReader.BaseStream.Position = tex.Offset + tex.Size;

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, tex.NumMipLevels - 1);

            InternalFormat format;

            switch (tex.Format)
            {
                case VTexFormat.DXT1: format = InternalFormat.CompressedRgbaS3tcDxt1Ext; break;
                case VTexFormat.DXT5: format = InternalFormat.CompressedRgbaS3tcDxt5Ext; break;
                case VTexFormat.ETC2: format = InternalFormat.CompressedRgb8Etc2; break;
                case VTexFormat.ETC2_EAC: format = InternalFormat.CompressedRgba8Etc2Eac; break;
                case VTexFormat.ATI1N: format = InternalFormat.CompressedRedRgtc1; break;
                case VTexFormat.ATI2N: format = InternalFormat.CompressedRgRgtc2; break;
                case VTexFormat.BC6H: format = InternalFormat.CompressedRgbBptcUnsignedFloat; break;
                case VTexFormat.BC7: format = InternalFormat.CompressedRgbaBptcUnorm; break;
                case VTexFormat.RGBA8888: format = InternalFormat.Rgba8; break;
                case VTexFormat.RGBA16161616F: format = InternalFormat.Rgba16f; break;
                case VTexFormat.I8: format = InternalFormat.Intensity8; break;
                default:
                    Console.Error.WriteLine($"Don't support {tex.Format} but don't want to crash either. Using error texture!");
                    return GetErrorTexture();
            }

            for (var i = tex.NumMipLevels - 1; i >= 0; i--)
            {
                var width = tex.Width >> i;
                var height = tex.Height >> i;
                var bytes = tex.GetDecompressedTextureAtMipLevel(i);

                GL.CompressedTexImage2D(TextureTarget.Texture2D, i, format, width, height, 0, bytes.Length, bytes);
            }

            // Dispose texture otherwise we run out of memory
            // TODO: This might conflict when opening multiple files due to shit caching
            textureResource.Dispose();

            if (MaxTextureMaxAnisotropy >= 4)
            {
                GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)ExtTextureFilterAnisotropic.TextureMaxAnisotropyExt, MaxTextureMaxAnisotropy);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            }
            else
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            }

            var clampModeS = tex.Flags.HasFlag(VTexFlags.SUGGEST_CLAMPS)
                ? TextureWrapMode.Clamp
                : TextureWrapMode.Repeat;
            var clampModeT = tex.Flags.HasFlag(VTexFlags.SUGGEST_CLAMPT)
                ? TextureWrapMode.Clamp
                : TextureWrapMode.Repeat;

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)clampModeS);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)clampModeT);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            return id;
        }

        public int GetErrorTexture()
        {
            if (ErrorTextureID == 0)
            {
                var color = new[]
                {
                    0.9f, 0.2f, 0.8f, 1f,
                    0f, 0.9f, 0f, 1f,
                    0.9f, 0.2f, 0.8f, 1f,
                    0f, 0.9f, 0f, 1f,

                    0f, 0.9f, 0f, 1f,
                    0.9f, 0.2f, 0.8f, 1f,
                    0f, 0.9f, 0f, 1f,
                    0.9f, 0.2f, 0.8f, 1f,

                    0.9f, 0.2f, 0.8f, 1f,
                    0f, 0.9f, 0f, 1f,
                    0.9f, 0.2f, 0.8f, 1f,
                    0f, 0.9f, 0f, 1f,

                    0f, 0.9f, 0f, 1f,
                    0.9f, 0.2f, 0.8f, 1f,
                    0f, 0.9f, 0f, 1f,
                    0.9f, 0.2f, 0.8f, 1f,
                };

                ErrorTextureID = GenerateColorTexture(4, 4, color);
            }

            return ErrorTextureID;
        }

        public static int CreateSolidTexture(float r, float g, float b)
            => GenerateColorTexture(1, 1, new[] { r, g, b, 1f });

        private static int GenerateColorTexture(int width, int height, float[] color)
        {
            var texture = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, width, height, 0, PixelFormat.Rgba, PixelType.Float, color);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            return texture;
        }
    }
}
