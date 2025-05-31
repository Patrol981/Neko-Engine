using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Utils;
using Dwarf.Vulkan;
using Dwarf.Rendering;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Neko.Rendering.PostProcessing;

public class PostProcessingSystem : SystemBase {
  // public static float DepthMax = 0.995f;
  // public static float DepthMin = 0.990f;
  // public static float EdgeLow = 100f;
  // public static float EdgeHigh = 65f;
  // public static float Contrast = 0.5f; // 2.0f / 0.56f
  // public static float Stipple = 64f; // 0.39f / 0.706f
  // public static Vector3 Luminance = new(0.299f, 0.587f, 0.114f);

  public static PostProcessInfo PostProcessInfo = new() {
    Float_1_1 = 1.4f,
    Float_1_2 = 65,
    Float_1_3 = 0.5f,
    Float_1_4 = 128,

    Float_2_1 = 0.299f,
    Float_2_2 = 0.587f,
    Float_2_3 = 0.114f,
    Float_2_4 = 1,

    Float_3_1 = 0.9f,
    Float_3_4 = 1.0f,

    Float_4_1 = 0.3f,
    Float_4_2 = 0.7f,
    Float_4_3 = 0.5f,
    Float_4_4 = 0.3f,

    Float_5_1 = 0.2f,
    Float_5_2 = 0.0f,
    Float_5_3 = 0.4f,
    Float_5_4 = 0.4f,

    Float_6_1 = 0.5f,
    Float_6_2 = 0.0f,
    Float_6_3 = 0.0f,
    Float_6_4 = 0.2f,

    Float_7_1 = 0.1f,
    Float_7_2 = 0.0f,
  };

  // private readonly unsafe PostProcessInfo* _postProcessInfoPushConstant =
  //   (PostProcessInfo*)Marshal.AllocHGlobal(Unsafe.SizeOf<PostProcessInfo>());
  private readonly TextureManager _textureManager;

  // private ITexture _inputTexture1 = null!;
  // private ITexture _inputTexture2 = null!;
  // private ITexture _inputTexture3 = null!;

  // private const string HatchOneTextureName = "./Resources/zaarg.png";
  // // "./Resources/twilight-5-1x.png";
  // // "./Resources/lv-corinthian-slate-801-1x.png";
  // // "./Resources/zaarg.png";
  // private const string HatchTwoTextureName = "./Resources/slso8-1x.png";
  // private const string HatchThreeTextureName = "./Resources/justparchment8-1x.png";

  private readonly ITexture[] _inputTextures = [];

  public PostProcessingSystem(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    SystemConfiguration systemConfiguration,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    IPipelineConfigInfo configInfo = null!
  ) : base(allocator, device, renderer, configInfo) {
    _textureManager = Application.Instance.TextureManager;

    _setLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.AllGraphics)
      .AddBinding(1, DescriptorType.CombinedImageSampler, ShaderStageFlags.AllGraphics)
      // .AddBinding(2, VkDescriptorType.SampledImage, VkShaderStageFlags.AllGraphics)
      // .AddBinding(3, VkDescriptorType.Sampler, VkShaderStageFlags.AllGraphics)
      .Build();

