using System;using System.Runtime.InteropServices;
class FastTests{
[DllImport("kernel32")]static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr n,uint t,uint p);[DllImport("kernel32")]static extern bool VirtualFree(IntPtr a,UIntPtr n,uint t);
[UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Schedule(IntPtr manager,IntPtr entity,float seconds,IntPtr ev);
[UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Gate(IntPtr entity,IntPtr ev);
[UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Fire(IntPtr entity,IntPtr bus,IntPtr data,uint id);
static int fails,total,originalCalls,fireCalls;static float observed;static IntPtr entity,data,ev,bus;static uint fired;static bool argsGood;
static void Check(bool ok,string name){total++;if(!ok){fails++;Console.WriteLine("FAIL "+name);}}
static void Put(IntPtr m,byte[] b){Marshal.Copy(b,0,m,b.Length);}
static int Main(){IntPtr memory=VirtualAlloc(IntPtr.Zero,(UIntPtr)16384,0x3000,0x40);if(memory==IntPtr.Zero)return 2;try{
 entity=IntPtr.Add(memory,8192);data=IntPtr.Add(memory,9216);ev=IntPtr.Add(memory,10240);bus=IntPtr.Add(memory,11264);Marshal.WriteIntPtr(entity,0x28,data);Marshal.WriteIntPtr(entity,0x20,bus);
 Schedule originalSchedule=delegate(IntPtr m,IntPtr e,float t,IntPtr v){observed=t;argsGood=m==bus&&e==entity&&v==ev;};
 Put(memory,DeathFastPatch.Delay(Marshal.GetFunctionPointerForDelegate(originalSchedule).ToInt64()));var schedule=(Schedule)Marshal.GetDelegateForFunctionPointer(memory,typeof(Schedule));
 Marshal.WriteInt32(data,0x10,440549240);Marshal.WriteInt32(data,0x18,0x41100000);schedule(bus,entity,10f,ev);Check(observed==1f&&argsGood,"target delay overrides dynamic 10 seconds");schedule(bus,entity,0.1f,ev);Check(observed==1f,"target delay fixed at one second");
 Marshal.WriteInt32(data,0x10,123);schedule(bus,entity,7f,ev);Check(observed==7f&&argsGood,"unrelated delay untouched");Marshal.WriteInt32(data,0x10,440549240);Marshal.WriteInt32(data,0x18,0);schedule(bus,entity,5f,ev);Check(observed==5f,"mismatched default rejected");
 Gate original=delegate(IntPtr e,IntPtr v){originalCalls++;argsGood=e==entity&&v==ev;};Fire fire=delegate(IntPtr e,IntPtr b,IntPtr d,uint id){fireCalls++;fired=id;argsGood=e==entity&&b==bus&&d==data;};
 IntPtr code=IntPtr.Add(memory,2048);Put(code,DeathFastPatch.Gate(Marshal.GetFunctionPointerForDelegate(original).ToInt64(),Marshal.GetFunctionPointerForDelegate(fire).ToInt64()));var gate=(Gate)Marshal.GetDelegateForFunctionPointer(code,typeof(Gate));Marshal.WriteInt32(data,0x10,36312304);Marshal.WriteInt32(ev,8,0x0b87d373);gate(entity,ev);Check(fireCalls==1&&originalCalls==0&&fired==0x0b87df4b&&argsGood,"target timer event advances flow");Check(Marshal.ReadInt32(ev,8)==0x0b87d373,"original event not mutated");Marshal.WriteInt32(ev,8,0x0b87d370);gate(entity,ev);Check(fireCalls==1&&originalCalls==0,"late server condition does not duplicate transition");Marshal.WriteInt32(ev,8,0x0db4b330);gate(entity,ev);Check(originalCalls==1&&argsGood,"reset preserved");Marshal.WriteInt32(data,0x10,123);Marshal.WriteInt32(ev,8,0x0b87d373);gate(entity,ev);Check(originalCalls==2&&fireCalls==1,"other AND nodes untouched");GC.KeepAlive(originalSchedule);GC.KeepAlive(original);GC.KeepAlive(fire);
 Console.WriteLine(total+" native cases; failures="+fails);return fails==0?0:1;}finally{VirtualFree(memory,UIntPtr.Zero,0x8000);}}
}
