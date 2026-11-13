using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]

public static class BF4LocalAssist
{
	private struct MEMORY_BASIC_INFORMATION
	{
		public ulong BaseAddress;

		public ulong AllocationBase;

		public uint AllocationProtect;

		public uint Align1;

		public ulong RegionSize;

		public uint State;

		public uint Protect;

		public uint Type;

		public uint Align2;
	}

	private const string ExpectedHash = V2RuntimeBuild.ClientHash;

	private const long ServerSite = 5371037197L;

	private static string ExpectedPath;

	private static string ServerPath;

	private static string OwnerPath;

	private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

	private static readonly byte[][] Original = new byte[3][]
	{
		new byte[5] { 232, 115, 47, 229, 255 },
		new byte[5] { 232, 118, 44, 229, 255 },
		new byte[5] { 232, 80, 151, 249, 255 }
	};

	private static readonly byte[] Enabled = new byte[5] { 176, 1, 144, 144, 144 };

	private static readonly long[] Sites = new long[3] { 5372753288L, 5372754053L, 5371415979L };

	private static byte[] MapEnabled;

	private static readonly byte[] ServerOriginal = new byte[5] { 232, 238, 191, 1, 0 };

	private static Process ServerProcess;

	private static IntPtr ServerHandle;

	private static byte[] ServerEnabled;

	private static bool ServerApplied;

	private static IntPtr Handle;

	[DllImport("kernel32", SetLastError = true)]
	private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

	[DllImport("kernel32", SetLastError = true)]
	private static extern bool ReadProcessMemory(IntPtr h, IntPtr a, byte[] b, UIntPtr n, out UIntPtr read);

	[DllImport("kernel32", SetLastError = true)]
	private static extern bool WriteProcessMemory(IntPtr h, IntPtr a, byte[] b, UIntPtr n, out UIntPtr wrote);

	[DllImport("kernel32", SetLastError = true)]
	private static extern bool VirtualProtectEx(IntPtr h, IntPtr a, UIntPtr n, uint p, out uint old);

