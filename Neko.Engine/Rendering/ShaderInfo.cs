namespace Neko.Rendering;

public struct ShaderInfo {
  public string Name { get; set; }
  public Guid ShaderTextureId { get; set; } = Guid.Empty;

  public ShaderInfo() {
    Name = CommonConstants.SHADER_INFO_NAME_UNSET;
  }

  public ShaderInfo(string name) {
    Name = name;
  }
}