using System.IO;
using System.Media;
using System.Text;

namespace VirtualKeyboard.App.Services;

/// <summary>Short synthesized click. No audio assets, no external dependencies.</summary>
public sealed class ClickSound : IDisposable
{
    private readonly SoundPlayer? _player;

    public ClickSound()
    {
        try
        {
            using var stream = new MemoryStream(BuildWav());
            _player = new SoundPlayer(stream);
            _player.Load();
        }
        catch
        {
            _player = null;
        }
    }

    public bool Enabled { get; set; } = true;

    public void Play()
    {
        if (!Enabled || _player is null)
        {
            return;
        }

        try
        {
            _player.Play();
        }
        catch
        {
            // A failed click must never break typing.
        }
    }

    public void Dispose() => _player?.Dispose();

    private static byte[] BuildWav()
    {
        const int sampleRate = 22050;
        const double duration = 0.018;
        var samples = (int)(sampleRate * duration);
        var dataSize = samples * 2;

        using var stream = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);          // PCM
        writer.Write((short)1);          // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);    // byte rate
        writer.Write((short)2);          // block align
        writer.Write((short)16);         // bits per sample
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        for (var i = 0; i < samples; i++)
        {
            var t = i / (double)sampleRate;
            var envelope = Math.Exp(-t * 190);
            var value = Math.Sin(2 * Math.PI * 1500 * t) * envelope * 0.32;
            writer.Write((short)(value * short.MaxValue));
        }

        writer.Flush();
        return stream.ToArray();
    }
}
