using System;
using UnityEngine;

/// <summary>
/// Lleva el tiempo de la cancion sincronizado con el audio (AudioSettings.dspTime),
/// usando el mismo reloj que marca los eventos del Arduino.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class Conductor : MonoBehaviour
{
    [Tooltip("Compensacion de latencia en ms. Si siempre golpeas tarde, sube este valor.")]
    public float latenciaMs = 0f;

    public AudioSource Fuente { get; private set; }
    public bool Sonando { get; private set; }
    public float Duracion => Fuente.clip != null ? Fuente.clip.length : 0f;

    double dspInicio;
    double relojInicio;

    void Awake()
    {
        Fuente = GetComponent<AudioSource>();
        Fuente.playOnAwake = false;
    }

    /// <summary>Programa la cancion para que empiece dentro de "espera" segundos.</summary>
    public void Iniciar(AudioClip clip, double espera)
    {
        Fuente.Stop();
        Fuente.loop = false;
        Fuente.clip = clip;
        Fuente.time = 0f;
        dspInicio = AudioSettings.dspTime + espera;
        relojInicio = ArduinoInput.Ahora + espera;
        if (clip != null) Fuente.PlayScheduled(dspInicio);
        Sonando = true;
    }

    public void Preview(AudioClip clip, float desde)
    {
        Sonando = false;
        Fuente.Stop();
        if (clip == null) return;
        Fuente.clip = clip;
        Fuente.loop = true;
        Fuente.time = Mathf.Clamp(desde, 0f, Mathf.Max(0f, clip.length - 1f));
        Fuente.Play();
    }

    public void Detener()
    {
        Fuente.Stop();
        Sonando = false;
    }

    /// <summary>Tiempo de cancion (ya compensado por latencia) para un instante del reloj.</summary>
    public double TiempoEn(double reloj) => (reloj - relojInicio) - latenciaMs / 1000.0;
    public double Tiempo => TiempoEn(ArduinoInput.Ahora);

    void Update()
    {
        if (!Sonando || Fuente.clip == null) return;
        // Corrige poco a poco la deriva entre el reloj y el audio real
        double dspT = AudioSettings.dspTime - dspInicio;
        if (dspT <= 0) return;
        double relT = ArduinoInput.Ahora - relojInicio;
        double error = dspT - relT;
        if (Math.Abs(error) > 0.1) relojInicio -= error;
        else relojInicio -= error * 0.02;
    }
}
