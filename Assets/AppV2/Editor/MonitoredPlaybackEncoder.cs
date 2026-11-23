#if UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.Recorder.Encoder;
using UnityEngine;

[Serializable]
[EncoderSettings(typeof(MonitoredPlaybackEncoder))]
public sealed class MonitoredPlaybackEncoderSettings : IEncoderSettings
{
    private static readonly IEncoderSettings Mp4 = new CoreEncoderSettings
    {
        Codec = CoreEncoderSettings.OutputCodec.MP4
    };

    public string Extension => Mp4.Extension;
    public bool CanCaptureAlpha => Mp4.CanCaptureAlpha;
    public bool CanCaptureAudio => Mp4.CanCaptureAudio;
    public EncoderCoordinateConvention CoordinateConvention => Mp4.CoordinateConvention;
    public TextureFormat GetTextureFormat(bool inputContainsAlpha) => Mp4.GetTextureFormat(inputContainsAlpha);
    public bool SupportsCurrentPlatform() => Mp4.SupportsCurrentPlatform();

    public void ValidateRecording(RecordingContext context, List<string> errors, List<string> warnings)
        => Mp4.ValidateRecording(context, errors, warnings);
}

public sealed class MonitoredPlaybackEncoder : IEncoder
{
    private MediaEncoder _encoder;
    private WindowsAudioMonitor _monitor;
    private int _width;
    private int _height;

    public void OpenStream(IEncoderSettings settings, RecordingContext context)
    {
        _width = context.width;
        _height = context.height;
        var video = new VideoTrackAttributes
        {
            width = (uint)_width,
            height = (uint)_height,
            frameRate = context.fps,
            bitRateMode = VideoBitrateMode.High
        };

        if (!context.doCaptureAudio)
        {
            _encoder = new MediaEncoder(context.path, video);
            return;
        }

        var channels = (ushort)(AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2);
        int sampleRate = AudioSettings.outputSampleRate;
        _encoder = new MediaEncoder(context.path, video, new AudioTrackAttributes
        {
            channelCount = channels,
            sampleRate = new MediaRational(sampleRate),
            language = ""
        });
        _monitor = WindowsAudioMonitor.Open(channels, sampleRate);
    }

    public void AddVideoFrame(NativeArray<byte> frame, MediaTime time)
    {
        if (!_encoder.AddFrame(_width, _height, 0, TextureFormat.RGBA32, frame, time))
            Debug.LogError("Videobild konnte nicht in die MP4 geschrieben werden.");
    }

    public void AddAudioFrame(NativeArray<float> samples)
    {
        if (samples.Length == 0) return;

        if (!_encoder.AddSamples(samples))
            Debug.LogError("Audiodaten konnten nicht in die MP4 geschrieben werden.");

        _monitor?.Play(samples);
    }

    public void CloseStream()
    {
        try
        {
            _monitor?.Dispose();
        }
        finally
        {
            _monitor = null;
            _encoder?.Dispose();
            _encoder = null;
        }
    }
}

internal sealed class WindowsAudioMonitor : IDisposable
{
    private const uint WaveMapper = uint.MaxValue;
    private const uint HeaderDone = 1;
    private const int QueueLimit = 8;
    private const int InitialBufferCount = 2;
    private static readonly uint HeaderSize = (uint)Marshal.SizeOf<WaveHeader>();
    private readonly Queue<IntPtr> _buffers = new Queue<IntPtr>(QueueLimit);
    private IntPtr _device;
    private bool _priming;
    private short[] _pcm = Array.Empty<short>();

    private WindowsAudioMonitor(IntPtr device)
    {
        _device = device;
        // Queue a little audio before playback to absorb small changes in frame timing.
        _priming = waveOutPause(_device) == 0;
    }

