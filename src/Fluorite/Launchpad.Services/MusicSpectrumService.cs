using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Launchpad.Services;

internal sealed class MusicSpectrumService : IDisposable
{
	internal sealed record Levels(double Bass, double Mid, double Treble);

	[ComImport]
	[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IDeviceEnumerator
	{
		void EnumAudioEndpoints(int flow, uint mask, out nint collection);

		void GetDefaultAudioEndpoint(int flow, int role, out IDevice device);
	}

	[ComImport]
	[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IDevice
	{
		void Activate(ref Guid id, uint context, nint parameters, [MarshalAs(UnmanagedType.IUnknown)] out object value);

		void OpenPropertyStore(uint access, out nint store);

		void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
	}

	[ComImport]
	[Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioClient
	{
		void Initialize(int mode, uint flags, long duration, long periodicity, nint format, nint session);

		void GetBufferSize(out uint frames);

		void GetStreamLatency(out long latency);

		void GetCurrentPadding(out uint frames);

		void IsFormatSupported(int mode, nint format, out nint closest);

		void GetMixFormat(out nint format);

		void GetDevicePeriod(out long normal, out long minimum);

		void Start();

		void Stop();

		void Reset();

		void SetEventHandle(nint handle);

		void GetService(ref Guid id, [MarshalAs(UnmanagedType.IUnknown)] out object value);
	}

	[ComImport]
	[Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioCaptureClient
	{
		void GetBuffer(out nint data, out uint frames, out uint flags, out ulong devicePosition, out ulong timestamp);

		void ReleaseBuffer(uint frames);

		void GetNextPacketSize(out uint frames);
	}

	private readonly CancellationTokenSource _stop = new CancellationTokenSource();

	private int _disposed;

	private Levels _levels = new Levels(0.0, 0.0, 0.0);

	private readonly TaskCompletionSource<bool> _started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

	internal Task<bool> Started => _started.Task;

	internal Levels Current => Volatile.Read(in _levels);

	internal MusicSpectrumService()
	{
		Task.Run((Func<Task?>)Run);
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 0)
		{
			_stop.Cancel();
		}
	}

	private async Task Run()
	{
		try
		{
			while (!_stop.IsCancellationRequested)
			{
				try
				{
					await Capture(_stop.Token);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex2)
				{
					_started.TrySetResult(result: false);
					App.Log("[Music spectrum] " + ex2.Message);
				}
				Volatile.Write(ref _levels, new Levels(0.0, 0.0, 0.0));
				await Task.Delay(1500, _stop.Token);
			}
		}
		catch (OperationCanceledException)
		{
		}
		finally
		{
			_started.TrySetResult(result: false);
			_stop.Dispose();
		}
	}

	private async Task Capture(CancellationToken cancellation)
	{
		object enumerator = null;
		object device = null;
		object client = null;
		object capture = null;
		nint format = IntPtr.Zero;
		try
		{
			enumerator = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
			((IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 0, out IDevice device2);
			device = device2;
			device2.GetId(out string endpointId);
			Guid id = typeof(IAudioClient).GUID;
			device2.Activate(ref id, 23u, IntPtr.Zero, out object value);
			client = value;
			IAudioClient audio = (IAudioClient)client;
			audio.GetMixFormat(out format);
			int channels = Marshal.ReadInt16(format, 2);
			int rate = Marshal.ReadInt32(format, 4);
			int block = Marshal.ReadInt16(format, 12);
			int bits = Marshal.ReadInt16(format, 14);
			bool floating = (int)(ushort)Marshal.ReadInt16(format) switch
			{
				65534 => Marshal.PtrToStructure<Guid>(format + 24) == new Guid("00000003-0000-0010-8000-00AA00389B71"), 
				3 => true, 
				_ => false, 
			};
			bool validated = channels < 1 || block < channels;
			if (!validated)
			{
				bool flag2 = floating && bits == 32;
				if (!flag2)
				{
					bool flag3 = !floating;
					if (flag3)
					{
						int pageNumber = bits;
						bool flag4 = ((pageNumber == 16 || pageNumber == 24 || pageNumber == 32) ? true : false);
						flag3 = flag4;
					}
					flag2 = flag3;
				}
				validated = !flag2;
			}
			if (validated)
			{
				throw new NotSupportedException("Unsupported output audio format");
			}
			audio.Initialize(0, 131072u, 1000000L, 0L, format, IntPtr.Zero);
			Guid id2 = typeof(IAudioCaptureClient).GUID;
			audio.GetService(ref id2, out object failure);
			capture = failure;
			IAudioCaptureClient reader = (IAudioCaptureClient)capture;
			double[] ring = new double[1024];
			Complex[] fft = new Complex[1024];
			int position = 0;
			DateTime lastSound = DateTime.UtcNow;
			DateTime nextDeviceCheck = DateTime.UtcNow.AddSeconds(2.0);
			audio.Start();
			_started.TrySetResult(result: true);
			try
			{
				while (!cancellation.IsCancellationRequested)
				{
					if (DateTime.UtcNow >= nextDeviceCheck)
					{
						((IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 0, out IDevice device3);
						try
						{
							device3.GetId(out string id3);
							if (id3 != endpointId)
							{
								return;
							}
						}
						finally
						{
							Marshal.ReleaseComObject(device3);
						}
						nextDeviceCheck = DateTime.UtcNow.AddSeconds(2.0);
					}
					reader.GetNextPacketSize(out var frames);
					bool flag5 = false;
					while (frames != 0)
					{
						reader.GetBuffer(out var data, out var frames2, out var flags, out var _, out var _);
						try
						{
							Append(data, frames2, flags, channels, block, bits, floating, ring, ref position);
							flag5 = true;
						}
						finally
						{
							reader.ReleaseBuffer(frames2);
						}
						reader.GetNextPacketSize(out frames);
					}
					if (flag5)
					{
						lastSound = DateTime.UtcNow;
					}
					Levels value3;
					if (flag5)
					{
						value3 = Analyze(ring, position, rate, fft);
					}
					else
					{
						value3 = ((DateTime.UtcNow - lastSound > TimeSpan.FromMilliseconds(150.0)) ? new Levels(0.0, 0.0, 0.0) : Current);
					}
					Volatile.Write(ref _levels, value3);
					await Task.Delay(33, cancellation);
				}
			}
			finally
			{
				audio.Stop();
			}
		}
		finally
		{
			if (format != IntPtr.Zero)
			{
				Marshal.FreeCoTaskMem(format);
			}
			object[] entries = new object[4] { capture, client, device, enumerator };
			foreach (object target in entries)
			{
				if (target != null && Marshal.IsComObject(target))
				{
					Marshal.ReleaseComObject(target);
				}
			}
		}
	}

	private unsafe static void Append(nint data, uint frames, uint flags, int channels, int block, int bits, bool floating, double[] ring, ref int position)
	{
		for (uint pageNumber = 0u; pageNumber < frames; pageNumber++)
		{
			double settingIndex = 0.0;
			if ((flags & 2) == 0 && data != IntPtr.Zero)
			{
				for (int i = 0; i < channels; i++)
				{
					byte* ptr = (byte*)(data + (nint)(pageNumber * block) + i * (bits / 8));
					settingIndex += (floating ? ((double)(*(float*)ptr)) : (bits switch
					{
						32 => (double)(*(int*)ptr) / 2147483648.0, 
						16 => (double)(*(short*)ptr) / 32768.0, 
						_ => (double)((*ptr | (ptr[1] << 8) | (ptr[2] << 16)) << 8 >> 8) / 8388608.0, 
					}));
				}
			}
			ring[position] = (double.IsFinite(settingIndex) ? Math.Clamp(settingIndex / (double)channels, -1.0, 1.0) : 0.0);
			position = (position + 1) % ring.Length;
		}
	}

	internal static Levels Analyze(double[] ring, int position, int rate, Complex[] fft)
	{
		int n = ring.Length;
		for (int i = 0; i < n; i++)
		{
			fft[i] = ring[(position + i) % n] * (0.5 - 0.5 * Math.Cos(Math.PI * 2.0 * (double)i / (double)(n - 1)));
		}
		int j = 1;
		int pageNumber = 0;
		for (; j < n; j++)
		{
			int settingIndex = n >> 1;
			while ((pageNumber & settingIndex) != 0)
			{
				pageNumber ^= settingIndex;
				settingIndex >>= 1;
			}
			pageNumber ^= settingIndex;
			if (j < pageNumber)
			{
				ref Complex reference = ref fft[j];
				ref Complex reference2 = ref fft[pageNumber];
				Complex complex = fft[pageNumber];
				Complex complex2 = fft[j];
				reference = complex;
				reference2 = complex2;
			}
		}
		for (int targetPage = 2; targetPage <= n; targetPage <<= 1)
		{
			Complex complex3 = Complex.FromPolarCoordinates(1.0, Math.PI * -2.0 / (double)targetPage);
			for (int k = 0; k < n; k += targetPage)
			{
				Complex one = Complex.One;
				for (int l = 0; l < targetPage / 2; l++)
				{
					Complex complex4 = fft[k + l];
					Complex complex5 = fft[k + l + targetPage / 2] * one;
					fft[k + l] = complex4 + complex5;
					fft[k + l + targetPage / 2] = complex4 - complex5;
					one *= complex3;
				}
			}
		}
		return new Levels(Band(40.0, 250.0), Band(250.0, 2500.0), Band(2500.0, 12000.0));
		double Band(double low, double high)
		{
			double num4 = 0.0;
			for (int m = Math.Max(1, (int)Math.Ceiling(low * (double)n / (double)rate)); m < n / 2 && (double)m * (double)rate / (double)n < high; m++)
			{
				num4 += fft[m].Magnitude * fft[m].Magnitude;
			}
			return Math.Clamp(Math.Sqrt(num4) * 8.0 / (double)n, 0.0, 1.0);
		}
	}
}
