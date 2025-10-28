using StbImageSharp;

namespace Neko.Utils;

public struct PackedTextureHeader {
  public int Width;
  public int Height;
  public int BytesPerPixel;
  public int Size;
}

public struct PackedTexture {
  public PackedTextureHeader[] Headers;
  public List<byte> ByteArray;
  public int Size;
}

public class ImageUtils {
  public static PackedTexture PackImage(ReadOnlySpan<ImageResult> images) {
    var packedTexture = new PackedTexture() {
      Headers = new PackedTextureHeader[images.Length],
      ByteArray = new()
    };

    for (int i = 0; i < images.Length; i++) {
      packedTexture.Headers[i] = new PackedTextureHeader() {
        Width = images[i].Width,
        Height = images[i].Height,
        Size = images[i].Width * images[i].Height * 4,
        BytesPerPixel = 4
      };

      packedTexture.ByteArray.AddRange(images[i].Data);
      packedTexture.Size += packedTexture.Headers[i].Size;
    }

    return packedTexture;
  }

  public static ReadOnlySpan<ImageResult> LoadTextures(string[] paths, int flip = 1) {
    var textures = new List<ImageResult>();

    StbImage.stbi_set_flip_vertically_on_load(flip);

    for (int i = 0; i < paths.Length; i++) {
      using var stream = File.OpenRead(paths[i]);
      var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
      textures.Add(img);
      stream.Dispose();
    }

    return textures.ToArray();
  }
}
