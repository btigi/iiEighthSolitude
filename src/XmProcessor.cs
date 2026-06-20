using SharpMod;
using SharpMod.Player;

namespace ii.EighthSolitude
{
    public class XmProcessor
    {
        private const int RenderBufferSize = 32768;

        public byte[] Read(string filename)
        {
            var module = ModuleLoader.Instance.LoadModule(filename);
            var player = new ModulePlayer(module);
            player.RegisterRenderer(new OfflineRenderer());

            var buffer = new byte[RenderBufferSize];
            using var pcmStream = new MemoryStream();
            var finished = false;
            var seenPositions = new HashSet<(short SongPosition, short PatternRow)>();

            player.OnCurrentModulePlayEnd += (_, _) => finished = true;
            player.Start();

            while (player.IsPlaying && !finished && !player.PlayerInstance.MP_Ready())
            {
                var position = (player.PlayerInstance.mp_sngpos, player.PlayerInstance.mp_patpos);

                if (!seenPositions.Add(position))
                { 
                    break;
                }

                var read = player.GetBytes(buffer, buffer.Length);
                if (read <= 0)
                { 
                    break;
                }

                pcmStream.Write(buffer, 0, read);
            }

            player.Stop();
            return BuildWav(pcmStream.ToArray(), player.MixCfg);
        }

        private static byte[] BuildWav(byte[] pcm, MixConfig mixCfg)
        {
            const short pcmFormat = 1;
            var channels = (short)(mixCfg.Style == RenderingStyle.Mono ? 1 : 2);
            var bitsPerSample = (short)(mixCfg.Is16Bits ? 16 : 8);
            var sampleRate = mixCfg.Rate;
            var byteRate = sampleRate * channels * bitsPerSample / 8;
            var blockAlign = (short)(channels * bitsPerSample / 8);

            using var ms = new MemoryStream(44 + pcm.Length);
            using var bw = new BinaryWriter(ms);

            bw.Write("RIFF"u8.ToArray());
            bw.Write(36 + pcm.Length);
            bw.Write("WAVE"u8.ToArray());

            bw.Write("fmt "u8.ToArray());
            bw.Write(16);
            bw.Write(pcmFormat);
            bw.Write(channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);

            bw.Write("data"u8.ToArray());
            bw.Write(pcm.Length);
            bw.Write(pcm);

            return ms.ToArray();
        }

        private sealed class OfflineRenderer : IRenderer
        {
            public ModulePlayer Player { get; set; } = null!;

            public void Init()
            {
            }

            public void PlayStart()
            {
            }

            public void PlayStop()
            {
            }
        }
    }
}