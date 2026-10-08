using System;
using System.Diagnostics;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;

namespace GameBarWidget.Services
{
    public static class PoeAudioService
    {
        private static MediaPlayer _player;

        public static void PlayLiveAlertSound()
        {
            try
            {
                if (_player == null)
                {
                    _player = new MediaPlayer();
                }

                string notifySound = @"C:\Windows\Media\Windows Notify Notification.wav";
                string dingSound = @"C:\Windows\Media\ding.wav";

                if (System.IO.File.Exists(notifySound))
                {
                    _player.Source = MediaSource.CreateFromUri(new Uri(notifySound));
                    _player.Volume = 1.0;
                    _player.Play();
                }
                else if (System.IO.File.Exists(dingSound))
                {
                    _player.Source = MediaSource.CreateFromUri(new Uri(dingSound));
                    _player.Volume = 1.0;
                    _player.Play();
                }
                else
                {
                    PlayUwpSystemSound();
                }
            }
            catch
            {
                PlayUwpSystemSound();
            }
        }

        private static void PlayUwpSystemSound()
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
