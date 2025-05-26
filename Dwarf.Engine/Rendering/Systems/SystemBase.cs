
using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Rendering;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf;

public class PipelineData {
  public VkPipelineLayout PipelineLayout;
  public IPipeline Pipeline = null!;

  public unsafe void Dispose(IDevice device) {
    Pipeline.Dispose();

    if (PipelineLayout.IsNotNull) {
      vkDestroyPipelineLayout(device.LogicalDevice, PipelineLayout);
    }
  }
}

public class PipelineInputData<T> where T : struct {
  public T PushConstantType { get; } = default;
  public string PipelineName { get; set; } = SystemBase.DefaultPipelineName;
  public VkDescriptorSetLayout[] DescriptorSetLayouts = [];
  public string VertexName = string.Empty;
  public string FragmentName = string.Empty;
  public VkPipelineProvider PipelineProvider { get; set; } = null!;
  public VkRenderPass RenderPass { get; set; } = VkRenderPass.Null;
}

public class PipelineInputData {
  public string PipelineName { get; set; } = SystemBase.DefaultPipelineName;
  public VkDescriptorSetLayout[] DescriptorSetLayouts = [];
  public string VertexName = string.Empty;
  public string FragmentName = string.Empty;
  public VkPipelineProvider PipelineProvider { get; set; } = null!;
  public VkRenderPass RenderPass { get; set; } = VkRenderPass.Null;
}


public abstract class SystemBase {
  public const string DefaultPipelineName = "main";

  protected readonly IDevice _device = null!;
  protected readonly nint _allocator = IntPtr.Zero;
  protected readonly IRenderer _renderer = null!;
  // protected VkPipelineConfigInfo _pipelineConfigInfo;
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
    VkPipelineConfigInfo configInfo = null!
  ) {
    _allocator = allocator;
    _device = device;
    _renderer = renderer;

    _pipelineConfigInfo = configInfo ?? null!;
  }

  #region Pipeline

  protected unsafe void CreatePipelineLayout<T>(
    VkDescriptorSetLayout[] layouts,
    out VkPipelineLayout pipelineLayout
  ) {
    CreatePipelineLayoutBase(layouts, out var pipelineInfo);
    var push = CreatePushConstants<T>();
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &push;
    FinalizePipelineLayout(&pipelineInfo, out pipelineLayout);
  }

  protected unsafe void CreatePipelineLayout(
    VkDescriptorSetLayout[] layouts,
    out VkPipelineLayout pipelineLayout
  ) {
    CreatePipelineLayoutBase(layouts, out var pipelineInfo);
    FinalizePipelineLayout(&pipelineInfo, out pipelineLayout);
  }

  protected unsafe void CreatePipelineLayoutBase(
    VkDescriptorSetLayout[] layouts,
    out VkPipelineLayoutCreateInfo pipelineInfo
  ) {
    pipelineInfo = new() {
      setLayoutCount = (uint)layouts.Length
    };
    fixed (VkDescriptorSetLayout* ptr = layouts) {
      pipelineInfo.pSetLayouts = ptr;
    }
  }

  protected unsafe void FinalizePipelineLayout(
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

  protected unsafe VkPushConstantRange CreatePushConstants<T>() {
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
    IPipelineProvider pipelineProvider,
    ulong pipelineLayout,
    out IPipeline pipeline
  ) {
    // _pipelineConfigInfo ??= new VkPipelineConfigInfo();
    _pipelineConfigInfo = PipelineFactory.GetOrCreatePipelineConfigInfo(Application.Instance, _pipelineConfigInfo);

    switch (_device.RenderAPI) {
      case RenderAPI.Vulkan:
        CreateVkPipeline(vertexName, fragmentName, pipelineLayout, pipelineProvider, out pipeline);
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
    ulong pipelineLayout,
    IPipelineProvider pipelineProvider,
    out IPipeline pipeline
  ) {
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    var colorFormat = _renderer.DynamicSwapchain.ColorFormat;
    var depthFormat = _renderer.DepthFormat;
    pipelineConfig.RenderPass = VkRenderPass.Null;
    pipelineConfig.PipelineLayout = pipelineLayout;
    pipeline = new VulkanPipeline(
      _device,
      vertexName,
      fragmentName,
      pipelineConfig,
      (VkPipelineProvider)pipelineProvider,
      depthFormat,
      colorFormat
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
