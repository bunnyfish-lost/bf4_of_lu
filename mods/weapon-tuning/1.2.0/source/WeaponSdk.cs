using System;using System.Linq;using System.Collections.Generic;
public static class WeaponSdk {
 public static Action<Mem,long,long,byte[]> RecordClone;
public sealed class Patch
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
        private long SwayType;

		private bool spreadWasActive;
 public void Adopt(IEnumerable<long> owned){foreach(long pointer in owned)copies.Add(pointer);}

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
			return Baseline != null && M.R(Data,GunSwayPlan.Size).SequenceEqual(Baseline);
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
			if (!Definition() || (SwayType != 0 && M.Q(Sway) != SwayType))
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
			if (SwayType == 0) SwayType=M.Q(Sway);
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
            if(RecordClone!=null)RecordClone(M,num,Data,Baseline);
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
public static Patch Find(Mem m, string name)
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
public static Patch FindServer(Mem m, long s)
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
public static long Server(Mem m, string name, bool living = true)
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
public static long Client(Mem m, string name, bool living = true)
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
}