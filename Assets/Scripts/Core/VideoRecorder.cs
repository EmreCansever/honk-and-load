using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace HonkAndLoad.Core
{
    /// <summary>
    /// Reklam videosu için kare kare kayıt: her kare JPG, ses WAV olarak yazılır.
    /// Oyun saati sabit kare hızına kilitlenir (Time.captureFramerate), böylece bilgisayar
    /// yavaş olsa bile video akıcı çıkar. Kareler sonra ffmpeg ile MP4'e çevrilir.
    /// </summary>
    public class VideoRecorder : MonoBehaviour
    {
        public bool IsRecording { get; private set; }
        public string OutputFolder { get; private set; }
        public int FrameCount { get; private set; }

        private int _fps;
        private readonly List<float> _audio = new List<float>();
        private int _channels;

        public void Begin(string folder, int fps)
        {
            OutputFolder = folder;
            Directory.CreateDirectory(folder);
            _fps = fps;
            FrameCount = 0;
            _audio.Clear();
            _channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;

            Time.captureFramerate = fps;
            AudioRenderer.Start();
            IsRecording = true;
            StartCoroutine(CaptureLoop());
        }

        public void End()
        {
            if (!IsRecording) return;
            IsRecording = false;
            AudioRenderer.Stop();
            Time.captureFramerate = 0;
            WriteWav(Path.Combine(OutputFolder, "audio.wav"));
            File.WriteAllText(Path.Combine(OutputFolder, "info.txt"),
                $"fps={_fps}\nframes={FrameCount}\nresolution={Screen.width}x{Screen.height}\nchannels={_channels}\nsampleRate={AudioSettings.outputSampleRate}\n");
            Debug.Log($"[HonkAndLoad] Video kaydı bitti: {FrameCount} kare → {OutputFolder}");
        }

        private IEnumerator CaptureLoop()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (IsRecording)
            {
                yield return endOfFrame;
                if (!IsRecording) break;

                Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
                byte[] jpg = frame.EncodeToJPG(90);
                Destroy(frame);
                File.WriteAllBytes(Path.Combine(OutputFolder, $"frame_{FrameCount:00000}.jpg"), jpg);
                FrameCount++;

                int samples = AudioRenderer.GetSampleCountForCaptureFrame();
                if (samples > 0)
                {
                    using (var buffer = new NativeArray<float>(samples * _channels, Allocator.Temp))
                    {
                        AudioRenderer.Render(buffer);
                        for (int i = 0; i < buffer.Length; i++) _audio.Add(buffer[i]);
                    }
                }
            }
        }

        private void WriteWav(string path)
        {
            int rate = AudioSettings.outputSampleRate;
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int dataBytes = _audio.Count * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)_channels);
                w.Write(rate);
                w.Write(rate * _channels * 2);
                w.Write((short)(_channels * 2));
                w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (float f in _audio)
                    w.Write((short)(Mathf.Clamp(f, -1f, 1f) * short.MaxValue));
            }
        }

        private void OnDisable()
        {
            if (IsRecording) End();
        }
    }
}
