using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Windows.Media;

[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: CompilationRelaxations(8)]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
internal static class Wire
{
	public static string Text(Dictionary<string, object> d, string k)
	{
		return d.ContainsKey(k) ? Convert.ToString(d[k]) : "";
	}

	public static string Json(object value)
	{
		return new JavaScriptSerializer().Serialize(value);
	}

	public static Dictionary<string, object> Read(string line)
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = 16384;
		return javaScriptSerializer.Deserialize<Dictionary<string, object>>(line);
	}

	public static void Output(object value)
	{
		Console.WriteLine(Json(value));
		Console.Out.Flush();
	}

	public static long Now()
	{
		return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
	}
}
internal sealed class Audio : IDisposable
{
	private readonly MediaPlayer media = new MediaPlayer();

	private readonly Action<string> state;

	private bool ready;

	private bool playAfterLoad;

	private long lastPlay;

	public Action Loaded;

	public Audio(Action<string> state)
	{
		Audio audio = this;
		this.state = state;
		MediaPlayer mediaPlayer = media;
		EventHandler value = delegate
		{
			if (!audio.media.NaturalDuration.HasTimeSpan || audio.media.NaturalDuration.TimeSpan.TotalSeconds > 8.0)
			{
				audio.ready = false;
				audio.media.Close();
				state("error:音效长度请控制在 8 秒以内");
			}
			else
			{
				audio.ready = true;
				state("ready");
				if (audio.Loaded != null)
				{
					audio.Loaded();
				}
				if (audio.playAfterLoad)
				{
					audio.playAfterLoad = false;
					audio.Play();
				}
			}
		};
		mediaPlayer.MediaOpened += value;
		media.MediaFailed += delegate(object s, ExceptionEventArgs e)
		{
			audio.ready = false;
			state("error:" + e.ErrorException.Message);
		};
	}

	public void Configure(string file, int volume, bool preview)
	{
		file = Path.GetFullPath(file);
		string text = Path.GetExtension(file).ToLowerInvariant();
		if ((text != ".wav" && text != ".mp3") || !File.Exists(file) || new FileInfo(file).Length > 4194304)
		{
			throw new Exception("请选择 4 MB 以内的 WAV 或 MP3 音频");
		}
		if (volume < 0 || volume > 100)
		{
			throw new Exception("音量需为 0～100");
		}
		ready = false;
		media.Stop();
		media.Close();
		media.Volume = (double)volume / 100.0;
		playAfterLoad = preview;
		media.Open(new Uri(file));
	}

	public void SetVolume(int value)
	{
		media.Volume = (double)Math.Max(0, Math.Min(100, value)) / 100.0;
	}

	public void Play()
	{
		if (ready && Wire.Now() - lastPlay >= 50)
		{
			lastPlay = Wire.Now();
			media.Position = TimeSpan.Zero;
			media.Play();
			state("played");
		}
	}

	public void Stop()
	{
		media.Stop();
	}

	public void Dispose()
	{
		media.Close();
	}
}
internal sealed class AudioBank : IDisposable
{
	private readonly Dictionary<string, Audio> clips = new Dictionary<string, Audio>();

	private readonly HashSet<string> seen = new HashSet<string>();

	private readonly Queue<string> order = new Queue<string>();

	private readonly Action<object> output;

	private int generation;

	private int pending;

	private bool ready;

	private bool failed;

	public AudioBank(Action<object> output)
	{
		this.output = output;
	}

	public void Configure(Dictionary<string, object> message)
	{
		object obj;
		if (!message.ContainsKey("files"))
		{
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			dictionary.Add("kill", Wire.Text(message, "file"));
			obj = dictionary;
		}
		else
		{
			obj = message["files"] as Dictionary<string, object>;
		}
		Dictionary<string, object> dictionary2 = (Dictionary<string, object>)obj;
		if (dictionary2 == null || !dictionary2.ContainsKey("kill") || dictionary2.Count > 12)
		{
			throw new Exception("音效包需要普通击杀音频，最多 12 个音频");
		}
		foreach (string key in dictionary2.Keys)
		{
			if (key != "kill" && key != "headKill" && key != "hit" && key != "headHit" && !Regex.IsMatch(key, "^streak[1-8]$"))
			{
				throw new Exception("音效类型无效");
			}
		}
		generation++;
		int g = generation;
		ready = false;
		failed = false;
		pending = dictionary2.Count;
		foreach (Audio value in clips.Values)
		{
			value.Dispose();
		}
		clips.Clear();
		try
		{
			foreach (KeyValuePair<string, object> item in dictionary2)
			{
				string cue = item.Key;
				Audio current2 = new Audio(delegate(string state)
				{
					if (g == generation)
					{
						if (state == "ready")
						{
							pending--;
							if (pending == 0 && !failed)
							{
								ready = true;
								output(new
								{
									type = "ready"
								});
							}
						}
						else if (state == "played")
						{
							output(new
							{
								type = "played",
								cue = cue
							});
						}
						else if (state.StartsWith("error:"))
						{
							failed = true;
							ready = false;
							output(new
							{
								type = "error",
								message = cue + ": " + state.Substring(6)
							});
						}
					}
				});
				clips.Add(cue, current2);
				current2.Configure(Convert.ToString(item.Value), Convert.ToInt32(message["volume"]), preview: false);
			}
		}
		catch
		{
			failed = true;
			ready = false;
			throw;
		}
	}

