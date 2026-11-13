using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

// Version-locked access to the registered PseudoPlayer.SpawnCount integer.
// The engine's own PseudoPlayerManager creates/removes bots on its game thread.
// This does not inject code or patch instructions.
public static class NativeBotCount {
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h, IntPtr a, byte[] b, UIntPtr n, out UIntPtr read);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool WriteProcessMemory(IntPtr h, IntPtr a, byte[] b, UIntPtr n, out UIntPtr written);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError=true)] static extern UIntPtr VirtualQueryEx(IntPtr h, IntPtr a, out MemoryInfo info, UIntPtr size);
    [StructLayout(LayoutKind.Sequential)] struct MemoryInfo {
        public IntPtr BaseAddress, AllocationBase;
        public uint AllocationProtect;
        public UIntPtr RegionSize;
        public uint State, Protect, Type;
    }
    static byte[] Read(IntPtr h, long address, int count) {
        byte[] b = new byte[count]; UIntPtr actual;
        if (!ReadProcessMemory(h, new IntPtr(address), b, (UIntPtr)count, out actual) || actual.ToUInt64() != (ulong)count)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read registered Bot parameter.");
        return b;
    }
    static void Check(IntPtr h, long address, byte[] expected) {
        byte[] actual = Read(h,address,expected.Length);
        for(int i=0;i<actual.Length;i++) if(actual[i]!=expected[i]) throw new InvalidOperationException("Unsupported loaded server layout.");
    }
    public static int Apply(int pid, long baseAddress, int target, int expected) {
        if (target < -1 || target > 64 || expected < -1 || expected > 64) throw new ArgumentOutOfRangeException("Bot count");
        uint access = target < 0 ? 0x1010u : 0x1038u;
        IntPtr h = OpenProcess(access,false,pid);
        if (h == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot open the tracked game process.");
        try {
            Check(h,baseAddress+0x1D81010,Encoding.ASCII.GetBytes("PseudoPlayer.SpawnCount\0"));
            Check(h,baseAddress+0x1AFA030,new byte[]{0x48,0x83,0xec,0x28,0x48,0x8d,0x0d,0xd5,0x6f,0x28,0,0x45,0x33,0xc0,0x33,0xd2,0xe8,0xbb,0x59,0xc2,0xfe,0x48,0x89,0x05,0x24,0x73,0xb7,0});
            Check(h,baseAddress+0x2639CB,new byte[]{0x48,0x8b,0x05,0x9e,0xd9,0x40,0x02,0x44,0x3b,0x00});
            long pointer = BitConverter.ToInt64(Read(h,baseAddress+0x2671370,8),0);
            if(pointer < 0x10000 || pointer > 0x7FFFFFFEFFFF || (pointer & 3) != 0) throw new InvalidOperationException("Invalid Bot parameter pointer.");
            MemoryInfo mi;
            if(VirtualQueryEx(h,new IntPtr(pointer),out mi,(UIntPtr)Marshal.SizeOf(typeof(MemoryInfo)))==UIntPtr.Zero || mi.State!=0x1000 || (mi.Protect!=4 && mi.Protect!=8) || pointer+4>mi.BaseAddress.ToInt64()+(long)mi.RegionSize.ToUInt64())
                throw new InvalidOperationException("Bot parameter is not in a writable data page.");
            int current = BitConverter.ToInt32(Read(h,pointer,4),0);
            if(current<0 || current>64) throw new InvalidOperationException("Unexpected current Bot count.");
            if(target<0) return current;
            if(expected<0 || current!=expected) throw new InvalidOperationException("Bot count changed; refresh before retrying.");
            if(current==target) return current;
            // Recheck the registered pointer immediately before the four-byte write.
            if(BitConverter.ToInt64(Read(h,baseAddress+0x2671370,8),0)!=pointer) throw new InvalidOperationException("Bot parameter moved.");
            UIntPtr written;
            if(!WriteProcessMemory(h,new IntPtr(pointer),BitConverter.GetBytes(target),(UIntPtr)4,out written) || written.ToUInt64()!=4)
                throw new Win32Exception(Marshal.GetLastWin32Error(),"Bot parameter write failed.");
            int observed=BitConverter.ToInt32(Read(h,pointer,4),0);
            if(observed!=target) throw new InvalidOperationException("Bot count readback did not match.");
            return observed;
        } finally { CloseHandle(h); }
    }
}
