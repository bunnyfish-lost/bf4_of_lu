internal sealed class CameraToggleState
{
	private bool held;

	private bool chat;

	private bool console;

	public int Key(int vk, bool down, bool focus, bool armed, bool modifiers)
	{
		if (!focus)
		{
			held = vk == 67 && down;
			return 0;
		}
		if (vk == 13 || vk == 27)
		{
			if (down)
			{
				chat = (console = false);
			}
			return 0;
		}
		if (down && (vk == 74 || vk == 75 || vk == 76) && !console)
		{
			chat = true;
		}
		if (down && vk == 192)
		{
			console = !console;
		}
		if (vk != 67)
		{
			return 0;
		}
		bool flag = down && !held;
		held = down;
		if (!armed || chat || console || modifiers)
		{
			return 0;
		}
		return (!flag) ? 1 : 2;
	}
}
