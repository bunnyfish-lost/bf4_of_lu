using System;using System.IO;using System.Diagnostics;using System.Threading;using System.Runtime.InteropServices;using System.Security.Cryptography;using System.Web.Script.Serialization;
class FastLoader{
 static string PathExpected; const string HashExpected="0365a62f197d4544b5326b82a682d77af542e973cb3d9cb6ae582cd543fae2d0";
 [DllImport("kernel32",SetLastError=true)]static extern IntPtr OpenProcess(uint a,bool i,int p);
 [DllImport("kernel32")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr r);
 [DllImport("kernel32",SetLastError=true)]static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr r);
 [DllImport("kernel32",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr a,UIntPtr n,uint t,uint p);
 [DllImport("kernel32",SetLastError=true)]static extern bool VirtualProtectEx(IntPtr h,IntPtr a,UIntPtr n,uint p,out uint old);
 [DllImport("kernel32")]static extern bool FlushInstructionCache(IntPtr h,IntPtr a,UIntPtr n);
 static IntPtr handle;static JavaScriptSerializer json=new JavaScriptSerializer();
 static readonly string home=AppDomain.CurrentDomain.BaseDirectory,stateFile=System.IO.Path.Combine(home,"runtime-state.json");
 public class State{public int pid;public long startTicks,baseAddress,allocation,callAddress,gateAddress;public string status,updatedAt,revision;public byte[] callOriginal,callPatched,gateOriginal,gatePatched;}
 static byte[] Read(long a,int n){byte[] b=new byte[n];UIntPtr got;if(!ReadProcessMemory(handle,new IntPtr(a),b,(UIntPtr)n,out got)||got.ToUInt64()!=(ulong)n)throw new Exception("无法读取补丁位置 "+a.ToString("x"));return b;}
 static bool Same(byte[] a,byte[] b){return DeathFastRecovery.Same(a,b);}
 static void Patch(long a,byte[] old,byte[] next){
  if(!Same(Read(a,old.Length),old))throw new Exception("补丁位置已改变 "+a.ToString("x"));
  uint protect,unused;if(!VirtualProtectEx(handle,new IntPtr(a),(UIntPtr)next.Length,0x40,out protect))throw new Exception("无法修改补丁页权限");
  try{UIntPtr got;if(!WriteProcessMemory(handle,new IntPtr(a),next,(UIntPtr)next.Length,out got)||got.ToUInt64()!=(ulong)next.Length)throw new Exception("写入失败");if(!FlushInstructionCache(handle,new IntPtr(a),(UIntPtr)next.Length))throw new Exception("刷新指令失败");}
  finally{if(!VirtualProtectEx(handle,new IntPtr(a),(UIntPtr)next.Length,protect,out unused))throw new Exception("恢复补丁页权限失败");}
  if(!Same(Read(a,next.Length),next))throw new Exception("补丁读回核验失败");
 }
 static void AtomicText(string file,string text){string tmp=file+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var stream=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new StreamWriter(stream,new System.Text.UTF8Encoding(false))){writer.Write(text);writer.Flush();stream.Flush(true);}if(File.Exists(file))File.Replace(tmp,file,null);else File.Move(tmp,file);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
 static void Save(State s){s.updatedAt=DateTime.UtcNow.ToString("o");AtomicText(stateFile,json.Serialize(s));}
 static State Load(){return File.Exists(stateFile)?json.Deserialize<State>(File.ReadAllText(stateFile)):null;}
 static Process Find(int pid){Process found=null;foreach(var p in Process.GetProcessesByName("bf4_no_drm")){if(pid!=0&&p.Id!=pid){p.Dispose();continue;}try{if(String.Equals(p.MainModule.FileName,PathExpected,StringComparison.OrdinalIgnoreCase)){if(found!=null)throw new Exception("检测到多个实验客户端，请只保留一个。");found=p;}else p.Dispose();}catch(System.ComponentModel.Win32Exception){p.Dispose();}}return found;}
 static bool Session(State s,Process p){return s!=null&&s.pid==p.Id&&s.startTicks==p.StartTime.ToUniversalTime().Ticks;}
 static DeathFastRecovery.Binding[] Bindings(State s,long delta){
  bool old=Same(s.callOriginal,new byte[]{0xe8,0x8c,0xfe,0xff,0xff});
  if(!old&&!Same(s.callOriginal,new byte[]{0xe8,0x80,0xfd,0xff,0xff}))throw new Exception("补丁原始指令记录无效，请退出 BF4 恢复。");
  long oldCall=(old?0x1407c55ef:0x1407c56fb)+delta;
  if(s.callAddress==0)s.callAddress=oldCall;if(s.gateAddress==0)s.gateAddress=0x141cf99d8+delta;
  if(s.callAddress!=oldCall||s.gateAddress!=0x141cf99d8+delta||s.allocation<0x10000||!Same(s.gateOriginal,BitConverter.GetBytes(0x1407cc390+delta))||!Same(s.callPatched,DeathFastPatch.Call(oldCall,s.allocation))||!Same(s.gatePatched,BitConverter.GetBytes(s.allocation+512)))throw new Exception("补丁恢复记录不完整，请正常退出 BF4 恢复。");
  return new[]{new DeathFastRecovery.Binding(s.callAddress,s.callOriginal,s.callPatched),new DeathFastRecovery.Binding(s.gateAddress,s.gateOriginal,s.gatePatched)};
 }
 static void Restore(State s,long delta){var bindings=Bindings(s,delta);s.status="restoring";Save(s);try{DeathFastRecovery.Restore(bindings,Read,Patch);s.status="restored";Save(s);}catch{s.status="restore-failed";Save(s);throw;}}
 static int Emit(bool machine,string state,string message,int pid,bool success){if(machine)Console.WriteLine(json.Serialize(new{state=state,message=message,pid=pid,success=success}));else Console.WriteLine(message);return success?0:1;}
 static int Main(string[] args){
  Console.OutputEncoding=new System.Text.UTF8Encoding(false);
  bool machine=Array.IndexOf(args,"--json")>=0,restore=Array.IndexOf(args,"--restore")>=0,status=Array.IndexOf(args,"--status")>=0;int target=0;Mutex mutex=null;bool locked=false;
  try{
   string hostRoot=System.IO.Path.GetFullPath(System.IO.Path.Combine(home,"..","..")); var clientConfig=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(System.IO.Path.Combine(hostRoot,"settings","client-game.json"))); PathExpected=System.IO.Path.GetFullPath(System.IO.Path.Combine((string)clientConfig["directory"],"bf4_no_drm.exe"));
   int at=Array.IndexOf(args,"--pid");if(at>=0&&(at+1>=args.Length||!Int32.TryParse(args[at+1],out target)||target<=0))throw new Exception("游戏进程编号无效");
   using(Process p=WaitForGame(target,!restore&&!status&&target==0)){
    if(p==null){if(status||restore)return Emit(machine,"not-running","BF4 未运行；退出游戏后内存补丁已自动恢复。",0,true);throw new Exception("3分钟内未检测到实验客户端，请关闭游戏后重试启动。");}
    using(var sha=SHA256.Create())using(var f=File.OpenRead(PathExpected))if(BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=HashExpected)throw new Exception("客户端版本不匹配，未加载1秒补丁。");
    long image=p.MainModule.BaseAddress.ToInt64(),delta=image-0x140000000,call=0x1407c56fb+delta,slot=0x141cf99d8+delta;
    handle=OpenProcess(status?0x410u:0x438u,false,p.Id);if(handle==IntPtr.Zero)throw new Exception("无法访问实验客户端，请确认游戏与网页的运行权限一致。");
    if(!status){mutex=new Mutex(false,"Local\\BF4DeathFast-"+p.Id);try{locked=mutex.WaitOne(0);}catch(AbandonedMutexException){locked=true;}if(!locked)throw new Exception("1秒补丁正在处理，请稍后核验。");}
    State previous=Load();
    if(status){
     if(!Session(previous,p))return Emit(machine,"not-loaded","本次游戏未加载1秒补丁；请退出后从网页或1秒实验入口重新启动。",p.Id,true);
     var bindings=Bindings(previous,delta);bool allOriginal=true,allPatched=true;
     foreach(var b in bindings){var actual=Read(b.Address,b.Original.Length);allOriginal&=Same(actual,b.Original);allPatched&=Same(actual,b.Patched);if(!Same(actual,b.Original)&&!Same(actual,b.Patched))return Emit(machine,"conflict","补丁位置与记录不符，请退出 BF4 后重开。",p.Id,true);}
     if(allPatched&&previous.callAddress==call&&previous.revision=="web-recovery-3")return Emit(machine,"applied","死亡卡片1秒补丁已加载到当前游戏。",p.Id,true);
     if(allOriginal)return Emit(machine,"not-loaded","当前游戏未加载1秒补丁；重新启动游戏后应用。",p.Id,true);
     return Emit(machine,"interrupted","检测到旧版或未完成的补丁，请使用还原入口，或正常退出 BF4 后重开。",p.Id,true);
    }
    if(Session(previous,p)&&DeathFastRecovery.Recoverable(previous.status)){
     var bindings=Bindings(previous,delta);bool installed=previous.revision=="web-recovery-3"&&previous.callAddress==call&&Same(Read(call,5),previous.callPatched)&&Same(Read(slot,8),previous.gatePatched);
     if(!restore&&installed){ClearError();return Emit(machine,"applied","死亡卡片1秒补丁已加载到当前游戏，无需重复安装。",p.Id,true);}
     Restore(previous,delta);if(restore){ClearError();return Emit(machine,"restored","已还原本次死亡卡片补丁；退出游戏后释放小段辅助内存。",p.Id,true);}
    }else if(restore)throw new Exception("没有当前游戏的可恢复记录，请正常退出 BF4 恢复。");
    byte[] callOriginal={0xe8,0x80,0xfd,0xff,0xff},gateOriginal=BitConverter.GetBytes(0x1407cc390+delta);
    if(!Same(Read(call,5),callOriginal)||!Same(Read(slot,8),gateOriginal))throw new Exception("补丁位置被占用，未覆盖；请正常退出 BF4 后重开。");
    IntPtr allocation=IntPtr.Zero;for(long a=image+0x4000000;a<image+0x40000000&&allocation==IntPtr.Zero;a+=0x10000)allocation=VirtualAllocEx(handle,new IntPtr(a),(UIntPtr)4096,0x3000,4);
    if(allocation==IntPtr.Zero)throw new Exception("无法分配补丁内存");long ptr=allocation.ToInt64();WriteCode(ptr,DeathFastPatch.Delay(0x1407c5480+delta));WriteCode(ptr+512,DeathFastPatch.Gate(0x1407cc390+delta,0x1407fc970+delta));uint prior;
    if(!VirtualProtectEx(handle,allocation,(UIntPtr)4096,0x20,out prior)||!FlushInstructionCache(handle,allocation,(UIntPtr)4096))throw new Exception("无法完成补丁代码准备");
    var current=new State{pid=p.Id,startTicks=p.StartTime.ToUniversalTime().Ticks,baseAddress=image,allocation=ptr,callAddress=call,gateAddress=slot,status="prepared",revision="web-recovery-3",callOriginal=callOriginal,callPatched=DeathFastPatch.Call(call,ptr),gateOriginal=gateOriginal,gatePatched=BitConverter.GetBytes(ptr+512)};Save(current);
    try{Patch(slot,gateOriginal,current.gatePatched);Patch(call,callOriginal,current.callPatched);current.status="installed";Save(current);}catch{Restore(current,delta);throw;}
    ClearError();return Emit(machine,"applied","死亡卡片1秒补丁已加载到当前游戏。",p.Id,true);
   }
  }catch(Exception e){if(!status)try{AtomicText(System.IO.Path.Combine(home,"last-error.txt"),DateTime.UtcNow.ToString("o")+" "+e.Message);}catch{}return Emit(machine,"error",e.Message,target,false);}
  finally{if(handle!=IntPtr.Zero)CloseHandle(handle);if(locked)mutex.ReleaseMutex();if(mutex!=null)mutex.Dispose();}
 }
 static void ClearError(){string file=System.IO.Path.Combine(home,"last-error.txt");if(File.Exists(file))try{File.Delete(file);}catch{}}
 static void WriteCode(long a,byte[] bytes){UIntPtr got;if(!WriteProcessMemory(handle,new IntPtr(a),bytes,(UIntPtr)bytes.Length,out got)||got.ToUInt64()!=(ulong)bytes.Length||!Same(Read(a,bytes.Length),bytes))throw new Exception("补丁代码写入核验失败");}
 static Process WaitForGame(int target,bool wait){Process p=Find(target);if(wait&&p==null){DateTime end=DateTime.UtcNow.AddMinutes(3);while(p==null&&DateTime.UtcNow<end){Thread.Sleep(250);p=Find(target);}}return p;}
}
