using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
namespace BF4SupplyMod;
public sealed record SupplySettings(bool Enabled,float Healing=1,float Ammo=1);
public static class SupplyPolicy {
 public static SupplySettings Parse(JsonNode config){
  float Value(string key){var value=config[key]?.ToString()??"1";if(!float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float n)||!float.IsFinite(n)||n<1||n>10)throw new Exception("补给倍率的范围为 1～10 倍。");return n;}
  return new(true,Value("healingMultiplier"),Value("ammoMultiplier"));
 }
}
public sealed class SupplyConflictException:Exception {public SupplyConflictException(string message):base(message){}}
public enum SupplyKind { HealingUnit,AmmoUnit,MedicSphere }
public interface ISupplyMemory {byte[]? Read(long address,int size);bool WriteFloat(long address,float value);}
public sealed class SupplyAsset {
 public SupplyKind Kind{get;}public long Address{get;}public byte[] Original{get;}public Dictionary<int,float> Written{get;}=new();
 // The timed-pack field is HP/sec; scale speed without changing its total amount or capacity.
 public bool TimedHealing=>Kind==SupplyKind.HealingUnit&&BitConverter.ToSingle(Original,0x30)>0&&BitConverter.ToSingle(Original,0x20)>0;
 public bool CanSupply=>Kind==SupplyKind.MedicSphere?BitConverter.ToSingle(Original,0x194)>0:BitConverter.ToSingle(Original,0x1c)>0||BitConverter.ToSingle(Original,0x20)>0;
 public int[] Fields=>Kind==SupplyKind.MedicSphere?new[]{0x194}:TimedHealing?new[]{0x30}:new[]{0x1c,0x20,0x24,0x28};
 public float Desired(int field,float multiplier)=>BitConverter.ToSingle(Original,field)*multiplier;
 public SupplyAsset(SupplyKind kind,long address,byte[] data){Kind=kind;Address=address;Original=(byte[])data.Clone();}
 public static int Size(SupplyKind kind)=>kind==SupplyKind.MedicSphere?0x1a0:0x38;
 public static long Vtable(SupplyKind kind)=>kind switch{SupplyKind.HealingUnit=>0x2122448,SupplyKind.AmmoUnit=>0x2122468,_=>0x2122150};
 public static bool Valid(SupplyKind kind,byte[] bytes){
  if(bytes.Length!=Size(kind))return false;var flags=BitConverter.ToUInt16(bytes,10);
  if(kind==SupplyKind.MedicSphere){if((flags&0xf000)!=0xa000&&(flags&0xf000)!=0xb000)return false;}
  else if((flags&0xff00)!=0xa000)return false;
  var fields=kind==SupplyKind.MedicSphere?new[]{0x190,0x194}:new[]{0x10,0x14,0x18,0x1c,0x20,0x24,0x28};
  if(fields.Any(i=>!float.IsFinite(BitConverter.ToSingle(bytes,i))||BitConverter.ToSingle(bytes,i)<0||BitConverter.ToSingle(bytes,i)>10000))return false;
  if(kind!=SupplyKind.MedicSphere&&(bytes[0x2c]>1||bytes[0x2d]>1))return false;
  if(kind==SupplyKind.HealingUnit&&(!float.IsFinite(BitConverter.ToSingle(bytes,0x30))||BitConverter.ToSingle(bytes,0x30)<0||BitConverter.ToSingle(bytes,0x30)>10000))return false;
  return fields.Take(kind==SupplyKind.MedicSphere?1:2).All(i=>BitConverter.ToSingle(bytes,i)<=100);
 }
 public bool HasOwnedValues(byte[]? current)=>current!=null&&current.Length==Original.Length&&BitConverter.ToInt64(current)==BitConverter.ToInt64(Original)&&Written.Any(p=>BitConverter.SingleToInt32Bits(BitConverter.ToSingle(current,p.Key))==BitConverter.SingleToInt32Bits(p.Value));
 public bool Same(byte[]? current){if(current==null||current.Length!=Original.Length)return false;for(int i=0;i<Original.Length;i++){if(i is 8 or 9||Fields.Any(f=>i>=f&&i<f+4))continue;if(current[i]!=Original[i])return false;}return true;}
 static bool Equal(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
 public int Apply(ISupplyMemory memory,float multiplier){
  if(!Same(memory.Read(Address,Original.Length)))return 0;
  if(multiplier==1){Restore(memory);if(Written.Count>0)throw new Exception("原始补给参数正在恢复。");return 0;}
  int count=0;
  foreach(int field in Fields){float original=BitConverter.ToSingle(Original,field);if(original==0)continue;float desired=Desired(field,multiplier);var bytes=memory.Read(Address+field,4);if(bytes==null)throw new Exception("补给资源正在切换，请稍候。");float current=BitConverter.ToSingle(bytes);float expected=Written.GetValueOrDefault(field,original);
   if(!Equal(current,expected))throw new SupplyConflictException("补给参数已由其他模块修改，重启游戏服后可重新加载。");
   if(!Equal(current,desired)&&!memory.WriteFloat(Address+field,desired))throw new Exception("补给倍率写入未完成。");Written[field]=desired;count++;
  }return count;
 }
 public int Restore(ISupplyMemory memory){
  int restored=0;var currentAsset=memory.Read(Address,Original.Length);if(currentAsset==null)return 0;if(!Same(currentAsset)){Written.Clear();return 0;}
  foreach(var pair in Written.ToArray()){var bytes=memory.Read(Address+pair.Key,4);if(bytes==null)continue;float current=BitConverter.ToSingle(bytes);if(!Equal(current,pair.Value)){Written.Remove(pair.Key);continue;}if(memory.WriteFloat(Address+pair.Key,BitConverter.ToSingle(Original,pair.Key))){restored++;Written.Remove(pair.Key);}}
  return restored;
 }
 public Dictionary<int,float[]> Accepted(float multiplier)=>Fields.Where(i=>BitConverter.ToSingle(Original,i)>0).ToDictionary(i=>i,i=>new[]{BitConverter.ToSingle(Original,i),Written.GetValueOrDefault(i,BitConverter.ToSingle(Original,i)),Desired(i,multiplier)}.Distinct().ToArray());
 public void Recover(ISupplyMemory memory,Dictionary<int,float[]> accepted){
  var current=memory.Read(Address,Original.Length);if(current==null)return;
  foreach(int field in Fields)if(accepted.TryGetValue(field,out var candidates)){float n=BitConverter.ToSingle(current,field);if(candidates.Any(c=>Equal(c,n))&&!Equal(n,BitConverter.ToSingle(Original,field)))Written[field]=n;}
  if(!Same(current)){if(HasOwnedValues(current))throw new SupplyConflictException("补给资源已由其他模块调整，重启游戏服后可重新加载。");Written.Clear();return;}
  Restore(memory);if(Written.Count>0)throw new Exception("上一次补给设置尚未恢复，请重新启动游戏服。");
 }
}
