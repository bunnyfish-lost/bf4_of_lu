using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

internal class PlayerEffects
{
	private sealed class Request
	{
		public long at;

		public string id;

		public float multiplier = 1f;

		public bool noRecoil;

		public bool noSpread;

		public bool noBulletDrop;

		public bool fastBullets;

		public bool flight;

		public bool thirdPerson;

		public bool cameraHotkey;

		public float cameraDistance = 3f;

		public float cameraHeight = 0.6f;
	}

	private sealed class Record
	{
		public string role;

		public string path;

		public string startTicks;

		public int pid;
	}

	private sealed class Records
	{
		public Record[] processes;
	}

	private sealed class Settings
	{
		public string gameServerDirectory;
	}

	private sealed class RosterPlayer
	{
		public string name;

		public string guid;
	}

	private sealed class Roster
	{
		public long at;

		public bool ownedServer;

		public RosterPlayer[] players;
	}

	private sealed class Track
	{
		public Mem M;

		public long Soldier;

		public long Field;

		public float Original;

		public float Written;

		public Func<long> Current;

		public void Restore()
		{
			if (Field == 0)
			{
				return;
			}
			try
			{
				if (Current() == Soldier && M.F(Field) == Written)
				{
					M.W(Field, Original);
				}
			}
			catch
			{
			}
			Field = 0L;
		}

		public void Set(float multiplier, long offset)
		{
			long num = Current();
			if (num != Soldier)
			{
				Restore();
				Soldier = num;
			}
			if (!Mem.Ptr(num))
			{
				Restore();
				return;
			}
			if (multiplier == 1f)
			{
				Restore();
				return;
			}
			if (Field == 0)
			{
				float num2 = M.F(num + offset);
				if (num2 < 0.1f || num2 > 3f || float.IsNaN(num2))
				{
					throw new Exception("Unexpected original multiplier");
				}
				Field = num + offset;
				Original = num2;
			}
			if (Current() != Soldier)
			{
				Restore();
				return;
			}
			Written = Original * multiplier;
			M.W(Field, Written);
		}
	}

	private sealed class Patch
	{
		public Mem M;

		public long Context;

		public long Soldier;

		public long Weapon;

		public long Firing;

		public long Sway;

		public long Data;

		public long Clone;

		public byte[] Baseline;

		public bool IsServer;

		private byte[] Applied;

		private bool spreadWasActive;

		private readonly HashSet<long> copies = new HashSet<long>();

		private static readonly int[] ClientSources = new int[7] { 8, 544, 560, 576, 584, 600, 616 };

		private static readonly int[] ServerSources = new int[7] { 8, 568, 584, 600, 608, 624, 640 };

		private static readonly int[] ClientSelections = new int[5] { 536, 552, 568, 592, 608 };

		private static readonly int[] ServerSelections = new int[5] { 560, 576, 592, 616, 632 };

		private int[] Sources => IsServer ? ServerSources : ClientSources;

		private int[] Selections => IsServer ? ServerSelections : ClientSelections;

		private long ContextSlot => M.Base + (IsServer ? 46563656 : 40308096);

		private long Accumulated => Sway + (IsServer ? 456 : 448);

		private bool Linked()
		{
			try
			{
				return M.Q(ContextSlot) == Context && M.Q(Weapon + (IsServer ? 512 : 18880)) == Firing && M.Q(Firing + (IsServer ? 72 : 120)) == Sway;
			}
			catch
			{
				return false;
			}
		}

		private bool Definition()
		{
			return Baseline != null && M.Q(Data) == BitConverter.ToInt64(Baseline, 0);
		}

		private bool Inside(long pointer, long source)
		{
			return pointer >= source && pointer < source + 1104;
		}

		private long OriginalSelection(long pointer)
		{
			foreach (long copy in copies)
			{
				if (Inside(pointer, copy))
				{
					return Data + (pointer - copy);
				}
			}
			return pointer;
		}

		public bool Valid()
		{
			try
			{
				return Linked() && Definition() && Sources.All(delegate(int off)
				{
					long num = M.Q(Sway + off);
					return num == Data || copies.Contains(num);
				});
			}
			catch
			{
				return false;
			}
		}

