using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

public sealed class Mem : IDisposable
{
	private readonly Dictionary<string, long> snapshots = new Dictionary<string, long>();

	public IntPtr H;

	public long Base;

	public Process Process;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

	[DllImport("kernel32.dll")]
	private static extern bool ReadProcessMemory(IntPtr handle, IntPtr address, byte[] data, UIntPtr size, out UIntPtr got);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(IntPtr h);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool WriteProcessMemory(IntPtr handle, IntPtr address, byte[] data, UIntPtr size, out UIntPtr done);

	public void W(long a, float value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		if (!Ptr(a) || !WriteProcessMemory(H, new IntPtr(a), bytes, (UIntPtr)4u, out var done) || done.ToUInt64() != 4)
		{
			throw new Exception("Write failed");
		}
	}

	[DllImport("kernel32.dll")]
	private static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr p, UIntPtr n, uint type, uint protect);

	public void Write(long a, byte[] bytes)
	{
		if (!Ptr(a) || !WriteProcessMemory(H, new IntPtr(a), bytes, (UIntPtr)(ulong)bytes.Length, out var done) || done.ToUInt64() != (ulong)bytes.Length || !R(a, bytes.Length).SequenceEqual(bytes))
		{
			throw new Exception("Weapon settings write was not verified");
		}
	}

	public long Snapshot(byte[] bytes)
	{
		string key;
		using (SHA256 sHA = SHA256.Create())
		{
			key = Convert.ToBase64String(sHA.ComputeHash(bytes));
		}
		if (snapshots.TryGetValue(key, out var value))
		{
			if (!R(value, bytes.Length).SequenceEqual(bytes))
			{
				throw new Exception("Private weapon settings changed");
			}
			return value;
		}
		value = VirtualAllocEx(H, IntPtr.Zero, (UIntPtr)4096u, 12288u, 4u).ToInt64();
		if (!Ptr(value))
		{
			throw new Exception("Cannot allocate private weapon settings");
		}
		Write(value, bytes);
		snapshots.Add(key, value);
		return value;
	}

	public Mem(Process p, string expectedHash)
	{
		Process = p;
		Base = p.MainModule.BaseAddress.ToInt64();
		using (FileStream inputStream = File.OpenRead(p.MainModule.FileName))
		{
			using SHA256 sHA = SHA256.Create();
			string text = BitConverter.ToString(sHA.ComputeHash(inputStream)).Replace("-", "").ToLowerInvariant();
			if (!V2RuntimeBuild.AcceptsHash(text, expectedHash))
			{
				throw new Exception("此执行器支持 v2（1107）游戏文件，请核对运行环境");
			}
		}
		H = OpenProcess(4152u, inherit: false, p.Id);
		if (H == IntPtr.Zero)
		{
			throw new Exception("Cannot open offline client for the isolated test");
		}
	}

	public static bool Ptr(long p)
	{
		return p >= 65536 && p < 140737488355327L;
	}

	public byte[] R(long a, int n)
	{
		if (!Ptr(a))
		{
			throw new Exception("Pointer unavailable");
		}
		byte[] array = new byte[n];
		if (!ReadProcessMemory(H, new IntPtr(a), array, (UIntPtr)(ulong)n, out var got) || got.ToUInt64() != (ulong)n)
		{
			throw new Exception("Read unavailable at " + a.ToString("x"));
		}
		return array;
	}

	public long Q(long a)
	{
		return BitConverter.ToInt64(R(a, 8), 0);
	}

	public int I(long a)
	{
		return BitConverter.ToInt32(R(a, 4), 0);
	}

	public float F(long a)
	{
		return BitConverter.ToSingle(R(a, 4), 0);
	}

	public byte B(long a)
	{
		return R(a, 1)[0];
	}

	public string S(long a, int n)
	{
		byte[] array = R(a, n);
		int num = Array.IndexOf(array, (byte)0);
		return Encoding.UTF8.GetString(array, 0, (num < 0) ? n : num);
	}

	public float[] V(long a, int n)
	{
		byte[] value = R(a, n * 4);
		float[] array = new float[n];
		for (int i = 0; i < n; i++)
		{
			array[i] = BitConverter.ToSingle(value, i * 4);
		}
		return array;
	}

	public void Dispose()
	{
		if (H != IntPtr.Zero)
		{
			CloseHandle(H);
		}
		H = IntPtr.Zero;
	}
}