	public void Play(Dictionary<string, object> message)
	{
		string text = Wire.Text(message, "id");
		if (!ready || text.Length == 0 || text.Length > 160 || seen.Contains(text) || !long.TryParse(Wire.Text(message, "at"), out var result) || Math.Abs(Wire.Now() - result) > 3000)
		{
			return;
		}
		seen.Add(text);
		order.Enqueue(text);
		if (order.Count > 2048)
		{
			seen.Remove(order.Dequeue());
		}
		string text2 = Wire.Text(message, "cue");
		if (!clips.ContainsKey(text2))
		{
			text2 = "kill";
		}
		foreach (KeyValuePair<string, Audio> clip in clips)
		{
			if (clip.Key != text2)
			{
				clip.Value.Stop();
			}
		}
		clips[text2].Play();
	}

	public void Dispose()
	{
		generation++;
		foreach (Audio value in clips.Values)
		{
			value.Dispose();
		}
		clips.Clear();
	}
}
internal sealed class Worker : ApplicationContext
{
	private readonly Control dispatch = new Control();

	private readonly Audio audio;

	private readonly Timer timeout = new Timer();

	private readonly bool preview;

	private readonly AudioBank bank;

	public Worker(string[] args)
	{
		dispatch.CreateControl();
		IntPtr handle = dispatch.Handle;
		preview = args[0] == "--preview";
		bank = new AudioBank(Wire.Output);
		Action<string> state = delegate(string s)
		{
			if (s == "ready")
			{
				Wire.Output(new
				{
					type = "ready"
				});
			}
			else if (s == "played")
			{
				Wire.Output(new
				{
					type = "played"
				});
			}
			else if (s.StartsWith("error:"))
			{
				Wire.Output(new
				{
					type = "error",
					message = s.Substring(6)
				});
				if (preview)
				{
					Environment.ExitCode = 1;
					ExitThread();
				}
			}
		};
		audio = new Audio(state);
		if (preview)
		{
			timeout.Interval = 8500;
			timeout.Tick += delegate
			{
				ExitThread();
			};
			timeout.Start();
			audio.Loaded = delegate
			{
				timeout.Interval = 3000;
			};
			try
			{
				audio.Configure(args[1], int.Parse(args[2]), preview: true);
				return;
			}
			catch (Exception ex)
			{
				Wire.Output(new
				{
					type = "error",
					message = ex.Message
				});
				Environment.ExitCode = 1;
				dispatch.BeginInvoke(new Action(base.ExitThread));
				return;
			}
		}
		Task.Run(delegate
		{
			try
			{
				string text;
				while ((text = Console.ReadLine()) != null)
				{
					if (text.Length > 16384)
					{
						break;
					}
					Dictionary<string, object> message = Wire.Read(text);
					dispatch.BeginInvoke((Action)delegate
					{
						Handle(message);
					});
				}
			}
			catch
			{
			}
			finally
			{
				if (!dispatch.IsDisposed)
				{
					try
					{
						dispatch.BeginInvoke(new Action(base.ExitThread));
					}
					catch
					{
					}
				}
			}
		});
	}

	private void Handle(Dictionary<string, object> message)
	{
		try
		{
			string text = Wire.Text(message, "type");
			if (text == "configure")
			{
				bank.Configure(message);
			}
			else if (text == "kill")
			{
				bank.Play(message);
			}
		}
		catch (Exception ex)
		{
			Wire.Output(new
			{
				type = "error",
				message = ex.Message
			});
		}
	}

	protected override void ExitThreadCore()
	{
		timeout.Stop();
		audio.Dispose();
		bank.Dispose();
		dispatch.Dispose();
		base.ExitThreadCore();
	}
}
internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		try
		{
			if (args.Length > 0 && (args[0] == "--worker" || args[0] == "--preview"))
			{
				StreamWriter streamWriter = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				streamWriter.AutoFlush = true;
				Console.SetOut(streamWriter);
				if (args[0] == "--worker")
				{
					Console.SetIn(new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)));
				}
				Application.Run(new Worker(args));
			}
			else
			{
				MessageBox.Show("请从主机管理面板的音效设置中使用播放器。", "BF4 本机音效");
			}
		}
		catch (Exception ex)
		{
			if (args.Length > 0)
			{
				Wire.Output(new
				{
					type = "error",
					message = ex.Message
				});
				Environment.ExitCode = 1;
			}
			else
			{
				MessageBox.Show(ex.Message, "BF4 击杀音效");
			}
		}
	}
}
