using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

internal sealed class CameraDriver
{
	private readonly Mem m;

	private long control;

	private int seq;

	private long modelControl;

	private long bodyControl;

	private long entryControl;

	private long data;

	private long savedSoldier;

	private long savedCamera;

	private float originalDistance;

	private float originalAngle;

	private float originalHeight;

	private float writtenHeight;

	private float writtenDistance;

	private float originalPitch;

	private float writtenPitch;

	public long BodyCalls
	{
		get
		{
			try
			{
				return (bodyControl == 0) ? 0 : m.Q(bodyControl + 16);
			}
			catch
			{
				return 0L;
			}
		}
	}

	public long ModelCalls
	{
		get
		{
			try
			{
				return (modelControl == 0) ? 0 : m.Q(modelControl + 16);
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
				return (control == 0) ? 0 : m.Q(control + 48);
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

	public CameraDriver(Mem memory)
	{
		m = memory;
	}

	private void Write(long p, byte[] b)
	{
		if (!WriteProcessMemory(m.H, new IntPtr(p), b, (UIntPtr)(ulong)b.Length, out var done) || done.ToUInt64() != (ulong)b.Length)
		{
			throw new Exception("Camera control write failed");
		}
	}

	private long Export(string moduleName, string exportName)
	{
		m.Process.Refresh();
		ProcessModule processModule = m.Process.Modules.Cast<ProcessModule>().SingleOrDefault((ProcessModule x) => x.ModuleName == moduleName);
		if (processModule == null)
		{
			throw new Exception("镜头模块未加载，请重新应用设置");
		}
		if (!string.Equals(processModule.FileName, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, moduleName), StringComparison.OrdinalIgnoreCase))
		{
			throw new Exception("Camera module path mismatch");
		}
		long num = processModule.BaseAddress.ToInt64();
		int num2 = m.I(num + 60);
		long num3 = num + m.I(num + num2 + 136);
		int num4 = m.I(num3 + 24);
		if (num4 < 1 || num4 > 64)
		{
			throw new Exception("Invalid camera exports");
		}
		long num5 = num + m.I(num3 + 32);
		long num6 = num + m.I(num3 + 36);
		long num7 = num + m.I(num3 + 28);
		for (int i = 0; i < num4; i++)
		{
			if (m.S(num + m.I(num5 + i * 4), 64) == exportName)
			{
				int num8 = BitConverter.ToUInt16(m.R(num6 + i * 2, 2), 0);
				return num + m.I(num7 + num8 * 4);
			}
		}
		throw new Exception("Camera export missing");
	}

	private void Locate()
	{
		if (control == 0 || modelControl == 0)
		{
			long num = Export("BF4CameraBody2.dll", "ModelControl");
			if (m.I(num + 8) != 1)
			{
				throw new Exception("角色显示入口尚未就绪");
			}
			long num2 = Export("BF4Camera.dll", "CameraControl");
			long num3 = Export("BF4CameraModel.dll", "ModelControl");
			if (m.I(num2 + 56) != 1 || m.I(num3 + 8) != 1)
			{
				throw new Exception("镜头入口尚未就绪");
			}
			long num4 = Export("BF4CameraEntry.dll", "ModelControl");
			if (m.I(num4 + 8) != 1)
			{
				throw new Exception("角色视角显示入口尚未就绪");
			}
			Write(Export("BF4CameraAim.dll", "AimControl"), BitConverter.GetBytes(0L));
			Write(Export("BF4CameraAim2.dll", "AimControl"), BitConverter.GetBytes(0L));
			Write(Export("BF4CameraAim3.dll", "AimControl"), BitConverter.GetBytes(0L));
			Write(Export("BF4CameraAim4.dll", "AimControl"), BitConverter.GetBytes(0L));
			Write(Export("BF4CameraAim5.dll", "AimControl"), BitConverter.GetBytes(0L));
			Write(Export("BF4CameraAim6.dll", "AimControl"), BitConverter.GetBytes(0L));
			long num5 = Export("BF4CameraAim7.dll", "AimControl");
			if (m.I(num5 + 8) != 1)
			{
				throw new Exception("平滑镜头入口尚未就绪");
			}
			Write(num5, BitConverter.GetBytes(num2));
			Write(num5 + 24, BitConverter.GetBytes(3));
			Write(num4, BitConverter.GetBytes(num2));
			entryControl = num4;
			Write(num, BitConverter.GetBytes(num2));
			bodyControl = num;
			Write(num3, BitConverter.GetBytes(0L));
			control = num2;
			modelControl = num3;
		}
	}

	private bool Same()
	{
		try
		{
			if (data == 0 || m.Q(savedSoldier + 504) != savedCamera || m.Q(savedCamera) != m.Base + 29095376 || m.Q(savedCamera + 312) != savedSoldier)
			{
				return false;
			}
			long num = m.Q(savedCamera + 280);
			if (m.Q(savedCamera + 288) - num != 24)
			{
				return false;
			}
			long num2 = m.Q(num + 16);
			return m.Q(num2) == m.Base + 30544864 && m.Q(num2 + 248) == data && m.Q(data) == m.Base + 30990264;
		}
		catch
		{
			return false;
		}
	}

	public void Clear()
	{
		if (control != 0)
		{
			try
			{
				Write(control + 4, new byte[4]);
			}
			catch
			{
			}
		}
		if (data == 0)
		{
			return;
		}
		try
		{
			if (Same())
			{
				if (m.F(data + 252) == writtenDistance)
				{
					m.W(data + 252, originalDistance);
				}
				if (m.F(data + 212) == writtenHeight)
				{
					m.W(data + 212, originalHeight);
				}
				if (m.F(data + 248) == writtenPitch)
				{
					m.W(data + 248, originalPitch);
				}
				if (m.F(data + 240) == 180f)
				{
					m.W(data + 240, originalAngle);
				}
			}
		}
		catch
		{
		}
		data = 0L;
		savedSoldier = 0L;
		savedCamera = 0L;
	}

	public void Set(long soldier, float distance, float height, bool rear)
	{
		Locate();
		long num = m.Q(soldier + 504);
		long value = m.Q(soldier + 480);
		long num2 = m.Q(num + 280);
		if (m.Q(num) != m.Base + 29095376 || m.Q(num + 312) != soldier || m.Q(num + 288) - num2 != 24)
		{
			throw new Exception("当前步兵镜头不受支持");
		}
		long num3 = m.Q(num2 + 16);
		long num4 = m.Q(num3 + 248);
		if (m.Q(num3) != m.Base + 30544864 || m.Q(num4) != m.Base + 30990264)
		{
			throw new Exception("追踪镜头不可用");
		}
		if (data != num4 || savedSoldier != soldier)
		{
			Clear();
			originalDistance = m.F(num4 + 252);
			originalAngle = m.F(num4 + 240);
			originalHeight = m.F(num4 + 212);
			originalPitch = m.F(num4 + 248);
			if (originalDistance < 0f || originalDistance > 30f || float.IsNaN(originalDistance) || float.IsNaN(originalAngle))
			{
				throw new Exception("镜头距离初始值异常");
			}
			data = num4;
			savedSoldier = soldier;
			savedCamera = num;
		}
		float num5 = m.F(soldier + 1244);
		if (float.IsNaN(num5) || float.IsInfinity(num5) || Math.Abs(num5) > 3.2f)
		{
			throw new Exception("镜头朝向读取异常");
		}
		writtenPitch = Math.Max(-89f, Math.Min(89f, (0f - num5) * 180f / (float)Math.PI));
		m.W(data + 248, writtenPitch);
		writtenDistance = distance;
		m.W(data + 252, distance);
		m.W(data + 240, 180f);
		writtenHeight = originalHeight + height;
		m.W(data + 212, writtenHeight);
		byte[] array = new byte[44];
		Array.Copy(BitConverter.GetBytes(1), 0, array, 0, 4);
		Array.Copy(BitConverter.GetBytes(GetTickCount64() + 350), 0, array, 4, 8);
		Array.Copy(BitConverter.GetBytes(num), 0, array, 12, 8);
		Array.Copy(BitConverter.GetBytes(soldier), 0, array, 20, 8);
		Array.Copy(BitConverter.GetBytes(value), 0, array, 28, 8);
		Array.Copy(BitConverter.GetBytes(distance), 0, array, 36, 4);
		Array.Copy(BitConverter.GetBytes(rear ? 1 : 0), 0, array, 40, 4);
		seq += 2;
		Write(control, BitConverter.GetBytes(seq - 1));
		Write(control + 4, array);
		Write(control, BitConverter.GetBytes(seq));
	}
}
