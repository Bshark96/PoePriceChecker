using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace GameBarWidget.Services
{
    public static class PoeAudioService
    {
        private static MediaPlayer _player;

        public static async void PlayLiveAlertSound()
        {
            try
            {
                // Generate in-memory 2-tone PCM WAV alert chime (880Hz -> 1320Hz)
                byte[] wavBytes = GenerateChimeWavBytes();

                var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(wavBytes);
                    await writer.StoreAsync();
                }

                if (_player == null)
                {
                    _player = new MediaPlayer();
                }

                _player.Source = MediaSource.CreateFromStream(stream, "audio/wav");
                _player.Volume = 1.0;
                _player.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PoeAudioService] Memory audio error: {ex.Message}");
                PlayUwpFallbackSound();
            }
        }

        private static byte[] GenerateChimeWavBytes()
        {
            int sampleRate = 44100;
            int durationMs = 280;
            int numSamples = sampleRate * durationMs / 1000;
            short numChannels = 1;
            short bitsPerSample = 16;
            int byteRate = sampleRate * numChannels * (bitsPerSample / 8);
            short blockAlign = (short)(numChannels * (bitsPerSample / 8));
            int dataSize = numSamples * numChannels * (bitsPerSample / 8);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // RIFF Header
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                // FMT Subchunk
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // Subchunk1Size (PCM)
                writer.Write((short)1); // AudioFormat
                writer.Write(numChannels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);

                // DATA Subchunk
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                int halfSamples = numSamples / 2;
                for (int i = 0; i < numSamples; i++)
                {
                    double t = (double)i / sampleRate;
                    // Two-tone chime: 880Hz (A5) then 1320Hz (E6)
                    double freq = (i < halfSamples) ? 880.0 : 1320.0;
                    double envelope = 1.0 - ((double)i / numSamples); // Linear decay
                    short sample = (short)(Math.Sin(2.0 * Math.PI * freq * t) * 26000.0 * envelope);
                    writer.Write(sample);
                }

                return ms.ToArray();
            }
        }

        private static void PlayUwpFallbackSound()
        {
            try
            {
                Windows.UI.Xaml.ElementSoundPlayer.State = Windows.UI.Xaml.ElementSoundPlayerState.On;
                Windows.UI.Xaml.ElementSoundPlayer.Play(Windows.UI.Xaml.ElementSoundKind.Invoke);
            }
            catch { }
        }
    }
}