    _textureSetLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.SampledImage, ShaderStageFlags.AllGraphics)
      .AddBinding(1, DescriptorType.Sampler, ShaderStageFlags.AllGraphics)
      .Build();

    IDescriptorSetLayout[] layouts = [
      // renderer.Swapchain.InputAttachmentLayout.GetDescriptorSetLayout(),
      // externalLayouts["Global"].GetDescriptorSetLayout()
      _setLayout,
      externalLayouts["Global"],
      _textureSetLayout,
      _textureSetLayout,
      _textureSetLayout
    ];

    var postProcessConfig = GetConfigurationBasedOnFlag(systemConfiguration.PostProcessingFlag);

    AddPipelineData<PostProcessInfo>(new() {
      VertexName = postProcessConfig.VertexName,
      FragmentName = postProcessConfig.FragmentName,
      PipelineProvider = new PostProcessingPipelineProvider(),
      DescriptorSetLayouts = layouts
    });

    if (systemConfiguration.PostProcessInputTextures != null) {
      _inputTextures = new ITexture[systemConfiguration.PostProcessInputTextures.Length];
    }

    Setup(systemConfiguration);
  }

  public void Setup(SystemConfiguration systemConfiguration) {
    _device.WaitQueue();

    _descriptorPool = new VulkanDescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets(4)
      .AddPoolSize(DescriptorType.SampledImage, 10)
      .AddPoolSize(DescriptorType.Sampler, 10)
      .AddPoolSize(DescriptorType.CombinedImageSampler, 20)
      .SetPoolFlags(DescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    var texLen = systemConfiguration.PostProcessInputTextures?.Length;
    if (!texLen.HasValue) return;

    for (int i = 0; i < texLen; i++) {
      if (_inputTextures[i] != null) continue;
      _textureManager.AddTextureGlobal(systemConfiguration.PostProcessInputTextures![i]).Wait();
      var id = _textureManager.GetTextureIdGlobal(systemConfiguration.PostProcessInputTextures[i]);
      _inputTextures[i] = _textureManager.GetTextureGlobal(id);
      _inputTextures[i].BuildDescriptor(_textureSetLayout, _descriptorPool);
    }
  }

  private void UpdateDescriptors(int currentFrame) {
    // _renderer.Swapchain.UpdateDescriptors(currentFrame);
    // _renderer.Swapchain.UpdatePostProcessDescriptors(currentFrame);
    // _renderer.UpdateDescriptors();
  }

  public void Render(FrameInfo frameInfo) {
    // UpdateDescriptors(_renderer.FrameIndex);
    BindPipeline(frameInfo.CommandBuffer);

    unsafe {
      var postProcessInfoPushConstant = new PostProcessInfo();
      postProcessInfoPushConstant.Float_1_1 = PostProcessInfo.Float_1_1;
      postProcessInfoPushConstant.Float_1_2 = PostProcessInfo.Float_1_2;
      postProcessInfoPushConstant.Float_1_3 = PostProcessInfo.Float_1_3;
      postProcessInfoPushConstant.Float_1_4 = PostProcessInfo.Float_1_4;

      postProcessInfoPushConstant.Float_2_1 = PostProcessInfo.Float_2_1;
      postProcessInfoPushConstant.Float_2_2 = PostProcessInfo.Float_2_2;
      postProcessInfoPushConstant.Float_2_3 = PostProcessInfo.Float_2_3;
      postProcessInfoPushConstant.Float_2_4 = PostProcessInfo.Float_2_4;

      postProcessInfoPushConstant.Float_3_1 = PostProcessInfo.Float_3_1;
      postProcessInfoPushConstant.Float_3_2 = PostProcessInfo.Float_3_2;
      postProcessInfoPushConstant.Float_3_3 = PostProcessInfo.Float_3_3;
      postProcessInfoPushConstant.Float_3_4 = PostProcessInfo.Float_3_4;

      postProcessInfoPushConstant.Float_4_1 = PostProcessInfo.Float_4_1;
      postProcessInfoPushConstant.Float_4_2 = PostProcessInfo.Float_4_2;
      postProcessInfoPushConstant.Float_4_3 = PostProcessInfo.Float_4_3;
      postProcessInfoPushConstant.Float_4_4 = PostProcessInfo.Float_4_4;

      postProcessInfoPushConstant.Float_5_1 = PostProcessInfo.Float_5_1;
      postProcessInfoPushConstant.Float_5_2 = PostProcessInfo.Float_5_2;
      postProcessInfoPushConstant.Float_5_3 = PostProcessInfo.Float_5_3;
      postProcessInfoPushConstant.Float_5_4 = PostProcessInfo.Float_5_4;

      postProcessInfoPushConstant.Float_6_1 = PostProcessInfo.Float_6_1;
      postProcessInfoPushConstant.Float_6_2 = PostProcessInfo.Float_6_2;
      postProcessInfoPushConstant.Float_6_3 = PostProcessInfo.Float_6_3;
      postProcessInfoPushConstant.Float_6_4 = PostProcessInfo.Float_6_4;

      postProcessInfoPushConstant.Float_7_1 = PostProcessInfo.Float_7_1;
      postProcessInfoPushConstant.Float_7_2 = PostProcessInfo.Float_7_2;
      postProcessInfoPushConstant.Float_7_3 = PostProcessInfo.Float_7_3;
      postProcessInfoPushConstant.Float_7_4 = PostProcessInfo.Float_7_4;

      postProcessInfoPushConstant.Float_8_1 = PostProcessInfo.Float_8_1;
      postProcessInfoPushConstant.Float_8_2 = PostProcessInfo.Float_8_2;
      postProcessInfoPushConstant.Float_8_3 = PostProcessInfo.Float_8_3;
      postProcessInfoPushConstant.Float_8_4 = PostProcessInfo.Float_8_4;

      _device.DeviceApi.vkCmdPushConstants(
        frameInfo.CommandBuffer,
        PipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<PostProcessInfo>(),
        &postProcessInfoPushConstant
      );
    }

    _device.DeviceApi.vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      PipelineLayout,
      0,
      _renderer.PostProcessDecriptor
    );

    _device.DeviceApi.vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      PipelineLayout,
      1,
      frameInfo.GlobalDescriptorSet
    );

    for (uint i = 2, j = 0; i <= _inputTextures.Length + 1; i++, j++) {
      _device.DeviceApi.vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        PipelineLayout,
        i,
        _inputTextures[j].TextureDescriptor
      );
    }

    _device.DeviceApi.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
  }

  public static PostProcessConfiguration GetConfigurationBasedOnFlag(PostProcessingConfigurationFlag flag) {
    return new PostProcessConfiguration {
      FlagIdentifier = flag,
      VertexName = CorrespondingVertex(flag),
      FragmentName = CorrespondingFragment(flag)
    };
  }

  public static ReadOnlySpan<PostProcessConfiguration> GetConfigurations() {
    var flags = Enum.GetValues<PostProcessingConfigurationFlag>().ToArray();
    var configurations = new PostProcessConfiguration[flags.Length];
    for (uint i = 0; i < configurations.Length; i++) {
      configurations[i] = new PostProcessConfiguration {
        FlagIdentifier = flags[i],
        VertexName = CorrespondingVertex(flags[i]),
        FragmentName = CorrespondingFragment(flags[i])
      };
    }
    return configurations;
  }

  private static string CorrespondingFragment(PostProcessingConfigurationFlag flag) {
    return flag switch {
      PostProcessingConfigurationFlag.None => "post_process_none_fragment",
      PostProcessingConfigurationFlag.Anime => "post_process_toon_shader_fragment",
      PostProcessingConfigurationFlag.Sketch => throw new NotImplementedException(),
      PostProcessingConfigurationFlag.PaletteFilter => "post_process_jpaint0_fragment",
      PostProcessingConfigurationFlag.Hatch => throw new NotImplementedException(),
      PostProcessingConfigurationFlag.Custom => "",
      _ => throw new ArgumentOutOfRangeException(nameof(flag)),
    };
  }

  private static string CorrespondingVertex(PostProcessingConfigurationFlag flag) {
    return flag switch {
      PostProcessingConfigurationFlag.None => "post_process_index_vertex",
      PostProcessingConfigurationFlag.Anime => "post_process_index_vertex",
      PostProcessingConfigurationFlag.Sketch => "post_process_index_vertex",
      PostProcessingConfigurationFlag.PaletteFilter => "post_process_index_vertex",
      PostProcessingConfigurationFlag.Hatch => "post_process_index_vertex",
      PostProcessingConfigurationFlag.Custom => "",
      _ => throw new ArgumentOutOfRangeException(nameof(flag)),
    };
  }

  public unsafe override void Dispose() {
    // MemoryUtils.FreeIntPtr<PostProcessInfo>((nint)_postProcessInfoPushConstant);
    base.Dispose();
  }
}