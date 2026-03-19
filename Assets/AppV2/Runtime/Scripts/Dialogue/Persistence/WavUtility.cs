using System;
using System.IO;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.Persistence
{
    public static class WavUtility
    {
        public static void SaveWav(string path, AudioClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            int channels = clip.channels;
            int frequency = clip.frequency;
            int samples = clip.samples;

            float[] data = new float[samples * channels];
            clip.GetData(data, 0);

            // float [-1..1] -> PCM16
            byte[] pcm = new byte[data.Length * 2];
            int o = 0;
            for (int i = 0; i < data.Length; i++)
            {
                short s = (short)Mathf.Clamp(data[i] * 32767f, short.MinValue, short.MaxValue);
                pcm[o++] = (byte)(s & 0xff);
                pcm[o++] = (byte)((s >> 8) & 0xff);
            }

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            int byteRate = frequency * channels * 2;
            int blockAlign = channels * 2;

            // RIFF header
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + pcm.Length);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);               // PCM
            bw.Write((short)channels);
            bw.Write(frequency);
            bw.Write(byteRate);
            bw.Write((short)blockAlign);
            bw.Write((short)16);              // bits

            // data chunk
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(pcm.Length);
            bw.Write(pcm);
        }

        // Minimaler WAV Loader (PCM16), reicht f�rs Wiederladen deiner eigenen Dateien
        public static AudioClip LoadWav(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);

            // sehr einfache WAV-Parsing-Annahme: PCM16, fmt chunk 16, data chunk vorhanden
            int channels = BitConverter.ToInt16(bytes, 22);
            int frequency = BitConverter.ToInt32(bytes, 24);

            int dataChunkOffset = FindDataChunk(bytes);
            int dataSize = BitConverter.ToInt32(bytes, dataChunkOffset + 4);
            int dataStart = dataChunkOffset + 8;

            int sampleCount = dataSize / 2; // int16
            float[] data = new float[sampleCount];

            int idx = 0;
            for (int i = dataStart; i < dataStart + dataSize; i += 2)
            {
                short s = BitConverter.ToInt16(bytes, i);
                data[idx++] = s / 32768f;
            }

            int samplesPerChannel = sampleCount / channels;
            var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), samplesPerChannel, channels, frequency, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static int FindDataChunk(byte[] bytes)
        {
            for (int i = 12; i < bytes.Length - 8; i++)
            {
                if (bytes[i] == (byte)'d' && bytes[i + 1] == (byte)'a' && bytes[i + 2] == (byte)'t' && bytes[i + 3] == (byte)'a')
                    return i;
            }
            throw new Exception("WAV data chunk not found.");
        }
    }
}
