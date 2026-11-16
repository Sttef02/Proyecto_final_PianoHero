using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Puntaje durante el juego y pantalla de resultados, hechos en Canvas con la fuente del menu (BoldPixels).
/// Se construye sola por codigo; GameFlow le pasa los datos cada frame.
/// Los textos van en MAYUSCULAS y sin tildes porque la fuente pixel no trae esos caracteres.
/// </summary>
public class PianoHeroUI : MonoBehaviour
{
    [Header("Estilo")]
    public TMP_FontAsset fuente;
    public Color acento = new Color(1f, 0.6f, 0.1f);
    public Color fondoPanel = new Color(0.04f, 0.04f, 0.08f, 0.88f);
    public Color bordeNormal = new Color(0.22f, 0.22f, 0.32f, 1f);
    public Color textoSecundario = new Color(0.66f, 0.66f, 0.78f, 1f);

    public static readonly Color ColorFacil = new Color(0.24f, 0.86f, 0.52f);
    public static readonly Color ColorMedia = new Color(1f, 0.76f, 0.2f);
    public static readonly Color ColorExperto = new Color(1f, 0.3f, 0.3f);

    Color[] coloresCarril;
    Sprite redondeado;
    RectTransform raiz;

    // ---- Juego
    RectTransform gHud, barraProgreso, txtPopupRT, txtCuentaRT, txtAvisoRT;
    TextMeshProUGUI txtPuntos, txtRacha, txtMulti, txtTitulo, txtDifHud, txtPopup, txtCuenta, txtAviso;
    Image fondoMulti;
    readonly System.Collections.Generic.List<Image> pips = new System.Collections.Generic.List<Image>();
    float popupDesde = -10f, avisoDesde = -10f;

    // ---- Resultados
    RectTransform gResultados, letraRT;
    TextMeshProUGUI txtResTitulo, txtResDif, txtLetra, txtResPuntos, txtResStats, txtRecordNuevo, txtResPie;
    float resDesde, resPuntosObjetivo;
    bool resRecord, enResultados;

    // =====================================================================================
    //  Construccion
    // =====================================================================================