	[DllImport("kernel32", SetLastError = true)]
	private static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr address, UIntPtr n, uint allocation, uint protection);

	[DllImport("kernel32")]
	private static extern bool CloseHandle(IntPtr h);

	[DllImport("kernel32")]
	private static extern bool FlushInstructionCache(IntPtr h, IntPtr a, UIntPtr n);

	[DllImport("user32")]
	private static extern short GetAsyncKeyState(int key);

	[DllImport("user32")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32")]
	private static extern uint GetWindowThreadProcessId(IntPtr w, out uint pid);

	private static string Digest(byte[] b)
	{
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(b)).Replace("-", "").ToLowerInvariant();
	}

	private static void Emit(object value)
	{
		Console.WriteLine(Json.Serialize(value));
		Console.Out.Flush();
	}

	private static long Now()
	{
		return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
	}

	private static byte[] Read(long a, int n)
	{
		if (a < 65536 || a > 140737488355327L)
		{
			throw new Exception("invalid pointer");
		}
		byte[] array = new byte[n];
		if (!ReadProcessMemory(Handle, new IntPtr(a), array, (UIntPtr)(ulong)n, out var read) || read.ToUInt64() != (ulong)n)
		{
			throw new Exception("memory read unavailable");
		}
		return array;
	}

	private static long Ptr(long a)
	{
		return BitConverter.ToInt64(Read(a, 8), 0);
	}

	private static int Int(long a)
	{
		return BitConverter.ToInt32(Read(a, 4), 0);
	}

	private static float Float(long a)
	{
		return BitConverter.ToSingle(Read(a, 4), 0);
	}

	private static bool Same(byte[] a, byte[] b)
	{
		return BitConverter.ToString(a) == BitConverter.ToString(b);
	}

	private static byte[] Next(int index)
	{
		if (index != 2)
		{
			return Enabled;
		}
		return MapEnabled;
	}

	[DllImport("kernel32", SetLastError = true)]
	private static extern UIntPtr VirtualQueryEx(IntPtr h, IntPtr a, out MEMORY_BASIC_INFORMATION info, UIntPtr length);

	private static IntPtr AllocateTeamBridge()
	{
		long num = 5435817984L;
		while (num < 7516192768L)
		{
			if (VirtualQueryEx(Handle, new IntPtr(num), out var info, (UIntPtr)48u) == UIntPtr.Zero)
			{
				throw new Exception("cannot query team-check space: " + Marshal.GetLastWin32Error());
			}
			long num2 = checked((long)(info.BaseAddress + info.RegionSize));
			if (num2 <= num)
			{
				throw new Exception("invalid team-check region");
			}
			if (info.State == 65536)
			{
				long num3 = (num + 65535) & -65536;
				if (num3 < 7516192768L && num3 + 4096 <= num2)
				{
					IntPtr intPtr = VirtualAllocEx(Handle, new IntPtr(num3), (UIntPtr)4096u, 12288u, 4u);
					if (intPtr != IntPtr.Zero)
					{
						return intPtr;
					}
					num = num3 + 65536;
					continue;
				}
			}
			num = num2;
		}
		throw new Exception("no reachable free team-check region");
	}

	private static byte[] PrepareTeamCheck(int extension, long site)
	{
		if (extension < 4096 || extension > 65536)
		{
			throw new Exception("unsupported player extension");
		}
		IntPtr intPtr = AllocateTeamBridge();
		if (intPtr == IntPtr.Zero)
		{
			throw new Exception("cannot allocate team check");
		}
		byte[] array = AssistTeamPatch.Code(extension);
		if (!WriteProcessMemory(Handle, intPtr, array, (UIntPtr)(ulong)array.Length, out var wrote) || wrote.ToUInt64() != (ulong)array.Length)
		{
			throw new Exception("cannot prepare team check");
		}
		if (!VirtualProtectEx(Handle, intPtr, (UIntPtr)4096u, 32u, out var _))
		{
			throw new Exception("cannot protect team check");
		}
		FlushInstructionCache(Handle, intPtr, (UIntPtr)(ulong)array.Length);
		return AssistTeamPatch.Call(site, intPtr.ToInt64());
	}

	private static void PrepareMapCheck()
	{
		if (MapEnabled == null)
		{
			int num = Int(5405363352L);
			if (num != 5472)
			{
				throw new Exception("unsupported client extension");
			}
			byte[] array = Read(Sites[2], 5);
			MapEnabled = (Same(array, Original[2]) ? PrepareTeamCheck(num, Sites[2]) : AssistTeamPatch.Recover(Sites[2], array, num, Read));
		}
	}

	private static void UpdateSite(long site, byte[] prior, byte[] enabled, bool value)
	{
		byte[] a = Read(site, 5);
		if (!Same(a, prior) && (enabled == null || !Same(a, enabled)))
		{
			throw new Exception("unsupported server deploy signature; restart BOT server");
		}
		byte[] b = (value ? enabled : prior);
		if (Same(a, b))
		{
			return;
		}
		if (!VirtualProtectEx(Handle, new IntPtr(site), (UIntPtr)5u, 64u, out var old))
		{
			throw new Exception("cannot protect server deploy site");
		}
		try
		{
			if (!WriteProcessMemory(Handle, new IntPtr(site), b, (UIntPtr)5u, out var wrote) || wrote.ToUInt64() != 5)
			{
				throw new Exception("cannot update server deploy site");
			}
			FlushInstructionCache(Handle, new IntPtr(site), (UIntPtr)5u);
		}
		finally
		{
			VirtualProtectEx(Handle, new IntPtr(site), (UIntPtr)5u, old, out var _);
		}
		if (Same(Read(site, 5), b))
		{
			return;
		}
		throw new Exception("server deploy readback mismatch");
	}

	private static bool FindServer()
	{
		if (ServerProcess != null && !ServerProcess.HasExited)
		{
			return true;
		}
		if (ServerHandle != IntPtr.Zero)
		{
			CloseHandle(ServerHandle);
		}
		ServerHandle = IntPtr.Zero;
		ServerProcess = null;
		ServerEnabled = null;
		ServerApplied = false;
		Process[] processesByName = Process.GetProcessesByName("bf4_Server_Final");
		foreach (Process process in processesByName)
		{
			string fileName = process.MainModule.FileName;
			if (string.Equals(fileName, ServerPath, StringComparison.OrdinalIgnoreCase))
			{
				if (process.MainModule.BaseAddress.ToInt64() != 5368709120L || Digest(File.ReadAllBytes(fileName)) != "160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba")
				{
					throw new Exception("unsupported managed BOT server");
				}
				ServerHandle = OpenProcess(1080u, inherit: false, process.Id);
				if (ServerHandle == IntPtr.Zero)
				{
					throw new Exception("BOT server attach failed");
				}
				ServerProcess = process;
				return true;
			}
		}
		return false;
	}

	private static void SetServerTeam(bool enabled)
	{
		IntPtr handle = Handle;
		try
		{
			if (!FindServer())
			{
				if (enabled)
				{
					throw new Exception("请先启动本机 BOT 服务端");
				}
				return;
			}
			Handle = ServerHandle;
			if (enabled && ServerEnabled == null)
			{
				int extension = Int(5408440312L);
				byte[] array = Read(5371037197L, 5);
				ServerEnabled = (Same(array, ServerOriginal) ? PrepareTeamCheck(extension, 5371037197L) : AssistTeamPatch.Recover(5371037197L, array, extension, Read));
			}
			UpdateSite(5371037197L, ServerOriginal, ServerEnabled, enabled);
			ServerApplied = enabled;
		}
		finally
		{
			Handle = handle;
		}
	}

	private static bool TeamApplied()
	{
		for (int i = 0; i < Sites.Length; i++)
		{
			if (!Same(Read(Sites[i], 5), Next(i)))
			{
				return false;
			}
		}
		return true;
	}

	private static void SetTeam(bool enabled)
	{
		if (enabled)
		{
			PrepareMapCheck();
		}
		for (int i = 0; i < Sites.Length; i++)
		{
			byte[] a = Read(Sites[i], 5);
			if (!Same(a, Original[i]) && (Next(i) == null || !Same(a, Next(i))))
			{
				throw new Exception("unsupported deploy signature");
			}
		}
		for (int j = 0; j < Sites.Length; j++)
		{
			byte[] b = (enabled ? Next(j) : Original[j]);
			if (Same(Read(Sites[j], 5), b))
			{
				continue;
			}
			if (!VirtualProtectEx(Handle, new IntPtr(Sites[j]), (UIntPtr)5u, 64u, out var old))
			{
				throw new Exception("cannot protect deploy site");
			}
			try
			{
				if (!WriteProcessMemory(Handle, new IntPtr(Sites[j]), b, (UIntPtr)5u, out var wrote) || wrote.ToUInt64() != 5)
				{
					throw new Exception("cannot update deploy site");
				}
				FlushInstructionCache(Handle, new IntPtr(Sites[j]), (UIntPtr)5u);
			}
			finally
			{
				VirtualProtectEx(Handle, new IntPtr(Sites[j]), (UIntPtr)5u, old, out var _);
			}
			if (!Same(Read(Sites[j], 5), b))
			{
				throw new Exception("deploy readback mismatch");
			}
		}
	}

	private static bool VisionApplied()
	{
		for (int i = 0; i < AssistVisionPatch.Sites.Length; i++)
		{
			if (!Same(Read(AssistVisionPatch.Sites[i], 6), AssistVisionPatch.Enabled[i]))
			{
				return false;
			}
		}
		return true;
	}

	private static void SetVision(bool enabled)
	{
		if (!Same(Read(5370823886L, 7), new byte[7] { 128, 184, 203, 0, 0, 0, 0 }))
		{
			throw new Exception("unsupported spotting field signature");
		}
		for (int i = 0; i < AssistVisionPatch.Sites.Length; i++)
		{
			AssistVisionPatch.Next(i, Read(AssistVisionPatch.Sites[i], 6), enabled);
		}
		for (int j = 0; j < AssistVisionPatch.Sites.Length; j++)
		{
			long num = AssistVisionPatch.Sites[j];
			byte[] array = Read(num, 6);
			byte[] b = AssistVisionPatch.Next(j, array, enabled);
			if (Same(array, b))
			{
				continue;
			}
			if (!VirtualProtectEx(Handle, new IntPtr(num), (UIntPtr)6u, 64u, out var old))
			{
				throw new Exception("cannot protect spotting code");
			}
			try
			{
				if (!WriteProcessMemory(Handle, new IntPtr(num), b, (UIntPtr)6u, out var wrote) || wrote.ToUInt64() != 6)
				{
					throw new Exception("cannot update spotting code");
				}
				FlushInstructionCache(Handle, new IntPtr(num), (UIntPtr)6u);
			}
			finally
			{
				VirtualProtectEx(Handle, new IntPtr(num), (UIntPtr)6u, old, out var _);
			}
			if (!Same(Read(num, 6), b))
			{
				throw new Exception("spotting code readback mismatch");
			}
		}
	}

	public static int Main(string[] args)
	{
		Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
		if (args.Length != 3)
		{
			Console.Error.WriteLine("Expected settings file, owned session and parent PID");
			return 2;
		}
		Process processById;
		try
		{
			string directoryName = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(args[0])));
			Dictionary<string, object> dictionary = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(directoryName, "settings", "client-game.json")));
			ExpectedPath = Path.GetFullPath(Path.Combine((string)dictionary["directory"], "bf4_no_drm.exe"));
			ServerPath = Path.Combine(directoryName, "runtime", "GameServer", "bf4_Server_Final.exe");
			OwnerPath = Path.Combine(directoryName, "node", "node.exe");
			processById = Process.GetProcessById(int.Parse(args[2]));
			if (!string.Equals(processById.MainModule.FileName, OwnerPath, StringComparison.OrdinalIgnoreCase))
			{
				throw new Exception("unsupported owner");
			}
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex.Message);
			return 3;
		}
		string path = args[0];
		string session = args[1];
		Process process = null;
		bool previouslyDown = false;
		bool flag = false;
		bool flag2 = false;
		long num = 0L;
		long num2 = 0L;
		long num3 = 0L;
		long num4 = 0L;
		long num5 = 0L;
		string revision = "";
		string skipKey = "MouseRight";
		string text = "";
		int num6 = 2;
		bool flag3 = false;
		bool flag4 = false;
		bool flag5 = false;
		string text2 = "";
		while (true)
		{
			try
			{
				long num7;
				while (true)
				{
					if (processById.HasExited)
					{
						if (ServerApplied)
						{
							SetServerTeam(enabled: false);
						}
						if (ServerHandle != IntPtr.Zero)
						{
							CloseHandle(ServerHandle);
						}
						if (Handle != IntPtr.Zero)
						{
							CloseHandle(Handle);
						}
						return 0;
					}
					num7 = Now();
					if (num7 - num4 >= 250)
					{
						num4 = num7;
						try
						{
							byte[] bytes = File.ReadAllBytes(path);
							Dictionary<string, object> dictionary2 = Json.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(bytes).TrimStart('\ufeff'));
							flag5 = dictionary2.ContainsKey("autoSpot") && (bool)dictionary2["autoSpot"];
							flag3 = false;
							flag4 = (bool)dictionary2["teamSpawn"];
							skipKey = (string)dictionary2["skipKey"];
							num6 = Convert.ToInt32(dictionary2["virtualKey"]);
							if (num6 < 1 || num6 > 255)
							{
								throw new Exception("unsupported key");
							}
							revision = (string)dictionary2["revision"];
						}
						catch (Exception)
						{
							flag3 = false;
							flag4 = false;
							flag5 = false;
							text2 = "辅助运行配置不可用";
						}
					}
					if (process != null && !process.HasExited)
					{
						break;
					}
					if (Handle != IntPtr.Zero)
					{
						CloseHandle(Handle);
						Handle = IntPtr.Zero;
					}
					process = null;
					flag = false;
					flag2 = false;
					MapEnabled = null;
					num5 = 0L;
					text = "";
					Process[] processesByName = Process.GetProcessesByName("bf4_no_drm");
					foreach (Process process2 in processesByName)
					{
						try
						{
							if (!string.Equals(process2.MainModule.FileName, ExpectedPath, StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}
							if (process2.MainModule.BaseAddress.ToInt64() != 5368709120L)
							{
								throw new Exception("unsupported client base");
							}
							if (!V2RuntimeBuild.IsClientHash(Digest(File.ReadAllBytes(ExpectedPath))))
							{
								throw new Exception("unsupported client hash");
							}
							process = process2;
							Handle = OpenProcess(1080u, inherit: false, process2.Id);
							if (Handle == IntPtr.Zero)
							{
								throw new Exception("client attach failed");
							}
							if (!Same(Read(Sites[2], 5), Original[2]))
							{
								PrepareMapCheck();
								flag = TeamApplied();
							}
							flag2 = VisionApplied();
							num = num7;
							previouslyDown = (GetAsyncKeyState(num6) & 0x8000) != 0;
							break;
						}
						catch (Exception ex3)
						{
							text2 = ex3.Message;
							process = null;
							if (Handle != IntPtr.Zero)
							{
								CloseHandle(Handle);
								Handle = IntPtr.Zero;
							}
						}
					}
					if (process != null)
					{
						break;
					}
					if (ServerApplied)
					{
						SetServerTeam(enabled: false);
					}
					if (num7 - num2 >= 1000)
					{
						num2 = num7;
						Emit(new
						{
							kind = "status",
							ready = false,
							pid = Process.GetCurrentProcess().Id,
							session = session,
							at = num7,
							message = ((text2.Length > 0) ? text2 : "请使用辅助版入口启动 BF4")
						});
					}
					Thread.Sleep(500);
				}
				long num8 = Ptr(5409017216L);
				long num9 = ((num8 == 0) ? 0 : Ptr(num8 + 96));
				long num10 = ((num9 == 0) ? 0 : Ptr(num9 + 1344));
				int num11 = ((num10 != 0) ? Int(num10 + 5068) : 0);
				if (ServerProcess != null && ServerProcess.HasExited)
				{
					ServerApplied = false;
					ServerEnabled = null;
				}
				if (flag != flag4 && num11 != 0)
				{
					text = "全队友部署的客户端尚未加载，请退出 BF4 后重新启动；无需退出 EA";
				}
				if (num5 == 0 || num7 - num5 >= 1000)
				{
					num5 = num7;
					if (flag != flag4 && num11 == 0)
					{
						try
						{
							SetTeam(flag4);
							flag = TeamApplied();
						}
						catch (Exception ex4)
						{
							text = "部署客户端加载失败：" + ex4.Message;
						}
					}
					if (flag == flag4 && ServerApplied != flag4)
					{
						try
						{
							SetServerTeam(flag4);
						}
						catch (Exception ex5)
						{
							text = "部署服务端等待恢复：" + ex5.Message;
						}
					}
					if (flag == flag4 && ServerApplied == flag4)
					{
						text = "";
					}
				}
				if (num7 - num >= 1500 && num11 == 0 && flag2 != flag5)
				{
					SetVision(flag5);
					flag2 = VisionApplied();
				}
				long num12 = ((num10 == 0) ? 0 : Ptr(num10 + 5328));
				float num13 = 100f;
				bool dead = false;
				if (num11 > 0 && num12 != 0)
				{
					long num14 = Ptr(num12 + 320);
					if (num14 != 0)
					{
						num13 = Float(num14 + 32);
						dead = !float.IsNaN(num13) && num13 <= 0f && num13 > -10000f;
					}
				}
				GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
				bool flag6 = (GetAsyncKeyState(num6) & 0x8000) != 0;
				if (AssistPolicy.SkipPressed(flag3, pid == (uint)process.Id, flag6, previouslyDown, dead) && num7 - num3 >= 1000)
				{
					num3 = num7;
					string @string = Encoding.UTF8.GetString(Read(Ptr(num10 + 24), 32));
					char[] separator = new char[1];
					string text3 = @string.Split(separator)[0];
					if (text3.Length > 0)
					{
						Emit(new
						{
							kind = "skip",
							gamePid = process.Id,
							name = text3,
							at = num7,
							session = session
						});
					}
				}
				previouslyDown = flag6;
				if (num7 - num2 >= 1000)
				{
					num2 = num7;
					bool flag7 = flag && ServerApplied;
					Emit(new
					{
						kind = "status",
						ready = true,
						pid = Process.GetCurrentProcess().Id,
						gamePid = process.Id,
						serverPid = ((ServerProcess != null) ? ServerProcess.Id : 0),
						session = session,
						at = num7,
						revision = revision,
						skipDeath = flag3,
						teamSpawn = flag7,
						teamClientApplied = flag,
						teamServerApplied = ServerApplied,
						teamMessage = text,
						autoSpot = flag2,
						visionRestartRequired = (flag2 != flag5),
						skipKey = skipKey,
						teamRestartRequired = (flag7 != flag4),
						health = num13,
						dead = dead,
						message = ((text.Length > 0) ? text : "辅助模块已载入，游戏效果待实测")
					});
				}
			}
			catch (Exception ex6)
			{
				if (Now() - num2 >= 1000)
				{
					num2 = Now();
					Emit(new
					{
						kind = "status",
						ready = false,
						pid = Process.GetCurrentProcess().Id,
						session = session,
						at = Now(),
						message = ex6.Message
					});
				}
				if (process != null && process.HasExited)
				{
					process = null;
				}
			}
			Thread.Sleep(40);
		}
	}
}
public static class AssistPolicy
{
	public static bool SkipPressed(bool enabled, bool foreground, bool down, bool previouslyDown, bool dead)
	{
		if (enabled && foreground && down && !previouslyDown)
		{
			return dead;
		}
		return false;
	}
}
public static class AssistTeamPatch
{
	public static byte[] Code(int extensionOffset)
	{
		byte[] array = new byte[31]
		{
			184, 0, 0, 0, 0, 72, 43, 200, 139, 129,
			204, 19, 0, 0, 133, 192, 126, 10, 59, 130,
			204, 19, 0, 0, 15, 148, 192, 195, 51, 192,
			195
		};
		Array.Copy(BitConverter.GetBytes(extensionOffset), 0, array, 1, 4);
		return array;
	}

