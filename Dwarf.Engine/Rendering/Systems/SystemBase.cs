
using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Rendering;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public class PipelineData {
  public ulong PipelineLayout;
  public IPipeline Pipeline = null!;

  public unsafe void Dispose(IDevice device) {
    Pipeline.Dispose();

    if (PipelineLayout != 0) {
      vkDestroyPipelineLayout(device.LogicalDevice, PipelineLayout);
    }
  }
}

public class PipelineInputData<T> where T : struct {
  public T PushConstantType { get; } = default;
  public string PipelineName { get; set; } = SystemBase.DefaultPipelineName;
  public IDescriptorSetLayout[] DescriptorSetLayouts = [];
  public string VertexName = string.Empty;
  public string FragmentName = string.Empty;
  public string? GeometryName = null;
  public IPipelineProvider PipelineProvider { get; set; } = null!;
  public ulong RenderPass { get; set; } = 0;
}

public class PipelineInputData {
  public string PipelineName { get; set; } = SystemBase.DefaultPipelineName;
  public IDescriptorSetLayout[] DescriptorSetLayouts = [];
  public string VertexName = string.Empty;
  public string FragmentName = string.Empty;
  public string? GeometryName = null;
  public IPipelineProvider PipelineProvider { get; set; } = null!;
  public ulong RenderPass { get; set; } = 0;
}


public abstract class SystemBase {
  public const string DefaultPipelineName = "main";

  protected readonly IDevice _device = null!;
  protected readonly nint _allocator = IntPtr.Zero;
  protected readonly IRenderer _renderer = null!;
  protected readonly TextureManager _textureManager = null!;
  protected IPipelineConfigInfo _pipelineConfigInfo;
  protected Dictionary<string, PipelineData> _pipelines = [];

  protected IDescriptorPool _descriptorPool = null!;
  protected IDescriptorPool _texturePool = null!;
  protected IDescriptorSetLayout _setLayout = null!;
  protected IDescriptorSetLayout _textureSetLayout = null!;
  protected IDescriptorSet[] _descriptorSets = [];

  protected int _texturesCount = 0;

  public SystemBase(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    IPipelineConfigInfo configInfo = null!
  ) {
    _allocator = allocator;
    _device = device;
    _renderer = renderer;
    _textureManager = textureManager;

    _pipelineConfigInfo = configInfo ?? null!;
  }

  #region Pipeline

  protected unsafe void CreatePipelineLayout<T>(
    IDescriptorSetLayout[] layouts,
    out ulong pipelineLayout
  ) {
    switch (_device.RenderAPI) {
      case RenderAPI.Vulkan:
        VkCreatePipelineLayoutBase(layouts, out var pipelineInfo);
        var push = VkCreatePushConstants<T>();
        pipelineInfo.pushConstantRangeCount = 1;
        pipelineInfo.pPushConstantRanges = &push;
        VkFinalizePipelineLayout(&pipelineInfo, out var vkLayout);
        pipelineLayout = vkLayout.Handle;
        break;
      default:
        throw new NotImplementedException();
    }
  }

  protected unsafe void CreatePipelineLayout(
    IDescriptorSetLayout[] layouts,
    out ulong pipelineLayout
  ) {
    switch (_device.RenderAPI) {
      case RenderAPI.Vulkan:
        VkCreatePipelineLayoutBase(layouts, out var pipelineInfo);
        VkFinalizePipelineLayout(&pipelineInfo, out var vkLayout);
        pipelineLayout = vkLayout.Handle;
        break;
      default:
        throw new NotImplementedException();
    }
  }

  protected unsafe void VkCreatePipelineLayoutBase(
    IDescriptorSetLayout[] layouts,
    out VkPipelineLayoutCreateInfo pipelineInfo
  ) {
    pipelineInfo = new() {
      setLayoutCount = (uint)layouts.Length
    };
    var vkLayouts = layouts.Select(x => x.GetDescriptorSetLayoutPointer()).ToArray();
    fixed (ulong* ptr = vkLayouts) {
      pipelineInfo.pSetLayouts = (VkDescriptorSetLayout*)ptr;
    }
    // fixed (VkDescriptorSetLayout* ptr = ) {
    //   pipelineInfo.pSetLayouts = ptr;
    // }
  }

  protected unsafe void VkFinalizePipelineLayout(
    VkPipelineLayoutCreateInfo* pipelineInfo,
    out VkPipelineLayout pipelineLayout
  ) {
    vkCreatePipelineLayout(
      _device.LogicalDevice,
      pipelineInfo,
      null,
      out pipelineLayout
    ).CheckResult();
  }