    public static WindowsAudioMonitor Open(ushort channels, int sampleRate)
    {
        ushort blockAlign = (ushort)(channels * sizeof(short));
        var format = new WaveFormat
        {
            FormatTag = 1, // PCM
            Channels = channels,
            SamplesPerSecond = (uint)sampleRate,
            BlockAlign = blockAlign,
            AverageBytesPerSecond = (uint)sampleRate * blockAlign,
            BitsPerSample = 16
        };

        uint result = waveOutOpen(out IntPtr device, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, 0);
        if (result == 0) return new WindowsAudioMonitor(device);

        Debug.LogWarning($"Windows-Audioausgabe zum Mithören konnte nicht geöffnet werden (Fehler {result}).");
        return null;
    }

    public void Play(NativeArray<float> samples)
    {
        if (_device == IntPtr.Zero) return;

        while (_buffers.Count > 0)
        {
            IntPtr oldest = _buffers.Peek();
            if ((Marshal.PtrToStructure<WaveHeader>(oldest).Flags & HeaderDone) == 0) break;
            if (waveOutUnprepareHeader(_device, oldest, HeaderSize) != 0) break;
            Marshal.FreeHGlobal(_buffers.Dequeue());
        }

        // Keep playback latency bounded if the recording briefly runs ahead of the device.
        if (_buffers.Count == QueueLimit) return;

        if (_pcm.Length < samples.Length) Array.Resize(ref _pcm, samples.Length);
        for (int i = 0; i < samples.Length; i++)
            _pcm[i] = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);

        int byteCount = samples.Length * sizeof(short);
        IntPtr buffer = Marshal.AllocHGlobal((int)HeaderSize + byteCount);
        IntPtr data = IntPtr.Add(buffer, (int)HeaderSize);
        Marshal.Copy(_pcm, 0, data, samples.Length);
        Marshal.StructureToPtr(new WaveHeader { Data = data, Length = (uint)byteCount }, buffer, false);

        uint result = waveOutPrepareHeader(_device, buffer, HeaderSize);
        if (result == 0)
        {
            _buffers.Enqueue(buffer);
            result = waveOutWrite(_device, buffer, HeaderSize);
            if (result == 0 && _priming && _buffers.Count >= InitialBufferCount)
            {
                result = waveOutRestart(_device);
                _priming = false;
            }
            if (result == 0) return;
        }
        else
        {
            Marshal.FreeHGlobal(buffer);
        }

        Debug.LogWarning($"Windows-Audioausgabe zum Mithören wurde beendet (Fehler {result}).");
        Dispose();
    }

    public void Dispose()
    {
        if (_device == IntPtr.Zero) return;

        // Reset returns queued buffers before their backing memory is released.
        waveOutReset(_device);
        while (_buffers.Count > 0)
        {
            IntPtr buffer = _buffers.Dequeue();
            if (waveOutUnprepareHeader(_device, buffer, HeaderSize) == 0)
                Marshal.FreeHGlobal(buffer);
        }
        waveOutClose(_device);
        _device = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSecond;
        public uint AverageBytesPerSecond;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort ExtraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint Length;
        public uint BytesRecorded;
        public UIntPtr User;
        public uint Flags;
        public uint Loops;
        public IntPtr Next;
        public UIntPtr Reserved;
    }

    [DllImport("winmm.dll")]
    private static extern uint waveOutOpen(out IntPtr device, uint deviceId, ref WaveFormat format,
        IntPtr callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    private static extern uint waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);

    [DllImport("winmm.dll")]
    private static extern uint waveOutWrite(IntPtr device, IntPtr header, uint size);

    [DllImport("winmm.dll")]
    private static extern uint waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);

    [DllImport("winmm.dll")]
    private static extern uint waveOutPause(IntPtr device);

    [DllImport("winmm.dll")]
    private static extern uint waveOutRestart(IntPtr device);

    [DllImport("winmm.dll")]
    private static extern uint waveOutReset(IntPtr device);

    [DllImport("winmm.dll")]
    private static extern uint waveOutClose(IntPtr device);
}
#endif
