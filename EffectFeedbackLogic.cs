using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
namespace BF4Launcher;
public static class EffectFeedbackLogic {
 static readonly Dictionary<string,JsonNode?> Defaults=new(){["speedPercent"]=JsonValue.Create(100),["noRecoil"]=JsonValue.Create(false),["noSpread"]=JsonValue.Create(false),["noBulletDrop"]=JsonValue.Create(false),["fastBullets"]=JsonValue.Create(false),["flight"]=JsonValue.Create(false),["thirdPerson"]=JsonValue.Create(false),["cameraHotkey"]=JsonValue.Create(false),["cameraDistance"]=JsonValue.Create(3),["cameraHeight"]=JsonValue.Create(0.6)};
 public static string Describe(ModItem mod,JsonNode module){
  var state=module["state"]?.ToString();if(state=="error")return module["message"]?.ToString()??"玩家效果未加载";
  var keys=mod.Manifest.Fields.Select(f=>f.Id).Where(Defaults.ContainsKey).ToArray();
  if(keys.Length==0)return "已启用 · 没有可应用的参数";
  var final=module["feedback"]?["config"];var live=module["feedback"]?["status"];
  foreach(var key in keys){var wanted=mod.Config.GetValueOrDefault(key)??Defaults[key];if(final?[key]!=null&&!JsonNode.DeepEquals(wanted,final[key]))return "参数被后方模组覆盖";}
  var changed=keys.Where(k=>!JsonNode.DeepEquals(mod.Config.GetValueOrDefault(k)??Defaults[k],Defaults[k])).ToArray();
  if(changed.Length==0)return "已启用 · 当前参数为默认值";
  if(state!="applied")return module["message"]?.ToString()??"等待角色部署";
  foreach(var key in changed){var value=mod.Config.GetValueOrDefault(key);if(value is JsonValue j&&j.TryGetValue<bool>(out bool flag)&&flag){if(live?[key]?.GetValue<bool>()!=true)return "等待适用的武器或角色状态";}}
  return "已应用到本机玩家";
 }
}
