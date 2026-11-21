using UnityEngine;

// the audio pieces more than one script needs, so each doesn't carry its own copy.
// nothing to attach: these are plain helpers the other scripts call.
//   Create2DSource  a sound that isn't placed in the world (it sounds the same wherever the player is)
//   LevelSound      a sound that plays while a level is above 0 and gets louder as the level rises
public static class AudioKit
{
    // a 2D AudioSource added to the owner, ready to play.
    // volume starts at 0 so nothing pops before the owner sets it
    public static AudioSource Create2DSource(GameObject owner, AudioClip clip, bool loop, float volume = 0f)
    {
        AudioSource source = owner.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;

        return source;
    }

    // a clip that loops seamlessly, even if it doesn't loop cleanly on its own: two copies of the clip play, and near the end of one the other starts again while the first fades out.
    // the fade is equal power (cos and sin), so there is no dip in loudness.
    // the clip has to be longer than a few seconds.
    // used by the wind and the enrage song.
    //   Play(startTime)   starts it, from startTime seconds into the clip
    //   Update(...)       call every frame while it plays: it loops and sets both volumes
    //   Stop()            stops both copies
    public class CrossfadeLoop
    {
        private readonly AudioClip clip;

        // current is the copy that is further into the clip
        private AudioSource current;
        private AudioSource other;

        // the frame Play was called in has nothing to loop yet (a source may not report isPlaying straight away)
        private bool justStarted;

        public CrossfadeLoop(GameObject owner, AudioClip clip)
        {
            this.clip = clip;

            current = Create2DSource(owner, clip, false);
            other = Create2DSource(owner, clip, false);
        }

        // from the very beginning of the clip, or from startTime seconds in.
        // it starts silent: Update sets the volume
        public void Play(float startTime = 0f)
        {
            current.volume = 0f;
            current.Play();

            if (startTime > 0f)
                current.time = startTime;

            justStarted = true;
        }

        public void Stop()
        {
            current.Stop();
            other.Stop();

            current.volume = 0f;
            other.volume = 0f;
        }

        // volume is the overall volume, 0 to 1.
        // crossfadeSeconds is how long the end of the clip fades into the start (cut to half of the part that loops).
        // loopStart is where in the clip the loop goes back to: 0 loops the whole clip, and a later time loops only the end of it
        public void Update(float volume, float crossfadeSeconds, float loopStart = 0f)
        {
            float currentGain = 1f;
            float otherGain = 0f;

            if (!justStarted)
                LoopGains(crossfadeSeconds, loopStart, out currentGain, out otherGain);

            justStarted = false;

            current.volume = volume * currentGain;
            other.volume = volume * otherGain;
        }

        private void LoopGains(float crossfadeSeconds, float loopStart, out float currentGain, out float otherGain)
        {
            currentGain = 1f;
            otherGain = 0f;

            // the copy we were fading out has finished: the other one is now the main one
            if (!current.isPlaying)
            {
                if (other.isPlaying)
                {
                    AudioSource finished = current;
                    current = other;
                    other = finished;
                }
                else
                {
                    current.Play();
                }
            }

            float length = clip.length;
            float crossfade = Mathf.Min(crossfadeSeconds, (length - loopStart) * 0.5f);
            float crossfadeStart = length - crossfade;

            // in the last part of the clip: bring in the loop start on the other copy, fading between them
            if (current.time >= crossfadeStart)
            {
                if (!other.isPlaying)
                {
                    other.Play();
                    other.time = loopStart;
                }

                float progress = Mathf.Clamp01((current.time - crossfadeStart) / crossfade);

                currentGain = Mathf.Cos(progress * Mathf.PI * 0.5f);
                otherGain = Mathf.Sin(progress * Mathf.PI * 0.5f);
            }
        }
    }

    // a song that plays once (no loop), fades in when it starts and fades out when it is stopped.
    // one source for the whole song, so playing it again restarts it from the beginning instead of stacking a second one on top.
    //   Play(...)   starts it, from the beginning
    //   Stop(...)   fades it out from wherever its volume is now, and then stops it. nothing happens if it isn't playing
    //   Update(...) call every frame: it does the fades. the volume and fade times are passed in each time, so a slider dragged during Play mode takes effect straight away
    // used by the run music in MusicPlayer, and by any other song that just starts and stops
    public class FadedSong
    {
        private readonly AudioSource source;

        // false until the first Play, so nothing touches the volume before there is a song
        private bool started;

        // when it started, for the fade-in
        private float startTime;

        // for the fade-out: whether it is happening, when it began, and the volume it started from
        private bool fadingOut;
        private float fadeOutStartTime;
        private float fadeOutStartVolume;

        public FadedSong(GameObject owner)
        {
            source = Create2DSource(owner, null, false, 1f);
        }

        // a clip of null does nothing.
        // a Play in the middle of a fade-out cancels it
        public void Play(AudioClip clip, float volume, float fadeInSeconds)
        {
            if (clip == null)
                return;

            started = true;
            fadingOut = false;

            source.Stop();
            source.clip = clip;
            source.volume = fadeInSeconds > 0f ? 0f : volume;
            source.Play();

            startTime = Time.unscaledTime;
        }

        // 0 stops it at once
        public void Stop(float fadeOutSeconds)
        {
            if (!started || !source.isPlaying)
                return;

            if (fadeOutSeconds <= 0f)
            {
                source.Stop();
                return;
            }

            fadingOut = true;
            fadeOutStartTime = Time.unscaledTime;
            fadeOutStartVolume = source.volume;
        }

        // the fade-out: volume falls in a straight line to 0 over fadeOutSeconds, then the song stops.
        // the fade-in: volume rises in a straight line from 0 to volume over fadeInSeconds
        public void Update(float volume, float fadeInSeconds, float fadeOutSeconds)
        {
            if (!started)
                return;

            if (fadingOut)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - fadeOutStartTime) / Mathf.Max(0.0001f, fadeOutSeconds));
                source.volume = Mathf.Lerp(fadeOutStartVolume, 0f, progress);

                if (progress >= 1f)
                {
                    source.Stop();
                    fadingOut = false;
                }

                return;
            }

            float fade = fadeInSeconds > 0f ? Mathf.Clamp01((Time.unscaledTime - startTime) / fadeInSeconds) : 1f;
            source.volume = volume * fade;
        }
    }

    // a sound that follows a level from 0 to 1: it plays while the level is above 0, its volume is level * maxVolume, and it stops when the level is back at 0.
    // used by the warnings, where the louder the words are on the screen, the louder the sound.
    // with no clip it does nothing
    public class LevelSound
    {
        private readonly AudioSource source;

        public LevelSound(GameObject owner, AudioClip clip, bool loop)
        {
            if (clip != null)
                source = Create2DSource(owner, clip, loop);
        }

        // call every frame
        public void Set(float level, float maxVolume)
        {
            if (source == null)
                return;

            if (level <= 0f)
            {
                if (source.isPlaying)
                    source.Stop();

                return;
            }

            if (!source.isPlaying)
                source.Play();

            source.volume = level * maxVolume;
        }

        // for when the owner switches off while the sound is playing
        public void Stop()
        {
            if (source != null)
                source.Stop();
        }
    }
}
