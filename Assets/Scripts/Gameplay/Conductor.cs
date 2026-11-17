using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Conductor : MonoBehaviour
{
    const double MillisecondsPerSecond = 1000.0;
    const double DriftSnapSeconds = 0.08;
    const double DriftSmoothing = 0.05;
    const float PreviewEndMargin = 1f;

    [Tooltip("Compensacion de latencia en ms. Si siempre golpeas tarde, sube este valor.")]
    public float latencyMs;

    public AudioSource Source { get; private set; }
    public bool IsPlaying { get; private set; }
    public float Duration => Source.clip != null ? Source.clip.length : 0f;
    public double SongTime => TimeAt(GameClock.Now);

    double dspStart;
    double clockStart;

    void Awake()
    {
        Source = GetComponent<AudioSource>();
        Source.playOnAwake = false;
    }

    public void Play(AudioClip clip, double delay)
    {
        Source.Stop();
        Source.loop = false;
        Source.clip = clip;
        Source.time = 0f;
        dspStart = AudioSettings.dspTime + delay;
        clockStart = GameClock.Now + delay;
        if (clip != null) Source.PlayScheduled(dspStart);
        IsPlaying = true;
    }

    public void PlayPreview(AudioClip clip, float startTime)
    {
        IsPlaying = false;
        Source.Stop();
        if (clip == null) return;

        Source.clip = clip;
        Source.loop = true;
        Source.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - PreviewEndMargin));
        Source.Play();
    }

    public void Stop()
    {
        Source.Stop();
        IsPlaying = false;
    }

    public double TimeAt(double clockTime) => clockTime - clockStart - latencyMs / MillisecondsPerSecond;

    void Update()
    {
        if (!IsPlaying || Source.clip == null || !AudioHasStarted()) return;

        // La posicion real del audio manda: si el audio se traba, las notas lo esperan.
        double audioTime = Source.timeSamples / (double)Source.clip.frequency;
        double drift = audioTime - (GameClock.Now - clockStart);
        clockStart -= Math.Abs(drift) > DriftSnapSeconds ? drift : drift * DriftSmoothing;
    }

    bool AudioHasStarted() => AudioSettings.dspTime >= dspStart && Source.isPlaying && Source.timeSamples > 0;
}
