using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

internal sealed class FlightDriver
{
	private readonly Mem m;

	private readonly bool server;

	private long control;

	private int seq;

	public long BrakeCalls
	{
		get
		{
			try
			{
				return (control == 0) ? 0 : m.Q(control + 72);
			}
			catch
			{
				return 0L;
			}
		}
	}

	public long Calls
	{
		get
		{
			try
			{
				return (control == 0) ? 0 : m.Q(control + 56);
			}
			catch
			{
				return 0L;
			}
		}
	}

	[DllImport("kernel32.dll")]
	private static extern ulong GetTickCount64();

	[DllImport("kernel32.dll")]
	private static extern bool WriteProcessMemory(IntPtr h, IntPtr p, byte[] b, UIntPtr n, out UIntPtr done);

	public FlightDriver(Mem memory, bool isServer)
	{
		m = memory;
		server = isServer;
	}

	private void Write(long p, byte[] b)
	{
		if (!WriteProcessMemory(m.H, new IntPtr(p), b, (UIntPtr)(ulong)b.Length, out var done) || done.ToUInt64() != (ulong)b.Length)
		{
			throw new Exception("Flight control write failed");
		}
	}

	private void Locate()
	{
		if (control != 0)
		{
			return;
		}
		m.Process.Refresh();
		ProcessModule processModule = m.Process.Modules.Cast<ProcessModule>().SingleOrDefault((ProcessModule x) => x.ModuleName == "BF4Flight2.dll");
		if (processModule == null)
		{
			throw new Exception("飞行模块未加载，请重新应用设置");
		}
		if (!string.Equals(processModule.FileName, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BF4Flight2.dll"), StringComparison.OrdinalIgnoreCase))
		{
			throw new Exception("Flight module path mismatch");
		}
		long num = processModule.BaseAddress.ToInt64();
		int num2 = m.I(num + 60);
		long num3 = num + m.I(num + num2 + 136);
		int num4 = m.I(num3 + 24);
		if (num4 < 1 || num4 > 64)
		{
			throw new Exception("Invalid flight exports");
		}
		long num5 = num + m.I(num3 + 32);
		long num6 = num + m.I(num3 + 36);
		long num7 = num + m.I(num3 + 28);
		for (int i = 0; i < num4; i++)
		{
			if (m.S(num + m.I(num5 + i * 4), 64) == "FlightControl")
			{
				int num8 = BitConverter.ToUInt16(m.R(num6 + i * 2, 2), 0);
				control = num + m.I(num7 + num8 * 4);
				break;
			}
		}
		if (control != 0 && m.I(control + 64) == 1)
		{
			return;
		}
		throw new Exception("飞行物理入口尚未就绪");
	}

	public void Clear()
	{
		if (control == 0)
		{
			return;
		}
		try
		{
			Write(control + 4, new byte[4]);
		}
		catch
		{
		}
	}

	public void Set(long soldier, float vertical, bool brake)
	{
		Locate();
		long num = m.Q(soldier + (server ? 576 : 440));
		long num2 = m.Q(num + 112);
		long num3 = m.Q(soldier + (server ? 616 : 480));
		if (m.Q(num) != m.Base + (server ? 33843328 : 31146368) || !Mem.Ptr(num2) || !Mem.Ptr(num3))
		{
			throw new Exception("飞行角色物理实例已变化");
		}
		byte[] array = new byte[52];
		Array.Copy(BitConverter.GetBytes(1), 0, array, 0, 4);
		Array.Copy(BitConverter.GetBytes(GetTickCount64() + 350), 0, array, 4, 8);
		Array.Copy(BitConverter.GetBytes(num2), 0, array, 12, 8);
		Array.Copy(BitConverter.GetBytes(num), 0, array, 20, 8);
		Array.Copy(BitConverter.GetBytes(soldier), 0, array, 28, 8);
		Array.Copy(BitConverter.GetBytes(num3), 0, array, 36, 8);
		Array.Copy(BitConverter.GetBytes(vertical), 0, array, 44, 4);
		Array.Copy(BitConverter.GetBytes(brake ? 1 : 0), 0, array, 48, 4);
		seq += 2;
		Write(control, BitConverter.GetBytes(seq - 1));
		Write(control + 4, array);
		Write(control, BitConverter.GetBytes(seq));
	}
}
