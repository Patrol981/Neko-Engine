using System.Runtime.InteropServices;
using Neko;
using Neko.EntityComponentSystem;
using Neko.Native;

public static partial class NekoNativeInterop {

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void NekoScriptEventNative();

  public static ActionResult Neko_Entity_AddTransform(
    string entityId,
    float pX, float pY, float pZ,
    float rX, float rY, float rZ,
    float sX, float sY, float sZ
  ) {
    var target = Application.Instance.GetEntity(Guid.Parse(entityId));
    if (target is null) {
      return ActionResult.Error;
    }
    target.AddTransform([pX, pY, pZ], [rX, rY, rZ], [sX, sY, sZ]);
    return ActionResult.Success;
  }

  public static ActionResult Neko_Entity_Script_Add(string entityId) {
    var target = Application.Instance.GetEntity(Guid.Parse(entityId));
    if (target is null) {
      return ActionResult.Error;
    }

    target.AddScript(new NekoScriptInterpreted());
    return ActionResult.Success;
  }

  // [UnmanagedCallersOnly(EntryPoint = "Neko_Entity_Script_SetAwake")]
  // public unsafe static ActionResult Neko_Entity_Script_SetAwake(char* entityId, NekoScriptEvent @event) {
  //   var target = Application.Instance.GetEntity(Guid.Parse(entityId));
  //   if (target is null) {
  //     return ActionResult.Error;
  //   }

  //   var script = target.GetScript<NekoScriptInterpreted>();
  //   if (script is null) {
  //     return ActionResult.Error;
  //   }

  //   script.OnAwake += @event;
  //   return ActionResult.Success;
  // }
}