internal static class FlightKeyMath
{
	public static bool Moving(bool forward, bool backward, bool left, bool right)
	{
		return forward != backward || left != right;
	}
}