	public static byte[] Recover(long site, byte[] call, int extension, Func<long, int, byte[]> read)
	{
		if (call == null || call.Length != 5 || call[0] != 232)
		{
			throw new InvalidOperationException("unsupported deploy call");
		}
		long num = site + 5 + BitConverter.ToInt32(call, 1);
		if (num < 5435817984L || num >= 7516192768L || (num & 0xFFFF) != 0)
		{
			throw new InvalidOperationException("foreign deploy bridge");
		}
		byte[] array = Code(extension);
		byte[] array2 = read(num, array.Length);
		if (array2 == null || array2.Length != array.Length)
		{
			throw new InvalidOperationException("unreadable deploy bridge");
		}
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] != array2[i])
			{
				throw new InvalidOperationException("foreign deploy payload");
			}
		}
		return (byte[])call.Clone();
	}

	public static byte[] Call(long site, long target)
	{
		long num = target - site - 5;
		if (num < int.MinValue || num > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException("target");
		}
		byte[] array = new byte[5] { 232, 0, 0, 0, 0 };
		Array.Copy(BitConverter.GetBytes((int)num), 0, array, 1, 4);
		return array;
	}
}
public static class AssistVisionPatch
{
	public static readonly long[] Sites = new long[2] { 5370823893L, 5370820738L };

	public static readonly byte[][] Original = new byte[2][]
	{
		new byte[6] { 15, 132, 190, 1, 0, 0 },
		new byte[6] { 65, 184, 2, 0, 0, 0 }
	};

	public static readonly byte[][] Enabled = new byte[2][]
	{
		new byte[6] { 144, 144, 144, 144, 144, 144 },
		new byte[6] { 65, 184, 1, 0, 0, 0 }
	};

	public static byte[] Next(int index, byte[] current, bool enabled)
	{
		string text = BitConverter.ToString(current);
		if (text != BitConverter.ToString(Original[index]) && text != BitConverter.ToString(Enabled[index]))
		{
			throw new InvalidOperationException("unsupported spotting signature");
		}
		return (byte[])(enabled ? Enabled[index] : Original[index]).Clone();
	}
}