    public void Construir(TMP_FontAsset f, Color[] carriles)
    {
        if (f != null) fuente = f;
        if (fuente == null) fuente = TMP_Settings.defaultFontAsset;
        coloresCarril = carriles ?? new[] { Color.green, Color.red, Color.yellow, Color.blue };
        redondeado = CrearSpriteRedondeado(64, 22);

        var go = new GameObject("PianoHeroUI", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        raiz = (RectTransform)go.transform;

        ConstruirHud();
        ConstruirResultados();
        Ocultar();
    }

    void ConstruirHud()
    {
        gHud = Grupo("Juego");

        // Tarjeta de puntaje (arriba izquierda)
        var p = Panel(gHud, "Puntaje", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                      new Vector2(28, -28), new Vector2(460, 196), fondoPanel, bordeNormal);
        var lbl = Texto(p, "Lbl", "PUNTOS", 22, textoSecundario, TextAlignmentOptions.TopLeft);
        Anclar(lbl.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(26, -16), new Vector2(-40, 30));
        txtPuntos = Texto(p, "Puntos", "0", 64, Color.white, TextAlignmentOptions.TopLeft);
        Anclar(txtPuntos.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -44), new Vector2(-40, 72));
        txtRacha = Texto(p, "Racha", "RACHA 0", 28, Color.white, TextAlignmentOptions.TopLeft);
        Anclar(txtRacha.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -122), new Vector2(260, 36));
        for (int i = 0; i < 4; i++)
        {
            var pip = Caja(p, "Led" + i, new Color(1, 1, 1, 0.15f));
            Anclar(pip.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26 + i * 62, -162), new Vector2(54, 16));
            pips.Add(pip);
        }
        fondoMulti = Caja(p, "Multiplicador", acento);
        Anclar(fondoMulti.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 20), new Vector2(120, 84));
        txtMulti = Texto(fondoMulti.rectTransform, "T", "X1", 50, Color.black, TextAlignmentOptions.Center);

        // Cancion (arriba derecha)
        var c = Panel(gHud, "Cancion", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                      new Vector2(-28, -28), new Vector2(620, 112), fondoPanel, bordeNormal);
        txtTitulo = Texto(c, "Titulo", "", 32, Color.white, TextAlignmentOptions.Right);
        Anclar(txtTitulo.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -14), new Vector2(-48, 44));
        txtDifHud = Texto(c, "Dificultad", "", 26, ColorMedia, TextAlignmentOptions.Right);
        Anclar(txtDifHud.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -62), new Vector2(-48, 34));

        // Progreso de la cancion (abajo)
        var fondoBarra = Caja(gHud, "Progreso", new Color(1, 1, 1, 0.12f), false);
        Anclar(fondoBarra.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 10));
        var relleno = Caja(fondoBarra.rectTransform, "Relleno", acento, false);
        barraProgreso = relleno.rectTransform;
        barraProgreso.anchorMin = Vector2.zero; barraProgreso.anchorMax = new Vector2(0, 1);
        barraProgreso.offsetMin = barraProgreso.offsetMax = Vector2.zero;

        // Textos del centro: PERFECTO / BIEN / FALLO, avisos y cuenta regresiva
        txtPopup = Texto(gHud, "Juicio", "", 92, Color.white, TextAlignmentOptions.Center);
        txtPopupRT = txtPopup.rectTransform;
        Anclar(txtPopupRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1000, 120));
        Contorno(txtPopup, 0.25f);
        txtAviso = Texto(gHud, "Aviso", "", 48, acento, TextAlignmentOptions.Center);
        txtAvisoRT = txtAviso.rectTransform;
        Anclar(txtAvisoRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(1100, 70));
        Contorno(txtAviso, 0.25f);
        txtCuenta = Texto(gHud, "Cuenta", "", 230, Color.white, TextAlignmentOptions.Center);
        txtCuentaRT = txtCuenta.rectTransform;
        Anclar(txtCuentaRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(800, 300));
        Contorno(txtCuenta, 0.3f);
    }

    void ConstruirResultados()
    {
        gResultados = Grupo("Resultados");
        var oscuro = Caja(gResultados, "Oscuro", new Color(0, 0, 0, 0.72f), false);
        Estirar(oscuro.rectTransform, 0, 0, 0, 0);
        var p = Panel(gResultados, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      Vector2.zero, new Vector2(1160, 860), fondoPanel, acento);
        txtResTitulo = Texto(p, "Titulo", "", 54, Color.white, TextAlignmentOptions.Center);
        Anclar(txtResTitulo.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(-80, 70));
        txtResDif = Texto(p, "Dificultad", "", 30, ColorMedia, TextAlignmentOptions.Center);
        Anclar(txtResDif.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(-80, 40));

        txtLetra = Texto(p, "Letra", "A", 280, acento, TextAlignmentOptions.Center);
        letraRT = txtLetra.rectTransform;
        Anclar(letraRT, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(250, -380), new Vector2(360, 340));
        Contorno(txtLetra, 0.2f);

        txtResPuntos = Texto(p, "Puntos", "0", 96, Color.white, TextAlignmentOptions.Left);
        Anclar(txtResPuntos.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(470, -190), new Vector2(-510, 110));
        txtRecordNuevo = Texto(p, "Record", "NUEVO RECORD!", 36, ColorMedia, TextAlignmentOptions.Left);
        Anclar(txtRecordNuevo.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(474, -300), new Vector2(-510, 46));
        txtResStats = Texto(p, "Estadisticas", "", 32, Color.white, TextAlignmentOptions.TopLeft);
        Anclar(txtResStats.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(474, -370), new Vector2(-510, 330));
        txtResStats.lineSpacing = 18;

        txtResPie = Texto(p, "Pie", "B1  O  CLIC  PARA VOLVER AL MENU", 28, textoSecundario, TextAlignmentOptions.Center);
        Anclar(txtResPie.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(-80, 40));
    }

    // =====================================================================================
    //  API para GameFlow
    // =====================================================================================

    public void Ocultar()
    {
        gHud.gameObject.SetActive(false);
        gResultados.gameObject.SetActive(false);
        enResultados = false;
    }

    public void MostrarJuego()
    {
        gHud.gameObject.SetActive(true);
        gResultados.gameObject.SetActive(false);
        enResultados = false;
        popupDesde = avisoDesde = -10f;
    }

    /// <summary>leds: 0-4 encendidos (espejo de los LEDs del control); estrella = los 4 parpadeando.</summary>
    public void Hud(float puntos, int racha, int mult, int leds, bool estrella, string titulo, string dif, Color difColor,
                    float progreso, double tiempo)
    {
        txtPuntos.text = Mathf.RoundToInt(puntos).ToString("N0");
        txtRacha.text = "RACHA " + racha;
        txtMulti.text = "X" + mult;
        fondoMulti.color = mult >= 4 ? ColorExperto : mult == 3 ? ColorMedia : mult == 2 ? ColorFacil : new Color(0.8f, 0.8f, 0.85f);
        for (int i = 0; i < pips.Count; i++)
        {
            bool on = estrella ? (Time.unscaledTime * 8f % 2f) < 1f : i < leds;
            pips[i].color = on ? coloresCarril[i % coloresCarril.Length] : new Color(1, 1, 1, 0.15f);
        }
        txtTitulo.text = Limpiar(titulo);
        txtDifHud.text = Limpiar(dif);
        txtDifHud.color = difColor;
        barraProgreso.anchorMax = new Vector2(Mathf.Clamp01(progreso), 1);

        // Cuenta regresiva: 3, 2, 1, YA!
        if (tiempo < 0)
        {
            int n = Mathf.CeilToInt((float)-tiempo);
            txtCuenta.text = n.ToString();
            txtCuenta.color = Color.white;
            float f = (float)(-tiempo) - (n - 1);           // 1 -> 0 en cada numero
            txtCuentaRT.localScale = Vector3.one * (1f + 0.35f * f);
            txtCuenta.alpha = Mathf.Clamp01(0.2f + f);
        }
        else if (tiempo < 0.6)
        {
            txtCuenta.text = "YA!";
            txtCuenta.color = acento;
            txtCuentaRT.localScale = Vector3.one * (1f + (float)tiempo * 0.4f);
            txtCuenta.alpha = 1f - (float)tiempo / 0.6f;
        }
        else txtCuenta.text = "";
    }

    public void Popup(string texto, Color color)
    {
        txtPopup.text = Limpiar(texto);
        txtPopup.color = color;
        popupDesde = Time.unscaledTime;
    }

    public void Aviso(string texto, Color color)
    {
        txtAviso.text = Limpiar(texto);
        txtAviso.color = color;
        avisoDesde = Time.unscaledTime;
    }

    public void Resultados(string titulo, string dif, Color difColor, float puntos, int perfectos, int buenos, int fallos,
                           int rachaMax, bool record)
    {
        int total = perfectos + buenos + fallos;
        float precision = total > 0 ? (perfectos + buenos) * 100f / total : 0f;
        string letra = precision >= 95 ? "S" : precision >= 85 ? "A" : precision >= 70 ? "B" : precision >= 50 ? "C" : "D";
        Color cLetra = letra == "S" ? ColorMedia : letra == "A" ? ColorFacil : letra == "B" ? acento
                     : letra == "C" ? new Color(0.6f, 0.7f, 1f) : ColorExperto;

        txtResTitulo.text = Limpiar(titulo);
        txtResDif.text = Limpiar(dif);
        txtResDif.color = difColor;
        txtLetra.text = letra;
        txtLetra.color = cLetra;
        resPuntosObjetivo = puntos;
        resRecord = record;
        txtResStats.text =
            $"PERFECTO      {perfectos}\n" +
            $"BIEN          {buenos}\n" +
            $"FALLO         {fallos}\n" +
            $"RACHA MAXIMA  {rachaMax}\n" +
            $"PRECISION     {precision:0.0}%";

        gHud.gameObject.SetActive(false);
        gResultados.gameObject.SetActive(true);
        enResultados = true;
        resDesde = Time.unscaledTime;
    }

    // =====================================================================================
    //  Animaciones
    // =====================================================================================

    void Update()
    {
        if (raiz == null) return;
        float ahora = Time.unscaledTime;

        // PERFECTO / BIEN / FALLO: aparece grande, se encoge y se desvanece
        float dp = ahora - popupDesde;
        if (dp < 0.5f)
        {
            txtPopupRT.localScale = Vector3.one * (dp < 0.12f ? Mathf.Lerp(1.45f, 1f, dp / 0.12f) : 1f);
            txtPopup.alpha = dp < 0.3f ? 1f : 1f - (dp - 0.3f) / 0.2f;
        }
        else txtPopup.alpha = 0f;

        float da = ahora - avisoDesde;
        txtAviso.alpha = da < 1.2f ? (da < 0.9f ? 1f : 1f - (da - 0.9f) / 0.3f) : 0f;
        txtAvisoRT.localScale = Vector3.one * (da < 0.15f ? Mathf.Lerp(1.3f, 1f, da / 0.15f) : 1f);

        // Resultados: los puntos suben, la letra aparece con rebote, el record parpadea
        if (enResultados)
        {
            float dr = ahora - resDesde;
            float k = Mathf.Clamp01(dr / 1.2f);
            k = 1f - (1f - k) * (1f - k);
            txtResPuntos.text = Mathf.RoundToInt(resPuntosObjetivo * k).ToString("N0");
            float kl = Mathf.Clamp01((dr - 1.0f) / 0.25f);
            letraRT.localScale = Vector3.one * (kl <= 0 ? 0f : kl < 1 ? Mathf.Lerp(1.6f, 1f, kl) : 1f);
            txtRecordNuevo.gameObject.SetActive(resRecord && dr > 1.2f && (ahora * 3f % 2f) < 1.4f);
            txtResPie.alpha = dr > 1.4f ? 1f : 0f;
        }
    }

    // =====================================================================================
    //  Ayudantes
    // =====================================================================================

    RectTransform Panel(Transform padre, string nombre, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 tam,
                        Color fondo, Color borde)
    {
        var rt = Nuevo(nombre, padre);
        Anclar(rt, aMin, aMax, pivot, pos, tam);
        var b = Caja(rt, "Borde", borde);
        Estirar(b.rectTransform, -3, -3, -3, -3);
        var f = Caja(rt, "Fondo", fondo);
        Estirar(f.rectTransform, 0, 0, 0, 0);
        return rt;
    }

    RectTransform Grupo(string nombre)
    {
        var g = Nuevo(nombre, raiz);
        Estirar(g, 0, 0, 0, 0);
        return g;
    }

    static RectTransform Nuevo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    Image Caja(Transform padre, string nombre, Color color, bool redondo = true)
    {
        var rt = Nuevo(nombre, padre);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (redondo && redondeado != null)
        {
            img.sprite = redondeado;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.6f;
        }
        return img;
    }

    TextMeshProUGUI Texto(Transform padre, string nombre, string texto, float tam, Color color, TextAlignmentOptions alin)
    {
        var rt = Nuevo(nombre, padre);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.text = texto;
        t.fontSize = tam;
        t.color = color;
        t.alignment = alin;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        Estirar(rt, 0, 0, 0, 0);
        return t;
    }

    static void Contorno(TextMeshProUGUI t, float ancho)
    {
        t.outlineWidth = ancho;
        t.outlineColor = new Color32(0, 0, 0, 255);
    }

    static void Anclar(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 tam)
    {
        r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot;
        r.anchoredPosition = pos; r.sizeDelta = tam;
    }

    static void Estirar(RectTransform r, float izq, float der, float abajo, float arriba)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(izq, abajo);
        r.offsetMax = new Vector2(-der, -arriba);
    }

    /// <summary>Quita tildes y signos que la fuente pixel no tiene, y pasa a mayusculas.</summary>
    public static string Limpiar(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s.Normalize(System.Text.NormalizationForm.FormD))
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (c == '¡' || c == '¿') continue;
            sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToUpperInvariant();
    }

    static Sprite CrearSpriteRedondeado(int tam, int radio)
    {
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[tam * tam];
        for (int y = 0; y < tam; y++)
            for (int x = 0; x < tam; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radio, tam - radio);
                float cy = Mathf.Clamp(y + 0.5f, radio, tam - radio);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                px[y * tam + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(radio - d + 0.5f) * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(radio, radio, radio, radio));
    }
}
