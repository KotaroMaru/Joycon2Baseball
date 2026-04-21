using UnityEngine;

namespace JoyconBaseball.Phase1.Audio
{
    public sealed class Phase1AudioManager : MonoBehaviour
    {
        private const float BgmVolume = 0.4f;
        private const float SeVolume = 1.0f;

        private AudioSource bgmSource;
        private AudioSource seSource;

        private AudioClip bgmClip;
        private AudioClip startClip;
        private AudioClip hitStrongClip;
        private AudioClip hitNormalClip;
        private AudioClip hitWeakClip;
        private AudioClip swingClip;
        private AudioClip catcherCatchClip;
        private AudioClip strikeClip;
        private AudioClip ballClip;
        private AudioClip outClip;
        private AudioClip cheeringClip;

        public void Initialize(
            AudioClip bgm,
            AudioClip start,
            AudioClip hitStrong,
            AudioClip hitNormal,
            AudioClip hitWeak,
            AudioClip swing,
            AudioClip catcherCatch,
            AudioClip strike,
            AudioClip ball,
            AudioClip @out,
            AudioClip cheering)
        {
            bgmClip = bgm;
            startClip = start;
            hitStrongClip = hitStrong;
            hitNormalClip = hitNormal;
            hitWeakClip = hitWeak;
            swingClip = swing;
            catcherCatchClip = catcherCatch;
            strikeClip = strike;
            ballClip = ball;
            outClip = @out;
            cheeringClip = cheering;

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.volume = BgmVolume;
            bgmSource.playOnAwake = false;

            seSource = gameObject.AddComponent<AudioSource>();
            seSource.loop = false;
            seSource.volume = SeVolume;
            seSource.playOnAwake = false;
        }

        public void StartBgm()
        {
            if (bgmClip == null)
            {
                Debug.LogWarning("[Audio] BGM clip が未設定です。");
                return;
            }

            bgmSource.clip = bgmClip;
            bgmSource.Play();
        }

        public void StopBgm()
        {
            bgmSource.Stop();
        }

        public void PlayStartSound()
        {
            PlaySe(startClip, "Start");
        }

        public void PlayHitSound(float hitSpeed)
        {
            const float StrongThreshold = 20f;
            const float NormalThreshold = 10f;

            if (hitSpeed >= StrongThreshold)
            {
                PlaySe(hitStrongClip, "HitStrong");
            }
            else if (hitSpeed >= NormalThreshold)
            {
                PlaySe(hitNormalClip, "HitNormal");
            }
            else
            {
                PlaySe(hitWeakClip, "HitWeak");
            }
        }

        public void PlaySwingSound()
        {
            PlaySe(swingClip, "Swing");
        }

        public void PlayCatcherCatchSound()
        {
            PlaySe(catcherCatchClip, "CatcherCatch");
        }

        public void PlayStrikeSound()
        {
            PlaySe(strikeClip, "Strike");
        }

        public void PlayBallSound()
        {
            PlaySe(ballClip, "Ball");
        }

        public void PlayOutSound()
        {
            PlaySe(outClip, "Out");
        }

        public void PlayCheeringSound()
        {
            PlaySe(cheeringClip, "Cheering");
        }

        private void PlaySe(AudioClip clip, string clipName)
        {
            if (clip == null)
            {
                Debug.LogWarning($"[Audio] {clipName} clip が未設定です。");
                return;
            }

            seSource.PlayOneShot(clip);
        }
    }
}