  protected unsafe VkPushConstantRange VkCreatePushConstants<T>() {
    VkPushConstantRange pushConstantRange = new() {
      stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
      offset = 0,
      size = (uint)Unsafe.SizeOf<T>()
    };

    return pushConstantRange;
  }

  protected unsafe void CreatePipeline(
    ulong renderPass,
    string vertexName,
    string fragmentName,
    string? geometryName,
    IPipelineProvider pipelineProvider,
    ulong pipelineLayout,
    out IPipeline pipeline
  ) {
    // _pipelineConfigInfo ??= new VkPipelineConfigInfo();
    _pipelineConfigInfo = PipelineFactory.GetOrCreatePipelineConfigInfo(Application.Instance, _pipelineConfigInfo);

    switch (_device.RenderAPI) {
      case RenderAPI.Vulkan:
        CreateVkPipeline(vertexName, fragmentName, geometryName, pipelineLayout, pipelineProvider, out pipeline);
        break;
      case RenderAPI.Metal:
        throw new NotImplementedException();
      default:
        throw new NotImplementedException();
    }

    // pipeline = PipelineFactory.CreatePipeline(
    //   Application.Instance,
    //   vertexName,
    //   fragmentName,
    //   ref _pipelineConfigInfo,
    //   ref pipelineProvider,
    //   pipelineLayout
    // );
  }

  private unsafe void CreateVkPipeline(
    string vertexName,
    string fragmentName,
    string? geometryName,
    ulong pipelineLayout,
    IPipelineProvider pipelineProvider,
    out IPipeline pipeline
  ) {
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    var colorFormat = _renderer.Swapchain.ColorFormat;
    var depthFormat = _renderer.DepthFormat;
    pipelineConfig.RenderPass = VkRenderPass.Null;
    pipelineConfig.PipelineLayout = pipelineLayout;
    pipeline = new VulkanPipeline(
      _device,
      pipelineConfig,
      (VkPipelineProvider)pipelineProvider,
      depthFormat.AsVkFormat(),
      colorFormat.AsVkFormat(),
            vertexName,
      fragmentName,
      geometryName ?? default
    );
  }

  protected void AddPipelineData<T>(PipelineInputData<T> pipelineInput) where T : struct {
    _pipelines.TryAdd(
      pipelineInput.PipelineName,
      new()
    );

    CreatePipelineLayout<T>(
      pipelineInput.DescriptorSetLayouts,
      out _pipelines[pipelineInput.PipelineName].PipelineLayout
    );

    CreatePipeline(
      pipelineInput.RenderPass,
      pipelineInput.VertexName,
      pipelineInput.FragmentName,
      pipelineInput.GeometryName,
      pipelineInput.PipelineProvider,
      _pipelines[pipelineInput.PipelineName].PipelineLayout,
      out _pipelines[pipelineInput.PipelineName].Pipeline
    );
  }

  protected void AddPipelineData(PipelineInputData pipelineInput) {
    _pipelines.TryAdd(
      pipelineInput.PipelineName,
      new()
    );

    CreatePipelineLayout(
      pipelineInput.DescriptorSetLayouts,
      out _pipelines[pipelineInput.PipelineName].PipelineLayout
    );

    CreatePipeline(
      pipelineInput.RenderPass,
      pipelineInput.VertexName,
      pipelineInput.FragmentName,
      pipelineInput.GeometryName,
      pipelineInput.PipelineProvider,
      _pipelines[pipelineInput.PipelineName].PipelineLayout,
      out _pipelines[pipelineInput.PipelineName].Pipeline
    );

  }

  protected void BindPipeline(VkCommandBuffer commandBuffer, string pipelineName = DefaultPipelineName) {
    _pipelines[pipelineName].Pipeline?.Bind(commandBuffer);
  }

  #endregion

  public virtual unsafe void Dispose() {
    _device.WaitQueue();
    _setLayout?.Dispose();
    _textureSetLayout?.Dispose();
    _descriptorPool?.Dispose();
    _texturePool?.Dispose();
    foreach (var p in _pipelines) {
      p.Value.Dispose(_device);
    }
    _pipelines.Clear();
  }

  public VkPipelineLayout PipelineLayout => _pipelines.FirstOrDefault().Value.PipelineLayout;
}
