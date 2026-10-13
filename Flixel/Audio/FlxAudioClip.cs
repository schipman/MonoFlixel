using System;
using System.IO;
using NVorbis;

namespace MonoFlixel;

/// <summary>
/// Reads decoded audio: 16 bit samples, interleaved, one frame per sample time (a mono frame is 2 bytes, a stereo frame 4).
/// Every playing sound has its own reader, so several copies of one clip can play at different positions.
/// </summary>
public abstract class FlxAudioReader : IDisposable
{
    public abstract int SampleRate { get; }
    public abstract int Channels { get; }
    public abstract long TotalFrames { get; }

    /// <summary>The frame the next Read starts at.</summary>
    public abstract long Position { get; }

    /// <summary>Reads up to <paramref name="frames"/> frames into <paramref name="destination"/>; returns how many were read (0 at the end).</summary>
    public abstract int Read(byte[] destination, int frames);

    public abstract void Seek(long frame);

    public virtual void Dispose() { }
}

/// <summary>
/// A decoded sound file, ready to play (FlxG.Sound loads and caches these). WAV files (PCM 8, 16, 24, 32 bit and 32 bit float, mono or
/// stereo) and MP3 files are decoded into memory; OGG Vorbis files are decoded while they play, unless they are short, so a long song does not take
/// up megabytes of memory.
/// </summary>
public sealed class FlxAudioClip
{
    /// <summary>OGG files shorter than this (seconds) are decoded into memory once instead of streamed.</summary>
    public const double DecodeOggUpTo = 5.0;

    readonly byte[]? _pcm;
    readonly byte[]? _oggFile;

    public string Name { get; }
    public int SampleRate { get; }
    public int Channels { get; }
    public long TotalFrames { get; }

    public int BlockAlign => Channels * 2;
    public double LengthMs => TotalFrames * 1000.0 / SampleRate;

    /// <summary>True if the clip is decoded while playing (long OGG files).</summary>
    public bool Streamed => _oggFile != null;

    FlxAudioClip(string name, int sampleRate, int channels, long totalFrames, byte[]? pcm, byte[]? oggFile)
    {
        Name = name; SampleRate = sampleRate; Channels = channels; TotalFrames = totalFrames; _pcm = pcm; _oggFile = oggFile;
    }

    /// <summary>A clip from raw 16 bit interleaved samples (generated sounds, tests).</summary>
    public static FlxAudioClip FromPcm(byte[] pcm16, int sampleRate, int channels, string name = "")
    {
        if (channels is < 1 or > 2) throw new NotSupportedException("Only mono and stereo sounds are supported.");
        if (sampleRate <= 0) throw new ArgumentException("A sound needs a sample rate.", nameof(sampleRate));
        return new FlxAudioClip(name, sampleRate, channels, pcm16.Length / (channels * 2), pcm16, null);
    }

    /// <summary>Loads a sound file by its path (.wav, .ogg or .mp3).</summary>
    public static FlxAudioClip Load(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".wav" && ext != ".ogg" && ext != ".mp3")
            throw new NotSupportedException($"Can't load '{Path.GetFileName(path)}': only .wav, .ogg and .mp3 sounds are supported.");

