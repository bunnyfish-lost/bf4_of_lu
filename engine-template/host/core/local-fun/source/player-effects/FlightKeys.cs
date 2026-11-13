using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal sealed class FlightKeys : IDisposable
{
	private delegate IntPtr Callback(int code, IntPtr w, IntPtr l);

	private readonly Callback callback;

	private readonly Thread thread;

	private IntPtr hook;

	private uint tid;

	private volatile bool up;

	private volatile bool down;

	private readonly ManualResetEvent ready = new ManualResetEvent(initialState: false);

	[DllImport("user32.dll")]
	private static extern IntPtr SetWindowsHookEx(int id, Callback fn, IntPtr mod, uint thread);

	[DllImport("user32.dll")]
	private static extern bool UnhookWindowsHookEx(IntPtr h);

	[DllImport("user32.dll")]
	private static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int key);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr w, out uint p);

	[DllImport("user32.dll")]
	private static extern bool PostThreadMessage(uint thread, uint msg, IntPtr w, IntPtr l);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetModuleHandle(string s);

	public FlightKeys()
	{
		callback = OnKey;
		ThreadStart start = delegate
		{
			tid = GetCurrentThreadId();
			hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0u);
			ready.Set();
			if (hook != IntPtr.Zero)
			{
				try
				{
					Application.Run();
				}
				finally
				{
					UnhookWindowsHookEx(hook);
				}
			}
		};
		thread = new Thread(start);
		thread.IsBackground = true;
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		ready.WaitOne(2000);
		if (hook == IntPtr.Zero)
		{
			throw new Exception("无法监听小键盘飞行按键");
		}
	}

	private IntPtr OnKey(int code, IntPtr w, IntPtr l)
	{
		if (code >= 0)
		{
			int num = Marshal.ReadInt32(l, 4);
			int num2 = Marshal.ReadInt32(l, 8);
			if ((num2 & 1) == 0)
			{
				bool flag = (num2 & 0x80) == 0;
				if (num == 72)
				{
					up = flag;
				}
				if (num == 80)
				{
					down = flag;
				}
			}
		}
		return CallNextHookEx(hook, code, w, l);
	}

	public bool Moving(int pid)
	{
		GetWindowThreadProcessId(GetForegroundWindow(), out var p);
		if (p != (uint)pid)
		{
			return false;
		}
		return FlightKeyMath.Moving((GetAsyncKeyState(87) & 0x8000) != 0, (GetAsyncKeyState(83) & 0x8000) != 0, (GetAsyncKeyState(65) & 0x8000) != 0, (GetAsyncKeyState(68) & 0x8000) != 0);
	}

	public int Axis(int pid)
	{
		GetWindowThreadProcessId(GetForegroundWindow(), out var p);
		if (p != (uint)pid)
		{
			up = false;
			down = false;
			return 0;
		}
		return (up ? 1 : 0) - (down ? 1 : 0);
	}

	public void Dispose()
	{
		up = (down = false);
		if (tid != 0)
		{
			PostThreadMessage(tid, 18u, IntPtr.Zero, IntPtr.Zero);
		}
		thread.Join(1000);
	}
}
