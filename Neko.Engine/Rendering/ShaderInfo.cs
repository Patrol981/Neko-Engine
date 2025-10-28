namespace Neko.Rendering;

public struct ShaderInfo {
  public string Name { get; set; }

  public ShaderInfo() {
    Name = CommonConstants.SHADER_INFO_NAME_UNSET;
  }

  public ShaderInfo(string name) {
    Name = name;
  }
}