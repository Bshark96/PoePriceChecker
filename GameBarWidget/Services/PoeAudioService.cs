using System;
using System.Diagnostics;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace GameBarWidget.Services
{
    public static class PoeAudioService
    {
        private static MediaPlayer _player;

        public static void PlayLiveAlertSound()
        {
            try
            {
                // Play crisp Windows notification alert audio
                string notifySound = @"C:\Windows\Media\Windows Notify Notification.wav";
                string dingSound = @"C:\Windows\Media\ding.wav";
                string tadaSound = @"C:\Windows\Media\tada.wav";

                if (System.IO.File.Exists(notifySound))
                {
                    PlayWavFile(notifySound);
                }
                else if (System.IO.File.Exists(dingSound))
                {
                    PlayWavFile(dingSound);
                }
                else if (System.IO.File.Exists(tadaSound))
                {
                    PlayWavFile(tadaSound);
                }
                else
                {
                    PlaySystemBeep();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PoeAudioService] Alert sound error: {ex.Message}");
                PlaySystemBeep();
            }
        }

        private static void PlayWavFile(string filePath)
        {
            try
            {
                if (_player == null)
                {
                    _player = new MediaPlayer();
                }
                _player.Source = MediaSource.CreateFromUri(new Uri(filePath));
                _player.Volume = 1.0;
                _player.Play();
            }
            catch
            {
                PlaySystemBeep();
            }
        }

        private static void PlaySystemBeep()
        {
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch { }
        }
    }
}
