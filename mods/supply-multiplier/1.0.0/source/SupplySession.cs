using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
namespace BF4SupplyMod;
public sealed class SupplyJournalEntry {public SupplyKind Kind{get;set;}public long Address{get;set;}public byte[] Original{get;set;}=Array.Empty<byte>();public Dictionary<int,float[]> Accepted{get;set;}=new();}
public sealed class SupplyJournal {public int Pid{get;set;}public string StartTicks{get;set;}="";public long Image{get;set;}public List<SupplyJournalEntry> Entries{get;set;}=new();}
public sealed class SupplySession:IDisposable {
 readonly SupplyMemory memory;readonly string journalPath,ticks;readonly Dictionary<long,SupplyAsset> assets=new();SupplySettings? previous;bool dirty=true;
 public int HealingFields{get;private set;}public int AmmoFields{get;private set;}public int AssetCount=>assets.Count;public long ScannedBytes{get;private set;}
 public SupplySession(SupplyMemory memory,string root,string ticks){this.memory=memory;this.ticks=ticks;journalPath=Path.Combine(root,"baseline.json");memory.VerifyLayout();Recover();}
 void Recover(){if(!File.Exists(journalPath))return;var journal=JsonSerializer.Deserialize<SupplyJournal>(ModJson.ReadShared(journalPath),ModJson.Options);if(journal==null||journal.Pid!=memory.Pid||journal.StartTicks!=ticks||journal.Image!=memory.Image)return;foreach(var entry in journal.Entries){if(entry.Address<0x10000||!Enum.IsDefined(entry.Kind)||!SupplyAsset.Valid(entry.Kind,entry.Original)||BitConverter.ToInt64(entry.Original)!=memory.Image+SupplyAsset.Vtable(entry.Kind))continue;new SupplyAsset(entry.Kind,entry.Address,entry.Original).Recover(memory,entry.Accepted);}}
 public void Scan(Func<bool> stopping,Action<long>? progress=null){
  foreach(var asset in memory.Scan(stopping,n=>{ScannedBytes=n;progress?.Invoke(n);}))if(asset.CanSupply){
   if(assets.TryGetValue(asset.Address,out var existing)){if(existing.Same(asset.Original))continue;if(existing.HasOwnedValues(asset.Original))throw new SupplyConflictException("补给资源已由其他模块调整，重启游戏服后可重新加载。");}
   assets[asset.Address]=asset;dirty=true;
  }
  foreach(var item in assets.ToArray()){var bytes=memory.Read(item.Key,item.Value.Original.Length);if(bytes!=null&&!item.Value.Same(bytes)){if(item.Value.HasOwnedValues(bytes))throw new SupplyConflictException("补给资源已由其他模块调整，重启游戏服后可重新加载。");assets.Remove(item.Key);dirty=true;}else if(bytes==null&&memory.KnownUnmapped(item.Key)){assets.Remove(item.Key);dirty=true;}}
 }
 public void Apply(SupplySettings settings){
  if(previous!=settings||dirty){var journal=new SupplyJournal{Pid=memory.Pid,StartTicks=ticks,Image=memory.Image,Entries=assets.Values.Select(a=>new SupplyJournalEntry{Kind=a.Kind,Address=a.Address,Original=a.Original,Accepted=a.Accepted(a.Kind==SupplyKind.AmmoUnit?settings.Ammo:settings.Healing)}).ToList()};ModJson.Atomic(journalPath,JsonSerializer.Serialize(journal,ModJson.Options));dirty=false;previous=settings;}
  HealingFields=0;AmmoFields=0;foreach(var asset in assets.Values){int count=asset.Apply(memory,asset.Kind==SupplyKind.AmmoUnit?settings.Ammo:settings.Healing);if(asset.Kind==SupplyKind.AmmoUnit)AmmoFields+=count;else HealingFields+=count;}
 }
 public void Dispose(){int expected=assets.Values.Sum(a=>a.Written.Count),restored=0;foreach(var asset in assets.Values)restored+=asset.Restore(memory);if(restored==expected&&File.Exists(journalPath))File.Delete(journalPath);}
}