		public void Restore()
		{
			if (Clone == 0)
			{
				return;
			}
			if (!Linked() || !Definition())
			{
				Clone = 0L;
				Applied = null;
				copies.Clear();
				spreadWasActive = false;
				return;
			}
			Exception ex = null;
			int[] selections = Selections;
			foreach (int num in selections)
			{
				try
				{
					long num2 = M.Q(Sway + num);
					long num3 = OriginalSelection(num2);
					if (num3 != num2)
					{
						M.Write(Sway + num, BitConverter.GetBytes(num3));
					}
				}
				catch (Exception ex2)
				{
					ex = ex2;
				}
			}
			selections = Sources;
			foreach (int num in selections)
			{
				try
				{
					if (copies.Contains(M.Q(Sway + num)))
					{
						M.Write(Sway + num, BitConverter.GetBytes(Data));
					}
				}
				catch (Exception ex2)
				{
					ex = ex2;
				}
			}
			if (ex != null)
			{
				throw new Exception("Cannot restore private weapon bindings", ex);
			}
			Clone = 0L;
			Applied = null;
			copies.Clear();
			spreadWasActive = false;
		}

		public bool IsApplied(bool recoil, bool spread)
		{
			try
			{
				if (Clone == 0 || !Valid() || !Sources.All((int off) => M.Q(Sway + off) == Clone) || !M.R(Clone, 1104).SequenceEqual(GunSwayPlan.Create(Baseline, recoil, spread)))
				{
					return false;
				}
				int[] selections = Selections;
				foreach (int num in selections)
				{
					if (Inside(M.Q(Sway + num), Data))
					{
						return false;
					}
				}
				if (spread)
				{
					float num2 = M.F(Accumulated);
					if (float.IsNaN(num2) || float.IsInfinity(num2) || Math.Abs(num2) > 1E-06f)
					{
						return false;
					}
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		public void Apply(bool recoil, bool spread)
		{
			if (!recoil && !spread)
			{
				Restore();
				return;
			}
			if (!Valid())
			{
				throw new Exception("Weapon instance or cached settings changed");
			}
			byte[] array = GunSwayPlan.Create(Baseline, recoil, spread);
			if (Applied != null && Applied.SequenceEqual(array) && IsApplied(recoil, spread))
			{
				return;
			}
			long num = M.Snapshot(array);
			if (!Valid())
			{
				throw new Exception("Weapon changed before applying private settings");
			}
			copies.Add(num);
			Clone = num;
			int[] sources = Sources;
			foreach (int num2 in sources)
			{
				if (M.Q(Sway + num2) != num)
				{
					M.Write(Sway + num2, BitConverter.GetBytes(num));
				}
			}
			sources = Selections;
			foreach (int num2 in sources)
			{
				long num3 = M.Q(Sway + num2);
				long num4 = OriginalSelection(num3);
				if (Inside(num4, Data))
				{
					long num5 = num + (num4 - Data);
					if (num3 != num5)
					{
						M.Write(Sway + num2, BitConverter.GetBytes(num5));
					}
				}
			}
			if (spread && !spreadWasActive)
			{
				float num6 = M.F(Accumulated);
				if (float.IsNaN(num6) || float.IsInfinity(num6) || Math.Abs(num6) > 100f)
				{
					throw new Exception("Unexpected accumulated dispersion");
				}
				if (num6 != 0f)
				{
					M.W(Accumulated, 0f);
				}
			}
			Applied = array;
			spreadWasActive = spread;
			if (!Valid() || !Sources.All((int off) => M.Q(Sway + off) == Clone))
			{
				throw new Exception("Private weapon bindings were not confirmed");
			}
		}
	}

	private static string Dir = AppDomain.CurrentDomain.BaseDirectory;

	private static string Root = Path.GetFullPath(Path.Combine(Dir, "../.."));

	private static JavaScriptSerializer Json = new JavaScriptSerializer();

	private static T Read<T>(string file)
	{
		return Json.Deserialize<T>(File.ReadAllText(file));
	}

	private static void Status(object value)
	{
		string text = Path.Combine(Dir, "player-effects-status.json");
		string text2 = text + ".tmp";
		File.WriteAllText(text2, Json.Serialize(value));
		if (File.Exists(text))
		{
			File.Replace(text2, text, null);
		}
		else
		{
			File.Move(text2, text);
		}
	}

	private static bool Valid(Request r, long now)
	{
		return r != null && r.at <= now && now - r.at < 4000 && r.cameraHeight >= 0f && r.cameraHeight <= 2f && r.cameraDistance >= 1f && r.cameraDistance <= 10f && r.multiplier >= 0f && r.multiplier <= 5f && Math.Abs((double)(r.multiplier * 10f) - Math.Round(r.multiplier * 10f)) < 0.0001;
	}

	private static string Identity(Mem cm, Roster roster, long now)
	{
		if (roster == null || !roster.ownedServer || roster.at > now || now - roster.at > 4000 || roster.players == null)
		{
			return "";
		}
		long num = cm.Q(cm.Base + 40308096);
		long num2 = cm.Q(num + 96);
		long num3 = cm.Q(num2 + 1344);
		string name = cm.S(num3 + 64, 64);
		return roster.players.Any((RosterPlayer x) => x.name == name && !string.IsNullOrEmpty(x.guid)) ? name : "";
	}

	private static long Client(Mem m, string name, bool living = true)
	{
		long num = m.Q(m.Base + 40308096);
		long num2 = m.Q(num + 96);
		long num3 = m.Q(num2 + 1344);
		if (m.S(num3 + 64, 64) != name || m.B(num3 + 5065) != 0 || m.Q(num3 + 5312) != 0)
		{
			return 0L;
		}
		long num4 = m.Q(num3 + 5328);
		if (!Mem.Ptr(num4) || (living && m.F(m.Q(num4 + 320) + 32) <= 0f))
		{
			return 0L;
		}
		return num4;
	}

	private static long Server(Mem m, string name, bool living = true)
	{
		long num = m.Q(m.Base + 46563656);
		long num2 = m.Q(num + 96);
		long num3 = m.Q(num2 + 2344);
		int num4 = m.I(num2 + 16);
		if (num4 < 1 || num4 > 70)
		{
			return 0L;
		}
		for (int i = 0; i < num4; i++)
		{
			long num5 = m.Q(num3 + i * 8);
			if (Mem.Ptr(num5) && !(m.S(num5 + 64, 64) != name))
			{
				long num6 = m.Q(num5 + 5280);
				if (Mem.Ptr(num6) && m.Q(num6 - 16) == num5)
				{
					return 0L;
				}
				if (m.B(num5 + 5065) != 0)
				{
					return 0L;
				}
				long num7 = m.Q(num5 + 5288);
				if (!Mem.Ptr(num7) || (living && m.F(m.Q(num7 + 496) + 32) <= 0f))
				{
					return 0L;
				}
				return num7;
			}
		}
		return 0L;
	}

	private static Patch Find(Mem m, string name)
	{
		long num = m.Q(m.Base + 40308096);
		long num2 = m.Q(num + 96);
		long num3 = m.Q(num2 + 1344);
		if (m.S(num3 + 64, 64) != name || m.B(num3 + 5065) != 0 || m.Q(num3 + 5312) != 0)
		{
			return null;
		}
		long num4 = m.Q(num3 + 5328);
		if (!Mem.Ptr(num4) || m.F(m.Q(num4 + 320) + 32) <= 0f)
		{
			return null;
		}
		long num5 = m.Q(num4 + 1392);
		int num6 = m.I(num5 + 2712);
		if (num6 < 0 || num6 > 1)
		{
			return null;
		}
		long num7 = m.Q(num5 + 2192);
		long num8 = m.Q(num7 + num6 * 8);
		long num9 = m.Q(num8 + 18880);
		long num10 = m.Q(num9 + 120);
		long num11 = m.Q(num10 + 8);
		if (m.Q(num11) != m.Base + 31961200)
		{
			return null;
		}
		Patch patch = new Patch();
		patch.M = m;
		patch.Context = num;
		patch.Soldier = num4;
		patch.Weapon = num8;
		patch.Firing = num9;
		patch.Sway = num10;
		patch.Data = num11;
		return patch;
	}

	private static Patch FindServer(Mem m, long s)
	{
		if (!Mem.Ptr(s))
		{
			return null;
		}
		long context = m.Q(m.Base + 46563656);
		long num = m.Q(s + 2032);
		long num2 = m.Q(num + 2336);
		int num3 = m.I(num2 + 8);
		if (num3 < 0 || num3 > 1)
		{
			return null;
		}
		long num4 = m.Q(num + 2064);
		long num5 = m.Q(num + 2072);
		if (num5 < num4 || num5 - num4 > 1024 || num4 + num3 * 8 >= num5)
		{
			return null;
		}
		long num6 = m.Q(num4 + num3 * 8);
		long num7 = m.Q(num6 + 512);
		if (m.Q(num7) != m.Base + 33267104)
		{
			return null;
		}
		long num8 = m.Q(num7 + 72);
		long num9 = m.Q(num8 + 8);
		if (m.Q(num8) != m.Base + 30844272 || m.Q(num9) != m.Base + 34758408)
		{
			return null;
		}
		Patch patch = new Patch();
		patch.M = m;
		patch.Context = context;
		patch.Soldier = s;
		patch.Weapon = num6;
		patch.Firing = num7;
		patch.Sway = num8;
		patch.Data = num9;
		patch.IsServer = true;
		return patch;
	}

	private static string RestoreWeapons(Patch client, Patch server)
	{
		List<string> list = new List<string>();
		Patch[] array = new Patch[2] { client, server };
		foreach (Patch patch in array)
		{
			if (patch != null)
			{
				try
				{
					patch.Restore();
				}
				catch (Exception ex)
				{
					list.Add((patch.IsServer ? "server: " : "client: ") + ex.Message);
				}
			}
		}
		return (list.Count == 0) ? null : string.Join("; ", list.ToArray());
	}

	private static void Update(ref Patch active, Patch next, bool recoil, bool spread)
	{
		if (active != null && (next == null || active.Weapon != next.Weapon || active.Firing != next.Firing || active.Sway != next.Sway || active.Context != next.Context || active.Soldier != next.Soldier))
		{
			active.Restore();
			active = null;
		}
		if (next != null)
		{
			if (active == null)
			{
				next.Baseline = next.M.R(next.Data, 1104);
				active = next;
			}
			active.Apply(recoil, spread);
		}
	}

	public static int Main()
	{
		using Mutex mutex = new Mutex(initiallyOwned: false, "Local/BF4PlayerEffects-" + Root.GetHashCode().ToString("x"));
		if (!mutex.WaitOne(0))
		{
			return 0;
		}
		try
		{
			Run();
			return 0;
		}
		catch (Exception ex)
		{
			try
			{
				Status(new
				{
					at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
					running = false,
					error = ex.Message
				});
			}
			catch
			{
			}
			return 1;
		}
		finally
		{
			mutex.ReleaseMutex();
		}
	}

	private static void Run()
	{
		long num = 0L;
		while (true)
		{
			bool flag = true;
			long num2 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			Request request = null;
			try
			{
				request = Read<Request>(Path.Combine(Dir, "player-effects-request.json"));
			}
			catch
			{
			}
			if (!Valid(request, num2))
			{
				if (num == 0)
				{
					num = num2;
				}
				Status(new
				{
					at = num2,
					running = true,
					active = false,
					status = "已关闭",
					requestId = request?.id
				});
				if (num2 - num > 8000)
				{
					break;
				}
				Thread.Sleep(250);
				continue;
			}
			num = 0L;
			if (request.multiplier == 1f && !request.noRecoil && !request.noSpread && !request.noBulletDrop && !request.fastBullets && !request.flight && !request.thirdPerson && !request.cameraHotkey)
			{
				Status(new
				{
					at = num2,
					running = true,
					active = false,
					status = "默认参数",
					requestId = request.id
				});
				Thread.Sleep(250);
				continue;
			}
			try
			{
				Records records = Read<Records>(Path.Combine(Root, "runtime/processes.json"));
				Record record = records.processes.Single((Record x) => x.role == "game");
				string fullPath = Path.GetFullPath(Path.Combine(Root, "runtime/GameServer/bf4_Server_Final.exe"));
				Process processById = Process.GetProcessById(record.pid);
				if (!string.Equals(Path.GetFullPath(record.path), fullPath, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetFullPath(processById.MainModule.FileName), fullPath, StringComparison.OrdinalIgnoreCase) || processById.StartTime.ToUniversalTime().Ticks.ToString() != record.startTicks)
				{
					throw new Exception("游戏服进程记录已改变");
				}
				Process[] array = (from x in Process.GetProcesses()
					where x.ProcessName == "bf4_no_drm" || x.ProcessName == "bf4"
					select x).ToArray();
				if (array.Length != 1)
				{
					throw new Exception("等待一个本机游戏客户端");
				}
				Process process = array[0];
				if (!string.Equals(Path.GetFullPath(process.MainModule.FileName), Path.GetFullPath(Path.Combine(Root, "mods/client/bf4_no_drm.exe")), StringComparison.OrdinalIgnoreCase)) throw new Exception("等待本实例客户端");
				Mem cm = new Mem(process, V2RuntimeBuild.ClientHash);
				try
				{
					Mem sm = new Mem(processById, "160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba");
					try
					{
						string name = "";
						Track track = new Track();
						track.M = cm;
						track.Current = () => Client(cm, name, living: false);
						Track track2 = track;
						Track track3 = new Track();
						track3.M = sm;
						track3.Current = () => Server(sm, name, living: false);
						Track track4 = track3;
						Patch active = null;
						Patch active2 = null;
						long num3 = 0L;
						Roster roster = null;
						CameraDriver cameraDriver = new CameraDriver(cm);
						BallisticsDriver ballisticsDriver = new BallisticsDriver(cm);
						bool flag2 = false;
						FlightDriver flightDriver = new FlightDriver(cm, isServer: false);
						FlightDriver flightDriver2 = new FlightDriver(sm, isServer: true);
						FlightKeys flightKeys = null;
						CameraKeys cameraKeys = null;
						string text = null;
						bool flag3 = false;
						try
						{
							while (!process.HasExited && !processById.HasExited)
							{
								num2 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
								if (num2 >= num3)
								{
									num3 = num2 + 250;
									request = Read<Request>(Path.Combine(Dir, "player-effects-request.json"));
									roster = Read<Roster>(Path.Combine(Dir, "roster.json"));
								}
								if (!Valid(request, num2))
								{
									break;
								}
								string text2 = Identity(cm, roster, num2);
								if (text2 != name)
								{
									track2.Restore();
									track4.Restore();
									if (active != null)
									{
										active.Restore();
										active = null;
									}
									if (active2 != null)
									{
										active2.Restore();
										active2 = null;
									}
									name = text2;
								}
								long num4 = ((name.Length > 0) ? Client(cm, name) : 0);
								long num5 = ((name.Length > 0) ? Server(sm, name) : 0);
								bool flag4 = num4 != 0 && num5 != 0;
								float multiplier = (flag4 ? request.multiplier : 1f);
								if (text != request.id)
								{
									text = request.id;
									flag3 = request.thirdPerson;
								}
								if (request.cameraHotkey)
								{
									if (cameraKeys == null)
									{
										cameraKeys = new CameraKeys();
									}
									if (cameraKeys.Poll(process.Id, flag4))
									{
										flag3 = !flag3;
										string text3 = Path.Combine(Dir, "camera-toggle.json");
										string text4 = text3 + ".tmp";
										File.WriteAllText(text4, Json.Serialize(new
										{
											id = Guid.NewGuid().ToString(),
											requestId = request.id,
											at = num2,
											thirdPerson = flag3
										}));
										if (File.Exists(text3))
										{
											File.Replace(text4, text3, null);
										}
										else
										{
											File.Move(text4, text3);
										}
									}
								}
								else if (cameraKeys != null)
								{
									cameraKeys.Dispose();
									cameraKeys = null;
								}
								bool flag5 = flag3 && flag4 && CameraKeys.RearLook(process.Id);
								if (flag3 && flag4)
								{
									cameraDriver.Set(num4, request.cameraDistance, request.cameraHeight, flag5);
								}
								else
								{
									cameraDriver.Clear();
								}
								if (request.flight && flag4)
								{
									if (flightKeys == null)
									{
										flightKeys = new FlightKeys();
									}
									float vertical = (float)flightKeys.Axis(process.Id) * 4f * request.multiplier;
									bool brake = !flightKeys.Moving(process.Id);
									flightDriver.Set(num4, vertical, brake);
									flightDriver2.Set(num5, vertical, brake);
								}
								else
								{
									flightDriver.Clear();
									flightDriver2.Clear();
									if (flightKeys != null)
									{
										flightKeys.Dispose();
										flightKeys = null;
									}
								}
								flag2 = ballisticsDriver.Set(flag4 ? num4 : 0, request.noBulletDrop, request.fastBullets);
								track4.Set(multiplier, 1432L);
								track2.Set(multiplier, 904L);
								Patch next = ((flag4 && (request.noRecoil || request.noSpread)) ? Find(cm, name) : null);
								Update(ref active, next, request.noRecoil, request.noSpread);
								Patch next2 = ((flag4 && request.noSpread) ? FindServer(sm, num5) : null);
								Update(ref active2, next2, recoil: false, request.noSpread);
								if (num2 + 25 >= num3)
								{
									Status(new
									{
										at = num2,
										running = true,
										active = flag4,
										requestId = request.id,
										name = name,
										multiplier = ((track2.Field == 0) ? 1f : cm.F(track2.Field)),
										serverMultiplier = ((track4.Field == 0) ? 1f : sm.F(track4.Field)),
										noRecoil = (request.noRecoil && active != null && active.IsApplied(request.noRecoil, request.noSpread)),
										noSpread = (request.noSpread && active != null && active.IsApplied(request.noRecoil, request.noSpread) && active2 != null && active2.IsApplied(recoil: false, spread: true)),
										clientSpread = (request.noSpread && active != null && active.IsApplied(request.noRecoil, request.noSpread)),
										serverSpread = (request.noSpread && active2 != null && active2.IsApplied(recoil: false, spread: true)),
										noBulletDrop = (flag2 && request.noBulletDrop),
										fastBullets = (flag2 && request.fastBullets),
										bulletSpeed = ((!flag2) ? 0f : (request.fastBullets ? 100000f : ballisticsDriver.OriginalSpeed)),
										bulletGravity = ((!flag2) ? 0f : (request.noBulletDrop ? 0f : ballisticsDriver.OriginalGravity)),
										thirdPerson = (flag3 && flag4),
										cameraHotkey = request.cameraHotkey,
										rearLook = flag5,
										cameraDistance = request.cameraDistance,
										cameraHeight = request.cameraHeight,
										cameraCalls = cameraDriver.Calls,
										modelCalls = cameraDriver.ModelCalls,
										bodyCalls = cameraDriver.BodyCalls,
										flight = (request.flight && flag4),
										clientFlightCalls = flightDriver.Calls,
										serverFlightCalls = flightDriver2.Calls,
										clientFlightBrakeCalls = flightDriver.BrakeCalls,
										serverFlightBrakeCalls = flightDriver2.BrakeCalls,
										status = ((!flag4) ? "等待本机步兵部署" : ((request.noSpread && (active == null || !active.IsApplied(request.noRecoil, spread: true) || active2 == null || !active2.IsApplied(recoil: false, spread: true))) ? "等待玩家枪械散布参数（主武器或手枪）" : "已应用到本机玩家；AI参数不变"))
									});
								}
								Thread.Sleep(25);
							}
						}
						finally
						{
							ballisticsDriver.Clear();
							cameraKeys?.Dispose();
							cameraDriver.Clear();
							flightDriver.Clear();
							flightDriver2.Clear();
							flightKeys?.Dispose();
							track2.Restore();
							track4.Restore();
							string text5 = RestoreWeapons(active, active2);
							Status(new
							{
								at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
								running = true,
								active = false,
								error = text5,
								status = ((text5 == null) ? "已恢复默认参数" : ("武器参数恢复未完成：" + text5)),
								requestId = request?.id
							});
						}
					}
					finally
					{
						if (sm != null)
						{
							((IDisposable)sm).Dispose();
						}
					}
				}
				finally
				{
					if (cm != null)
					{
						((IDisposable)cm).Dispose();
					}
				}
			}
			catch (Exception ex)
			{
				Status(new
				{
					at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
					running = true,
					active = false,
					error = ex.Message,
					status = ex.Message,
					requestId = request?.id
				});
				Thread.Sleep(750);
			}
		}
	}
}
