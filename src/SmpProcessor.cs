namespace ii.EighthSolitude
{
    public class SmpProcessor
    {
        private const int SampleRate = 16000;
        private const short Channels = 1;
        private const short BitsPerSample = 8;

        public byte[] Process(string filename)
        {
            var pcm = File.ReadAllBytes(filename);
            return BuildWav(pcm);
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