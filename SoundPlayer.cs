using System.Collections.Generic;
using IrrKlang;

namespace MusicPlayer
{
    public class SoundPlayer
    {
        static ISoundEngine soundEngine;
        public string RootDirectory;
        List<Soundtrack> Sounds;

        class Soundtrack
        {
            public ISound Sound;
            public ISoundSource SoundSource;
            public bool IsIntroToNext = false;
            public bool IsRepeating = false;
            public float Volume;

            public Soundtrack(string filename, float volume, bool isLoop, bool isIntro)
            {
                IsIntroToNext = isIntro;
                IsRepeating = isLoop;
                Volume = volume;
                SoundSource = soundEngine.AddSoundSourceFromFile(filename, StreamMode.Streaming, false);
                Sound = soundEngine.Play2D(SoundSource, IsRepeating, true, true);
                Sound.Volume = volume;
            }
        }

        public void Initialize(string rootDir)
        {
            RootDirectory = rootDir;
            soundEngine = new ISoundEngine();
            soundEngine.SetListenerPosition(0, 0, 0, 0, 0, 1);
            Sounds = new List<Soundtrack>();
        }

        /// <summary>
        /// 播放指定的BGM。需先指定BGMRootDirectory參數。
        /// </summary>
        public void AddSound(string filename, float volume = 1f, bool isRepeating = false, bool isIntro = false)
        {
            Sounds.Add(new Soundtrack(RootDirectory + "\\" + filename, volume, isRepeating, isIntro));
        }

        public void Update()
        {
            // 偵測是否有Intro音軌需要切到下一軌
            for (int i = 0; i < Sounds.Count; i++)
            {
                if (Sounds[i].IsIntroToNext && Sounds[i].Sound.PlayPosition >= Sounds[i].Sound.PlayLength - 50)
                {
                    if (!IsPlaying(i + 1))
                    {
                        StartSound(i + 1);
                    }
                    if (!Sounds[i].Sound.Finished)
                    {
                        Sounds[i].Sound.Volume = Sounds[i].Sound.PlayLength - Sounds[i].Sound.PlayPosition;
                    }
                }
                else
                {
                    if (Sounds[i].Sound.Finished)
                    {
                        Sounds[i].Sound.Dispose();
                    }
                }
            }
        }

        public void Clear()
        {
            Sounds.Clear();
        }
        public void Dispose()
        {
            soundEngine.Dispose();
        }

        public bool IsPlaying(int soundNo)
        {
            bool isPausedOrStopped = false;
            if (Sounds[soundNo] != null)
            {
                if (Sounds[soundNo].Sound != null)
                {
                    isPausedOrStopped = !(Sounds[soundNo].Sound.Paused | Sounds[soundNo].Sound.Finished);
                }
            }
            return isPausedOrStopped;
        }

        public void StartSound(int soundNo)
        {
            if (Sounds[soundNo] != null)
            {
                if (IsPlaying(soundNo))
                {
                    // 如果正在播則倒回開頭就好
                    Sounds[soundNo].Sound.PlayPosition = 0;
                }
                else
                {
                    // 沒在播則重新呼叫一次Play
                    Sounds[soundNo].Sound = soundEngine.Play2D(Sounds[soundNo].SoundSource, Sounds[soundNo].IsRepeating, true, true);
                    // 不知道為啥有時候Play2D會load失敗，所以加個保險
                    if (Sounds[soundNo].Sound != null)
                    {
                        Sounds[soundNo].Sound.Volume = Sounds[soundNo].Volume;
                        Sounds[soundNo].Sound.Paused = false;
                    }
                }
            }
        }

        public void StopSound(int soundNo)
        {
            if (Sounds[soundNo] != null)
            {
                Sounds[soundNo].Sound.Stop();
            }
        }

        public void SetVolume(int soundNo, float volume)
        {
            if (Sounds[soundNo] != null)
            {
                Sounds[soundNo].Volume = volume;        // 設定往後每次播放時的音量
                Sounds[soundNo].Sound.Volume = volume;  // 如果正在播放中則一併更改Sound instace的音量
            }
        }

        public float GetVolume(int soundNo)
        {
            if (Sounds[soundNo] != null)
            {
                return Sounds[soundNo].Volume;
            }
            else
            {
                return 0f;
            }
        }

        public uint GetPosition(int soundNo)
        {
            if (Sounds[soundNo] != null)
            {
                return Sounds[soundNo].Sound.PlayPosition;
            }
            else
            {
                return 0;
            }
        }

        public void AllStop()
        {
            soundEngine.StopAllSounds();
        }
    }

}
