using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Models;
using OpenTK.Graphics.OpenGL4;

namespace GTA1MapEditor.Rendering;

/// <summary>
/// Lazy GL-texture cache keyed by global sprite index. Decoded once via
/// <see cref="SpriteRenderer.DecodeSprite"/> the first time a sprite is
/// requested, then reused. Sentinels are stored for invalid indices so we
/// don't repeatedly attempt to decode them.
/// </summary>
public sealed class SpriteCache : IDisposable
{
    public readonly record struct Entry(int Texture, int Width, int Height);

    private readonly G24StyleData _style;
    private readonly Dictionary<int, Entry> _cache = new();

    public SpriteCache(G24StyleData style) { _style = style; }

    /// <summary>Texture + size for <paramref name="spriteIndex"/>, or null if the sprite couldn't be decoded.</summary>
    public Entry? Get(int spriteIndex)
    {
        if (_cache.TryGetValue(spriteIndex, out var existing))
            return existing.Texture == 0 ? null : existing;

        var rgba = SpriteRenderer.DecodeSprite(_style, spriteIndex, out int w, out int h);
        if (rgba is null || w == 0 || h == 0)
        {
            _cache[spriteIndex] = new Entry(0, 0, 0); // negative cache
            return null;
        }

        int tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, tex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, w, h, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
            (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
            (int)TextureWrapMode.ClampToEdge);

        var entry = new Entry(tex, w, h);
        _cache[spriteIndex] = entry;
        return entry;
    }

    public void Dispose()
    {
        foreach (var e in _cache.Values)
            if (e.Texture != 0) GL.DeleteTexture(e.Texture);
        _cache.Clear();
    }
}
