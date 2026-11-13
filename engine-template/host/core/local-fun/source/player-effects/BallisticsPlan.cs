using System;

public sealed class BallisticsPlan
{
	public byte[] Firing;

	public byte[] Function;

	public byte[] Bullet;

	public float Speed;

	public float Gravity;

	private static float F(byte[] b, int off)
	{
		return BitConverter.ToSingle(b, off);
	}

	private static bool Fin(float x)
	{
		return !float.IsNaN(x) && !float.IsInfinity(x);
	}

	private static void Put(byte[] b, int off, float x)
	{
		Array.Copy(BitConverter.GetBytes(x), 0, b, off, 4);
	}

	public static BallisticsPlan Create(byte[] fd, byte[] fn, byte[] bullet, bool drop, bool fast)
	{
		if (fd.Length != 88 || fn.Length != 592 || bullet.Length != 368)
		{
			throw new Exception("Invalid bullet data size");
		}
		float num = F(fn, 128);
		float num2 = F(fn, 132);
		float num3 = F(fn, 136);
		float num4 = F(bullet, 304);
		float num5 = F(bullet, 144);
		float num6 = (float)Math.Sqrt(num * num + num2 * num2 + num3 * num3);
		if (!Fin(num6) || num6 < 100f || num6 > 3000f || !Fin(num4) || num4 > 0f || num4 < -100f || !Fin(num5) || num5 < 0f || num5 > 3000f)
		{
			throw new Exception("Unsupported firearm ballistics");
		}
		BallisticsPlan ballisticsPlan = new BallisticsPlan();
		ballisticsPlan.Firing = (byte[])fd.Clone();
		ballisticsPlan.Function = (byte[])fn.Clone();
		ballisticsPlan.Bullet = (byte[])bullet.Clone();
		ballisticsPlan.Speed = num6;
		ballisticsPlan.Gravity = num4;
		BallisticsPlan ballisticsPlan2 = ballisticsPlan;
		if (drop)
		{
			Put(ballisticsPlan2.Bullet, 304, 0f);
		}
		if (fast)
		{
			float num7 = 100000f / num6;
			Put(ballisticsPlan2.Function, 128, num * num7);
			Put(ballisticsPlan2.Function, 132, num2 * num7);
			Put(ballisticsPlan2.Function, 136, num3 * num7);
			Put(ballisticsPlan2.Bullet, 144, 100000f);
		}
		return ballisticsPlan2;
	}
}
