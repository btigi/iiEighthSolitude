using System.Buffers.Binary;

namespace ii.EighthSolitude
{
    public class SmpProcessor
    {
        private const int SampleRate = 16000;
        private const short Channels = 1;
        private const short BitsPerSample = 8;

        public byte[] Read(string filename)
        {
            var pcm = File.ReadAllBytes(filename);
            return BuildWav(pcm);
        }

        public void Write(string filename, byte[] wavBytes)
        {
            ArgumentNullException.ThrowIfNull(wavBytes);

            ReadOnlySpan<byte> pcm = ExtractPcmFromWav(wavBytes);
            using var stream = new FileStream(filename, FileMode.Create, FileAccess.Write);
            stream.Write(pcm);
        }

        private static ReadOnlySpan<byte> ExtractPcmFromWav(ReadOnlySpan<byte> wav)
        {
            if (wav.Length < 12)
                throw new InvalidDataException("WAV data is too small to contain a valid RIFF header.");

            if (!wav[..4].SequenceEqual("RIFF"u8))
                throw new InvalidDataException("Expected RIFF header.");
            if (!wav[8..12].SequenceEqual("WAVE"u8))
                throw new InvalidDataException("Expected WAVE format.");

            ushort format = 0;
            ushort channels = 0;
            uint sampleRate = 0;
            ushort bitsPerSample = 0;
            bool haveFmt = false;
            ReadOnlySpan<byte> pcm = default;
            bool haveData = false;

            int pos = 12;
            while (pos + 8 <= wav.Length)
            {
                ReadOnlySpan<byte> chunkId = wav.Slice(pos, 4);
                int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(wav.Slice(pos + 4, 4));
                int payloadStart = pos + 8;
                if (payloadStart > wav.Length || chunkSize < 0 || payloadStart + chunkSize > wav.Length)
                    throw new InvalidDataException("Invalid WAV chunk size or truncated file.");

                ReadOnlySpan<byte> payload = wav.Slice(payloadStart, chunkSize);

                if (chunkId.SequenceEqual("fmt "u8))
                {
                    if (chunkSize < 16)
                        throw new InvalidDataException("fmt chunk is too small.");

                    format = BinaryPrimitives.ReadUInt16LittleEndian(payload);
                    channels = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]);
                    sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(payload[4..]);
                    bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(payload[14..]);
                    haveFmt = true;
                }
                else if (chunkId.SequenceEqual("data"u8))
                {
                    pcm = payload;
                    haveData = true;
                }

                pos = payloadStart + chunkSize + (chunkSize & 1);
            }

            if (!haveFmt)
                throw new InvalidDataException("WAV is missing a fmt chunk.");
            if (!haveData)
                throw new InvalidDataException("WAV is missing a data chunk.");

            if (format != 1)
                throw new InvalidDataException($"Expected PCM (format 1); got format {format}.");
            if (channels != Channels)
                throw new InvalidDataException($"Expected {Channels} channel(s); got {channels}.");
            if (sampleRate != SampleRate)
                throw new InvalidDataException($"Expected sample rate {SampleRate} Hz; got {sampleRate} Hz.");
            if (bitsPerSample != BitsPerSample)
                throw new InvalidDataException($"Expected {BitsPerSample} bits per sample; got {bitsPerSample}.");

            return pcm;
        }

        private static byte[] BuildWav(byte[] pcm)
        {
            const short pcmFormat = 1;
            var byteRate = SampleRate * Channels * BitsPerSample / 8;
            short blockAlign = (short)(Channels * BitsPerSample / 8);

            using var ms = new MemoryStream(44 + pcm.Length);
            using var bw = new BinaryWriter(ms);

            bw.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            bw.Write(36 + pcm.Length);
            bw.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });

            bw.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            bw.Write(16);
            bw.Write(pcmFormat);
            bw.Write(Channels);
            bw.Write(SampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(BitsPerSample);

            bw.Write("data"u8.ToArray());
            bw.Write(pcm.Length);
            bw.Write(pcm);

            return ms.ToArray();
        }
    }
}