        var bytes = File.ReadAllBytes(path);
        string name = Path.GetFileNameWithoutExtension(path);
        return ext switch { ".wav" => FromWav(bytes, name), ".ogg" => FromOgg(bytes, name), _ => FromMp3(bytes, name) };
    }

    public FlxAudioReader OpenReader() => _pcm != null ? new PcmReader(this, _pcm) : new OggReader(this, _oggFile!);

    // ------------------------------------------------------------------ WAV

    /// <summary>Decodes a WAV file (PCM or float, 8 to 32 bit, mono or stereo) into 16 bit samples.</summary>
    public static FlxAudioClip FromWav(byte[] file, string name = "")
    {
        if (file.Length < 12 || file[0] != 'R' || file[1] != 'I' || file[2] != 'F' || file[3] != 'F' || file[8] != 'W' || file[9] != 'A' || file[10] != 'V' || file[11] != 'E')
            throw new InvalidDataException($"'{name}' is not a WAV file.");

        int format = 0, channels = 0, rate = 0, bits = 0, dataStart = -1, dataLength = 0;

        // walk the chunks
        int pos = 12;
        while (pos + 8 <= file.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(file, pos, 4);
            int size = BitConverter.ToInt32(file, pos + 4);
            int body = pos + 8;

            if (id == "fmt " && size >= 16)
            {
                format = BitConverter.ToUInt16(file, body);
                channels = BitConverter.ToUInt16(file, body + 2);
                rate = BitConverter.ToInt32(file, body + 4);
                bits = BitConverter.ToUInt16(file, body + 14);
                if (format == 0xFFFE && size >= 26) format = BitConverter.ToUInt16(file, body + 24);      // extensible: the real format follows
            }
            else if (id == "data")
            {
                dataStart = body;
                // a streamed file may claim a size bigger than what is there
                dataLength = Math.Min(Math.Max(0, size), file.Length - body);
                break;
            }

            pos = body + size + (size & 1);
            if (size < 0) break;
        }

        if (dataStart < 0 || channels == 0) throw new InvalidDataException($"'{name}' has no sound data.");
        if (channels > 2) throw new NotSupportedException($"'{name}' has {channels} channels: only mono and stereo are supported.");
        if (format != 1 && format != 3)
            throw new NotSupportedException($"'{name}' is not plain PCM (format {format}): convert it to a normal 16 bit WAV or an OGG.");

        int bytesPerSample = bits / 8;
        int samples = dataLength / bytesPerSample;
        samples -= samples % channels;                       // whole frames only
        var pcm = new byte[samples * 2];

        for (int i = 0; i < samples; i++)
        {
            int p = dataStart + i * bytesPerSample;
            short s;
            if (format == 3)
            {
                float f = bits == 64 ? (float)BitConverter.ToDouble(file, p) : BitConverter.ToSingle(file, p);
                s = (short)Math.Clamp((int)MathF.Round(f * 32767f), -32768, 32767);
            }
            else
            {
                s = bits switch
                {
                    8 => (short)((file[p] - 128) << 8),
                    16 => BitConverter.ToInt16(file, p),
                    24 => (short)(((sbyte)file[p + 2] << 8) | file[p + 1]),            // keep the top 16 of the 24 bits
                    32 => (short)(BitConverter.ToInt32(file, p) >> 16),
                    _ => throw new NotSupportedException($"'{name}' uses {bits} bit samples.")
                };
            }
            pcm[i * 2] = (byte)(s & 0xFF);
            pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }

        return new FlxAudioClip(name, rate, channels, samples / channels, pcm, null);
    }

    // ------------------------------------------------------------------ MP3

    /// <summary>Decodes an MP3 file (with NLayer) into 16 bit samples in memory. A three minute song takes about 30 MB.</summary>
    public static FlxAudioClip FromMp3(byte[] file, string name = "")
    {
        try
        {
            using var mp3 = new NLayer.MpegFile(new MemoryStream(file));
            int channels = mp3.Channels, rate = mp3.SampleRate;
            if (channels is < 1 or > 2) throw new NotSupportedException($"'{name}' has {channels} channels: only mono and stereo are supported.");

            var output = new MemoryStream();
            var floats = new float[8192 * channels];
            var bytes = new byte[floats.Length * 2];
            int got;
            while ((got = mp3.ReadSamples(floats, 0, floats.Length)) > 0)
            {
                got -= got % channels;
                for (int i = 0; i < got; i++)
                {
                    short v = (short)Math.Clamp((int)MathF.Round(floats[i] * 32767f), -32768, 32767);
                    bytes[i * 2] = (byte)(v & 0xFF);
                    bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
                }
                output.Write(bytes, 0, got * 2);
            }

            var pcm = output.ToArray();
            if (pcm.Length == 0) throw new InvalidDataException($"'{name}' has no sound data.");
            return new FlxAudioClip(name, rate, channels, pcm.Length / (channels * 2), pcm, null);
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or EndOfStreamException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            if (e is InvalidDataException) throw;
            throw new InvalidDataException($"'{name}' is not a readable MP3 file ({e.Message}).", e);
        }
    }

    // ------------------------------------------------------------------ OGG

    /// <summary>Opens an OGG Vorbis file. Short ones are decoded now, long ones while they play.</summary>
    public static FlxAudioClip FromOgg(byte[] file, string name = "")
    {
        int channels, rate;
        long frames;
        try
        {
            using var vorbis = new VorbisReader(new MemoryStream(file), closeOnDispose: true);
            channels = vorbis.Channels;
            rate = vorbis.SampleRate;
            frames = vorbis.TotalSamples;
        }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or EndOfStreamException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException($"'{name}' is not a readable OGG Vorbis file ({e.Message}).", e);
        }

        if (channels > 2) throw new NotSupportedException($"'{name}' has {channels} channels: only mono and stereo are supported.");
        if (frames / (double)rate > DecodeOggUpTo)
            return new FlxAudioClip(name, rate, channels, frames, null, file);

        // short: decode it all now
        var clip = new FlxAudioClip(name, rate, channels, frames, null, file);
        using var reader = clip.OpenReader();
        var pcm = new byte[frames * channels * 2];
        int done = 0;
        while (done < frames)
        {
            var chunk = new byte[Math.Min(4096, (int)(frames - done)) * channels * 2];
            int got = reader.Read(chunk, chunk.Length / (channels * 2));
            if (got <= 0) break;
            Buffer.BlockCopy(chunk, 0, pcm, done * channels * 2, got * channels * 2);
            done += got;
        }

        long real = done;                       // the header can promise more than the file holds
        if (real < frames) Array.Resize(ref pcm, (int)(real * channels * 2));
        return new FlxAudioClip(name, rate, channels, real, pcm, null);
    }

    // ------------------------------------------------------------------ readers

    sealed class PcmReader : FlxAudioReader
    {
        readonly FlxAudioClip _clip;
        readonly byte[] _pcm;
        long _pos;

        public PcmReader(FlxAudioClip clip, byte[] pcm) { _clip = clip; _pcm = pcm; }

        public override int SampleRate => _clip.SampleRate;
        public override int Channels => _clip.Channels;
        public override long TotalFrames => _clip.TotalFrames;
        public override long Position => _pos;

        public override int Read(byte[] destination, int frames)
        {
            int block = _clip.BlockAlign;
            int n = (int)Math.Min(frames, _clip.TotalFrames - _pos);
            if (n <= 0) return 0;

            Buffer.BlockCopy(_pcm, (int)(_pos * block), destination, 0, n * block);
            _pos += n;
            return n;
        }

        public override void Seek(long frame) => _pos = Math.Clamp(frame, 0, _clip.TotalFrames);
    }

    sealed class OggReader : FlxAudioReader
    {
        readonly FlxAudioClip _clip;
        readonly byte[] _file;
        VorbisReader _vorbis;
        float[] _floats = new float[8192];
        long _pos;

        public OggReader(FlxAudioClip clip, byte[] file)
        {
            _clip = clip;
            _file = file;
            _vorbis = new VorbisReader(new MemoryStream(file), closeOnDispose: true);
        }

        public override int SampleRate => _clip.SampleRate;
        public override int Channels => _clip.Channels;
        public override long TotalFrames => _clip.TotalFrames;
        public override long Position => _pos;

        public override int Read(byte[] destination, int frames)
        {
            int channels = _clip.Channels;
            frames = (int)Math.Min(frames, _clip.TotalFrames - _pos);
            if (frames <= 0) return 0;
            if (_floats.Length < frames * channels) _floats = new float[frames * channels];

            int got;
            try { got = _vorbis.ReadSamples(_floats, 0, frames * channels) / channels; }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or EndOfStreamException)
            {
                // a damaged stream: the sound just ends there instead of crashing the game
                _pos = _clip.TotalFrames;
                return 0;
            }
            for (int i = 0; i < got * channels; i++)
            {
                short s = (short)Math.Clamp((int)MathF.Round(_floats[i] * 32767f), -32768, 32767);
                destination[i * 2] = (byte)(s & 0xFF);
                destination[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            _pos += got;
            return got;
        }

        public override void Seek(long frame)
        {
            frame = Math.Clamp(frame, 0, _clip.TotalFrames);
            if (frame == _pos) return;               // already there (also avoids seeking a stream that has not been read yet)

            try
            {
                _vorbis.SamplePosition = frame;
                _pos = frame;
                return;
            }
            catch (Exception)
            {
                // the decoder could not seek (it can fail on a stream that was just opened): start over and read up to the place
            }

            _vorbis.Dispose();
            _vorbis = new VorbisReader(new MemoryStream(_file), closeOnDispose: true);
            _pos = 0;

            var scratch = new byte[4096 * _clip.BlockAlign];
            while (_pos < frame)
                if (Read(scratch, (int)Math.Min(4096, frame - _pos)) <= 0) break;
        }

        public override void Dispose() => _vorbis.Dispose();
    }
}
