using System;
using System.Runtime.InteropServices;

internal sealed class BallisticsDriver
{
	public const float FastSpeed = 100000f;

	private readonly Mem m;

	private long context;

	private long weapon;

	private long firing;

	private long clientWeapon;

	private long original;

	private long clone;

	private bool drop;

	private bool fast;

	private int allocations;

	public float OriginalSpeed;

	public float OriginalGravity;

	public bool Active => Owned();

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr p, UIntPtr n, uint type, uint protect);

	[DllImport("kernel32.dll")]
	private static extern bool WriteProcessMemory(IntPtr h, IntPtr p, byte[] b, UIntPtr n, out UIntPtr done);

	public BallisticsDriver(Mem memory)
	{
		m = memory;
	}

	private void Write(long p, byte[] bytes)
	{
		if (!WriteProcessMemory(m.H, new IntPtr(p), bytes, (UIntPtr)(ulong)bytes.Length, out var done) || done.ToUInt64() != (ulong)bytes.Length)
		{
			throw new Exception("Ballistics write failed");
		}
	}

	private bool Owned()
	{
		try
		{
			return clone != 0 && m.Q(m.Base + 40308096) == context && m.Q(weapon + 18880) == firing && m.Q(firing + 296) == clone && m.Q(weapon + 18856) == clientWeapon && m.Q(clientWeapon + 24) == clone && m.Q(original) == m.Base + 31052728;
		}
		catch
		{
			return false;
		}
	}

	public void Clear()
	{
		if (clone == 0)
		{
			return;
		}
		try
		{
			if (m.Q(m.Base + 40308096) == context && m.Q(original) == m.Base + 31052728)
			{
				if (m.Q(weapon + 18856) == clientWeapon && m.Q(clientWeapon + 24) == clone)
				{
					Write(clientWeapon + 24, BitConverter.GetBytes(original));
				}
				if (m.Q(weapon + 18880) == firing && m.Q(firing + 296) == clone)
				{
					Write(firing + 296, BitConverter.GetBytes(original));
				}
			}
		}
		finally
		{
			clone = 0L;
		}
	}

	public bool Set(long soldier, bool noDrop, bool fastBullets)
	{
		if ((!noDrop && !fastBullets) || !Mem.Ptr(soldier))
		{
			Clear();
			return false;
		}
		long num = m.Q(soldier + 1392);
		int num2 = m.I(num + 2712);
		if (num2 < 0 || num2 > 1)
		{
			Clear();
			return false;
		}
		long num3 = m.Q(m.Q(num + 2192) + num2 * 8);
		long num4 = m.Q(num3 + 18880);
		if (Active && num3 == weapon && num4 == firing && drop == noDrop && fast == fastBullets && Owned())
		{
			return true;
		}
		Clear();
		long num5 = m.Q(num3 + 18856);
		if (m.Q(num5) != m.Base + 30660496)
		{
			return false;
		}
		long num6 = m.Q(num4 + 296);
		if (m.Q(num5 + 24) != num6)
		{
			return false;
		}
		if (m.Q(num6) != m.Base + 31052728)
		{
			return false;
		}
		long num7 = m.Q(num6 + 16);
		if (m.Q(num7) != m.Base + 31052672)
		{
			return false;
		}
		long num8 = m.Q(num7 + 176);
		if (!Mem.Ptr(num8) || m.Q(num8) != m.Base + 31052184 || m.Q(num7 + 184) != 0)
		{
			return false;
		}
		byte[] fd = m.R(num6, 88);
		byte[] fn = m.R(num7, 592);
		byte[] bullet = m.R(num8, 368);
		BallisticsPlan ballisticsPlan = BallisticsPlan.Create(fd, fn, bullet, noDrop, fastBullets);
		OriginalSpeed = ballisticsPlan.Speed;
		OriginalGravity = ballisticsPlan.Gravity;
		if (++allocations > 128)
		{
			throw new Exception("弹道测试分配次数已达上限，请重新启动玩家模块");
		}
		long num9 = VirtualAllocEx(m.H, IntPtr.Zero, (UIntPtr)4096u, 12288u, 4u).ToInt64();
		if (!Mem.Ptr(num9))
		{
			throw new Exception("Cannot allocate local bullet settings");
		}
		Array.Copy(BitConverter.GetBytes(num9 + 256), 0, ballisticsPlan.Firing, 16, 8);
		Array.Copy(BitConverter.GetBytes(num9 + 1024), 0, ballisticsPlan.Function, 176, 8);
		Write(num9, ballisticsPlan.Firing);
		Write(num9 + 256, ballisticsPlan.Function);
		Write(num9 + 1024, ballisticsPlan.Bullet);
		if (m.Q(num3 + 18880) != num4 || m.Q(num4 + 296) != num6 || m.Q(num3 + 18856) != num5 || m.Q(num5 + 24) != num6)
		{
			throw new Exception("Weapon changed before applying ballistics");
		}
		context = m.Q(m.Base + 40308096);
		weapon = num3;
		firing = num4;
		clientWeapon = num5;
		original = num6;
		clone = num9;
		drop = noDrop;
		fast = fastBullets;
		Write(num4 + 296, BitConverter.GetBytes(num9));
		Write(num5 + 24, BitConverter.GetBytes(num9));
		return Owned();
	}
}
