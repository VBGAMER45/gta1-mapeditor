using OpenTK.Graphics.OpenGL4;

namespace GTA1MapEditor.Rendering;

/// <summary>
/// Shared atlas → GL texture upload. Generates mipmaps and configures
/// the filters so minified rendering (zoomed out) stays clean while
/// magnification stays pixel-perfect.
/// </summary>
public static class AtlasTexture
{
    /// <summary>Upload <paramref name="atlas"/> into <paramref name="texHandle"/> with mipmaps.</summary>
    public static void Upload(int texHandle, TileAtlas atlas)
    {
        GL.BindTexture(TextureTarget.Texture2D, texHandle);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
            atlas.WidthPixels, atlas.HeightPixels, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, atlas.Rgba);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
            (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
            (int)TextureWrapMode.ClampToEdge);
    }
}
