using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal sealed class CameraKeys : IDisposable
{
	private delegate IntPtr Callback(int c, IntPtr w, IntPtr l);

	private readonly Callback callback;

	private readonly Thread thread;

	private readonly CameraToggleState state = new CameraToggleState();

	private readonly ManualResetEvent ready = new ManualResetEvent(initialState: false);

	private IntPtr hook;

	private uint tid;

	private volatile int pid;

	private volatile bool armed;

	private int pending;

	[DllImport("user32.dll")]
	private static extern IntPtr SetWindowsHookEx(int id, Callback cb, IntPtr mod, uint thread);

	[DllImport("user32.dll")]
	private static extern bool UnhookWindowsHookEx(IntPtr h);

	[DllImport("user32.dll")]
	private static extern IntPtr CallNextHookEx(IntPtr h, int c, IntPtr w, IntPtr l);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr w, out uint p);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int key);

	[DllImport("user32.dll")]
	private static extern bool PostThreadMessage(uint t, uint msg, IntPtr w, IntPtr l);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetModuleHandle(string s);

	public CameraKeys()
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
			throw new Exception("无法监听 C 键视角切换");
		}
	}

	private IntPtr OnKey(int code, IntPtr w, IntPtr l)
	{
		if (code >= 0)
		{
			int num = Marshal.ReadInt32(l, 8);
			if ((num & 0x10) == 0)
			{
				GetWindowThreadProcessId(GetForegroundWindow(), out var p);
				bool modifiers = ((uint)GetAsyncKeyState(17) & 0x8000u) != 0 || ((uint)GetAsyncKeyState(18) & 0x8000u) != 0 || ((uint)GetAsyncKeyState(91) & 0x8000u) != 0 || (GetAsyncKeyState(92) & 0x8000) != 0;
				int num2 = state.Key(Marshal.ReadInt32(l), (num & 0x80) == 0, p == (uint)pid, armed, modifiers);
				if (num2 == 2)
				{
					Interlocked.Increment(ref pending);
				}
				if (num2 != 0)
				{
					return new IntPtr(1);
				}
			}
		}
		return CallNextHookEx(hook, code, w, l);
	}

	public static bool RearLook(int processId)
	{
		GetWindowThreadProcessId(GetForegroundWindow(), out var p);
		return p == (uint)processId && (GetAsyncKeyState(4) & 0x8000) != 0;
	}

	public bool Poll(int processId, bool enabled)
	{
		pid = processId;
		armed = enabled;
		int num = Interlocked.Exchange(ref pending, 0);
		return enabled && (num & 1) != 0;
	}

	public void Dispose()
	{
		armed = false;
		if (tid != 0)
		{
			PostThreadMessage(tid, 18u, IntPtr.Zero, IntPtr.Zero);
		}
		thread.Join(1000);
	}
}
