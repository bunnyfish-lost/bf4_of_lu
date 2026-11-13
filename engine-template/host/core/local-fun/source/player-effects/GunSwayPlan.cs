using System;
using System.Collections.Generic;

public static class GunSwayPlan
{
	public const int Size = 1104;

	public static readonly int[] Spread = SpreadFields();

	public static readonly int[] Recoil = RecoilFields();

	private static int[] SpreadFields()
	{
		List<int> list = new List<int>();
		int[] array = new int[26]
		{
			16, 40, 64, 88, 112, 136, 204, 228, 252, 276,
			300, 324, 392, 416, 484, 508, 576, 600, 668, 692,
			760, 788, 816, 844, 872, 900
		};
		foreach (int num in array)
		{
			for (int j = 0; j < 3; j++)
			{
				list.Add(num + j * 4);
			}
		}
		array = new int[6] { 928, 944, 960, 996, 1012, 1028 };
		foreach (int num in array)
		{
			for (int j = 0; j < 3; j++)
			{
				list.Add(num + j * 4);
			}
		}
		list.AddRange(new int[4] { 1072, 1076, 1080, 1084 });
		return list.ToArray();
	}

	private static int[] RecoilFields()
	{
		List<int> list = new List<int>();
		int[] array = new int[6] { 160, 348, 440, 532, 624, 716 };
		foreach (int num in array)
		{
			for (int j = 0; j < 5; j++)
			{
				list.Add(num + j * 4);
			}
		}
		list.Add(1092);
		return list.ToArray();
	}

	public static byte[] Create(byte[] original, bool recoil, bool spread)
	{
		if (original == null || original.Length != 1104)
		{
			throw new Exception("Unexpected GunSwayData size");
		}
		byte[] array = (byte[])original.Clone();
		foreach (int item in Fields(recoil, spread))
		{
			float f = BitConverter.ToSingle(original, item);
			if (float.IsNaN(f) || float.IsInfinity(f))
			{
				throw new Exception("Unexpected weapon dispersion value");
			}
			Array.Clear(array, item, 4);
		}
		return array;
	}

	public static IEnumerable<int> Fields(bool recoil, bool spread)
	{
		if (recoil)
		{
			try
			{
				int[] recoil2 = Recoil;
				for (int i = 0; i < recoil2.Length; i++)
				{
					yield return recoil2[i];
				}
			}
			finally
			{
			}
		}
		if (!spread)
		{
			yield break;
		}
		try
		{
			int[] spread2 = Spread;
			for (int j = 0; j < spread2.Length; j++)
			{
				yield return spread2[j];
			}
		}
		finally
		{
		}
	}
}
