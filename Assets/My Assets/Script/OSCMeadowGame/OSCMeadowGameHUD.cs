using UnityEngine;

[DisallowMultipleComponent]
public class OSCMeadowGameHUD : MonoBehaviour
{
    public OSCCowController cow;
    public OSCUFOController ufo;
    public OSCAlienController alien;
    public OSCMeadowGameManager gameManager;

    [Header("Responsive UI Scaling")]
    [Tooltip("Resolution at which the HUD uses scale 1.")]
    public Vector2 referenceResolution = new Vector2(1280f, 720f);
    [Range(0.5f, 1f)] public float minimumScale = 0.72f;
    [Range(1f, 4f)] public float maximumScale = 3f;

    private GUIStyle leftNameStyle;
    private GUIStyle centerNameStyle;
    private GUIStyle rightNameStyle;
    private GUIStyle leftValueStyle;
    private GUIStyle centerValueStyle;
    private GUIStyle rightValueStyle;
    private GUIStyle timerStyle;
    private GUIStyle winnerStyle;
    private GUIStyle finalScoreStyle;
    private GUIStyle menuTitleStyle;
    private GUIStyle menuLabelStyle;
    private GUIStyle menuButtonStyle;
    private GUIStyle timeInputStyle;

    private int selectedDuration = 90;
    private string durationInput = "90";
    private static readonly Color NeonPink = new Color(1f, 0.015f, 0.40f);
    private static readonly Color NeonCyan = new Color(0.02f, 1f, 1f);
    private static readonly Color NeonYellow = new Color(0.94f, 1f, 0.015f);
    private static readonly Color NeonPurple = new Color(0.65f, 0.015f, 1f);

    private void Start()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<OSCMeadowGameManager>();
        if (cow == null) cow = FindFirstObjectByType<OSCCowController>();
        if (ufo == null) ufo = FindFirstObjectByType<OSCUFOController>();
        if (alien == null) alien = FindFirstObjectByType<OSCAlienController>();

        if (gameManager != null)
        {
            selectedDuration = Mathf.RoundToInt(gameManager.matchDurationSeconds);
            durationInput = selectedDuration.ToString();
        }
    }

    private void EnsureStyles(float scale)
    {
        int nameSize = Mathf.Max(11, Mathf.RoundToInt(14f * scale));
        int valueSize = Mathf.Max(10, Mathf.RoundToInt(12f * scale));

        if (leftNameStyle == null)
        {
            leftNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            rightNameStyle = new GUIStyle(leftNameStyle)
            {
                alignment = TextAnchor.MiddleRight
            };
            centerNameStyle = new GUIStyle(leftNameStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };
            leftValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.94f, 0.97f, 1f, 1f) }
            };
            rightValueStyle = new GUIStyle(leftValueStyle)
            {
                alignment = TextAnchor.MiddleRight
            };
            centerValueStyle = new GUIStyle(leftValueStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };
            timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            winnerStyle = new GUIStyle(timerStyle);
            finalScoreStyle = new GUIStyle(timerStyle)
            {
                normal = { textColor = new Color(0.88f, 0.95f, 1f, 1f) }
            };
            menuTitleStyle = new GUIStyle(timerStyle);
            menuLabelStyle = new GUIStyle(timerStyle)
            {
                normal = { textColor = new Color(0.88f, 0.95f, 1f, 1f) }
            };
            menuButtonStyle = new GUIStyle()
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            timeInputStyle = new GUIStyle()
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            StyleControl(menuButtonStyle, NeonYellow);
            StyleControl(timeInputStyle, NeonCyan);
            winnerStyle.fontStyle = FontStyle.Bold;
            winnerStyle.normal.textColor = NeonPink;
            finalScoreStyle.fontStyle = FontStyle.Bold;
            finalScoreStyle.normal.textColor = NeonCyan;
            menuTitleStyle.fontStyle = FontStyle.Bold;
            menuTitleStyle.normal.textColor = NeonPink;
            menuLabelStyle.fontStyle = FontStyle.Bold;
            menuLabelStyle.normal.textColor = NeonYellow;
        }

        leftNameStyle.fontSize = nameSize;
        centerNameStyle.fontSize = nameSize;
        rightNameStyle.fontSize = nameSize;
        leftValueStyle.fontSize = valueSize;
        centerValueStyle.fontSize = valueSize;
        rightValueStyle.fontSize = valueSize;
        timerStyle.fontSize = Mathf.Max(16, Mathf.RoundToInt(22f * scale));
        winnerStyle.fontSize = Mathf.Max(34, Mathf.RoundToInt(50f * scale));
        finalScoreStyle.fontSize = Mathf.Max(20, Mathf.RoundToInt(28f * scale));
        menuTitleStyle.fontSize = Mathf.Max(30, Mathf.RoundToInt(44f * scale));
        menuLabelStyle.fontSize = Mathf.Max(15, Mathf.RoundToInt(20f * scale));
        menuButtonStyle.fontSize = Mathf.Max(14, Mathf.RoundToInt(19f * scale));
        timeInputStyle.fontSize = Mathf.Max(18, Mathf.RoundToInt(25f * scale));
        leftNameStyle.normal.textColor = NeonYellow;
        centerNameStyle.normal.textColor = NeonYellow;
        rightNameStyle.normal.textColor = NeonYellow;
        timerStyle.normal.textColor = NeonPink;
        timerStyle.fontStyle = FontStyle.Bold;
    }

    private void OnGUI()
    {
        float scale = CalculateUIScale();
        EnsureStyles(scale);

        if (gameManager != null &&
            !gameManager.IsMatchStarted &&
            !gameManager.IsMatchOver)
        {
            DrawStartPage(scale);
            return;
        }

        if (gameManager != null && gameManager.IsMatchOver)
        {
            DrawWinnerPage(scale);
            return;
        }

        DrawTimer(scale);
        DrawPlayerHUD(scale);
    }

    private float CalculateUIScale()
    {
        float referenceWidth = Mathf.Max(1f, referenceResolution.x);
        float referenceHeight = Mathf.Max(1f, referenceResolution.y);
        float widthScale = Screen.width / referenceWidth;
        float heightScale = Screen.height / referenceHeight;
        return Mathf.Clamp(Mathf.Min(widthScale, heightScale), minimumScale, maximumScale);
    }

    private void DrawStartPage(float scale)
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.38f);
        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture);
        GUI.color = oldColor;

        float centerX = Screen.width * 0.5f;
        float titleY = Screen.height * 0.22f;
        DrawNeonFrame(new Rect(centerX - 215f * scale, titleY - 12f * scale,
            430f * scale, 362f * scale), NeonPurple, NeonCyan, 2f * scale);
        DrawText(new Rect(0f, titleY, Screen.width, 60f * scale),
            "MATCH READY", menuTitleStyle);
        DrawText(new Rect(0f, titleY + 72f * scale,
            Screen.width, 34f * scale),
            "MATCH TIME  (SECONDS)", menuLabelStyle);

        float controlY = titleY + 118f * scale;
        float buttonWidth = 92f * scale;
        float inputWidth = 118f * scale;
        float height = 46f * scale;
        float gap = 10f * scale;
        float totalWidth = buttonWidth * 2f + inputWidth + gap * 2f;
        float startX = centerX - totalWidth * 0.5f;

        if (NeonButton(new Rect(startX, controlY, buttonWidth, height),
            "- 30s", menuButtonStyle))
        {
            selectedDuration = Mathf.Max(5, selectedDuration - 30);
            durationInput = selectedDuration.ToString();
        }

        Rect inputRect = new Rect(startX + buttonWidth + gap, controlY, inputWidth, height);
        DrawNeonFrame(inputRect, NeonCyan, NeonPurple, 2f * scale);
        durationInput = GUI.TextField(
            inputRect,
            durationInput,
            4,
            timeInputStyle);
        if (int.TryParse(durationInput, out int typedDuration))
            selectedDuration = Mathf.Clamp(typedDuration, 5, 3600);

        if (NeonButton(
            new Rect(startX + buttonWidth + gap + inputWidth + gap,
                controlY, buttonWidth, height),
            "+ 30s", menuButtonStyle))
        {
            selectedDuration = Mathf.Min(3600, selectedDuration + 30);
            durationInput = selectedDuration.ToString();
        }

        int minutes = selectedDuration / 60;
        int seconds = selectedDuration % 60;
        DrawText(new Rect(0f, controlY + 56f * scale,
            Screen.width, 32f * scale),
            minutes.ToString("00") + ":" + seconds.ToString("00"),
            menuLabelStyle);

        float startWidth = 260f * scale;
        if (NeonButton(
            new Rect(centerX - startWidth * 0.5f,
                controlY + 112f * scale,
                startWidth, 58f * scale),
            "START GAME",
            menuButtonStyle))
        {
            gameManager.BeginMatch(selectedDuration);
        }
    }

    private void DrawPlayerHUD(float scale)
    {
        float margin = 14f * scale;
        float width = 158f * scale;
        float lineHeight = 17f * scale;
        float barHeight = 2f * scale;
        float gap = 3f * scale;
        float panelHeight = lineHeight * 3f + barHeight * 2f + gap * 2f;
        float bottomY = Screen.height - margin - panelHeight;
        // Keep all three original anchors and panel extents; skin only their contents.
        if (cow != null) DrawPanelSkin(new Rect(margin, bottomY, width, panelHeight), scale);
        if (ufo != null) DrawPanelSkin(new Rect(Screen.width - margin - width, bottomY, width, panelHeight), scale);
        if (alien != null) DrawPanelSkin(new Rect((Screen.width - width) * 0.5f, bottomY, width, panelHeight), scale);
        int cowScore = gameManager != null ? gameManager.CowScore : 0;
        int ufoScore = gameManager != null ? gameManager.UfoScore : 0;
        int alienScore = gameManager != null ? gameManager.AlienScore : 0;

        if (cow != null)
        {
            float x = margin;
            float y = bottomY;
            DrawText(new Rect(x, y, width, lineHeight),
                "COW // " + cowScore, leftNameStyle);
            y += lineHeight;
            DrawValueLine(x, y, width, lineHeight, barHeight,
                "HP  " + Mathf.CeilToInt(cow.Health),
                cow.Health / cow.maximumHealth,
                new Color(0.32f, 0.9f, 0.42f, 1f),
                leftValueStyle);
            y += lineHeight + barHeight + gap;
            DrawValueLine(x, y, width, lineHeight, barHeight,
                "MILK  " + Mathf.CeilToInt(cow.Milk),
                cow.Milk / cow.maximumMilk,
                new Color(0.73f, 0.9f, 1f, 1f),
                leftValueStyle);
        }

        if (ufo != null)
        {
            float x = Screen.width - margin - width;
            float y = bottomY;
            DrawText(new Rect(x, y, width, lineHeight),
                "UFO // " + ufoScore, rightNameStyle);
            y += lineHeight;
            DrawValueLine(x, y, width, lineHeight, barHeight,
                "HP  " + Mathf.CeilToInt(ufo.Health),
                ufo.Health / ufo.maximumHealth,
                new Color(0.76f, 0.48f, 1f, 1f),
                rightValueStyle);
            y += lineHeight + barHeight + gap;
            DrawValueLine(x, y, width, lineHeight, barHeight,
                "HEAT  " + Mathf.CeilToInt(ufo.Heat),
                ufo.Heat / 100f,
                ufo.IsOverheated
                    ? new Color(1f, 0.2f, 0.08f, 1f)
                    : new Color(1f, 0.58f, 0.15f, 1f),
                rightValueStyle);
        }

        if (alien != null)
        {
            float x = (Screen.width - width) * 0.5f;
            float y = bottomY;
            DrawText(new Rect(x, y, width, lineHeight),
                "ALIEN // " + alienScore, centerNameStyle);
            y += lineHeight;
            DrawValueLine(x, y, width, lineHeight, barHeight,
                "HP  " + Mathf.CeilToInt(alien.Health),
                alien.Health / alien.maximumHealth,
                new Color(0.38f, 1f, 0.42f, 1f),
                centerValueStyle);
            y += lineHeight + barHeight + gap;
            DrawValueLine(x, y, width, lineHeight, barHeight,
                "STAMINA  " + Mathf.CeilToInt(alien.Stamina),
                alien.Stamina / alien.maximumStamina,
                new Color(0.95f, 0.86f, 0.26f, 1f),
                centerValueStyle);
        }
    }

    private void DrawTimer(float scale)
    {
        if (gameManager == null) return;
        int totalSeconds = Mathf.CeilToInt(gameManager.RemainingTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        string value = minutes.ToString("00") + ":" + seconds.ToString("00");
        float width = 130f * scale;
        float bottomHudHeight = (17f * 3f + 4f * 2f + 3f * 2f) * scale;
        float y = Screen.height - 14f * scale - bottomHudHeight - 32f * scale;
        DrawNeonFrame(new Rect((Screen.width - width) * 0.5f, y, width, 28f * scale), NeonPink, NeonCyan, Mathf.Max(1f, scale));
        DrawText(new Rect((Screen.width - width) * 0.5f,
            y, width, 28f * scale), value, timerStyle);
    }

    private void DrawWinnerPage(float scale)
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.42f);
        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture);
        GUI.color = oldColor;

        string title;
        string score;
        if (gameManager.Winner == OSCMatchWinner.Cow)
        {
            title = "COW WINS";
            score = "SCORE  " + gameManager.CowScore;
        }
        else if (gameManager.Winner == OSCMatchWinner.UFO)
        {
            title = "UFO WINS";
            score = "SCORE  " + gameManager.UfoScore;
        }
        else if (gameManager.Winner == OSCMatchWinner.Alien)
        {
            title = "ALIEN WINS";
            score = "SCORE  " + gameManager.AlienScore;
        }
        else
        {
            title = "DRAW";
            score = "COW  " + gameManager.CowScore +
                    "     UFO  " + gameManager.UfoScore +
                    "     ALIEN  " + gameManager.AlienScore;
        }

        float centerY = Screen.height * 0.68f;
        float resultWidth = Mathf.Min(Screen.width - 28f * scale, 620f * scale);
        DrawNeonFrame(new Rect((Screen.width - resultWidth) * 0.5f,
            centerY, resultWidth, 70f * scale), NeonPink, NeonPurple, 2f * scale);
        DrawNeonFrame(new Rect((Screen.width - resultWidth) * 0.5f,
            centerY + 72f * scale, resultWidth, 36f * scale), NeonYellow, NeonCyan, scale);
        DrawText(new Rect(0f, centerY, Screen.width, 70f * scale),
            title, winnerStyle);
        DrawText(new Rect(0f, centerY + 64f * scale,
            Screen.width, 42f * scale), score, finalScoreStyle);

        float buttonWidth = 220f * scale;
        float buttonHeight = 50f * scale;
        float buttonY = centerY + 122f * scale;
        if (NeonButton(
            new Rect((Screen.width - buttonWidth) * 0.5f,
                buttonY, buttonWidth, buttonHeight),
            "PLAY AGAIN",
            menuButtonStyle))
        {
            durationInput = selectedDuration.ToString();
            gameManager.ResetMatch();
        }
    }

    private static void DrawValueLine(
        float x, float y, float width, float lineHeight,
        float barHeight, string label, float value,
        Color color, GUIStyle style)
    {
        bool health = label.StartsWith("HP");
        Color fill = health ? NeonPink : NeonCyan;
        if (label.StartsWith("HEAT") && value >= 0.95f) fill = NeonYellow;
        float unit = Mathf.Max(1f, lineHeight / 17f);
        float height = lineHeight + barHeight;
        float badge = height;
        DrawNeonFrame(new Rect(x, y, badge, height), NeonYellow, NeonPurple, unit);
        DrawPixelIcon(new Rect(x + 4f * unit, y + 4f * unit,
            badge - 8f * unit, height - 8f * unit), health, fill);
        Rect track = new Rect(x + badge + unit, y, width - badge - unit, height);
        DrawNeonFrame(track, health ? NeonYellow : NeonPurple, health ? NeonPurple : NeonYellow, unit);
        Rect inner = new Rect(track.x + 3f * unit, track.y + 3f * unit,
            track.width - 6f * unit, track.height - 6f * unit);
        DrawBar(inner, value, fill);
        DrawText(new Rect(track.x + 5f * unit, y, track.width - 10f * unit, lineHeight), label, style);
    }

    private static void Solid(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void StyleControl(GUIStyle style, Color text)
    {
        style.fontStyle = FontStyle.Bold;
        style.border = new RectOffset();
        foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active,
            style.focused, style.onNormal, style.onHover, style.onActive, style.onFocused })
        {
            state.background = null;
            state.textColor = text;
        }
        style.hover.textColor = Color.white;
        style.active.textColor = NeonPink;
        style.focused.textColor = Color.white;
    }

    private static bool NeonButton(Rect rect, string text, GUIStyle style)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        float unit = Mathf.Max(1f, rect.height / 25f);
        DrawNeonFrame(rect, hover ? NeonCyan : NeonYellow, NeonPurple, unit);
        if (hover)
            Solid(new Rect(rect.x + 3f * unit, rect.y + 3f * unit,
                rect.width - 6f * unit, rect.height - 6f * unit), new Color(0.1f, 0.85f, 1f, 0.14f));
        return GUI.Button(rect, text, style);
    }

    private static void DrawPanelSkin(Rect rect, float scale)
    {
        Solid(rect, new Color(0.008f, 0.002f, 0.025f, 0.9f));
        float u = Mathf.Max(1f, scale);
        Solid(new Rect(rect.x, rect.y, rect.width * 0.56f, u), NeonYellow);
        Solid(new Rect(rect.x + rect.width * 0.59f, rect.y, rect.width * 0.41f, u), NeonPurple);
    }

    private static void DrawNeonFrame(Rect rect, Color first, Color second, float u)
    {
        Solid(rect, new Color(0.005f, 0.002f, 0.018f, 0.96f));
        // Clipped-corner neon rails, all contained within the existing layout rectangle.
        float cut = 3f * u;
        Solid(new Rect(rect.x + cut, rect.y + u, rect.width - cut * 2f, u), first);
        Solid(new Rect(rect.x + u, rect.y + cut, u, rect.height - cut * 2f), first);
        Solid(new Rect(rect.x + cut, rect.yMax - 2f * u, rect.width - cut * 2f, u), second);
        Solid(new Rect(rect.xMax - 2f * u, rect.y + cut, u, rect.height - cut * 2f), second);
        Solid(new Rect(rect.x + rect.width * 0.7f, rect.y + u, rect.width * 0.18f, u), second);
    }

    private static void DrawPixelIcon(Rect rect, bool heart, Color color)
    {
        string[] pixels = heart
            ? new[] { "0110110", "1111111", "1111111", "0111110", "0011100", "0001000" }
            : new[] { "0011100", "0111110", "0100010", "0110110", "0111110", "0111110" };
        float cell = Mathf.Min(rect.width / 7f, rect.height / 6f);
        for (int row = 0; row < pixels.Length; row++)
            for (int col = 0; col < 7; col++)
                if (pixels[row][col] == '1')
                    Solid(new Rect(rect.center.x - cell * 3.5f + col * cell,
                        rect.center.y - cell * 3f + row * cell, cell, cell), color);
    }

    private static void DrawText(
        Rect rect, string label, GUIStyle style)
    {
        float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 3.73f + rect.x * 0.013f)), 18f);
        if (pulse > 0.04f)
        {
            Color original = style.normal.textColor;
            style.normal.textColor = new Color(0f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(rect.x - 2.5f * pulse, rect.y, rect.width, rect.height), label, style);
            style.normal.textColor = new Color(1f, 0f, 0.62f, 0.55f);
            GUI.Label(new Rect(rect.x + 2.5f * pulse, rect.y, rect.width, rect.height), label, style);
            style.normal.textColor = original;
        }
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f,
            rect.width, rect.height), label, style);
        GUI.color = oldColor;
        GUI.Label(rect, label, style);
    }

    private static void DrawBar(Rect rect, float value, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.52f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = color;
        rect.width *= Mathf.Clamp01(value);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
        if (rect.width > 2f)
            Solid(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 1f), new Color(1f, 1f, 1f, 0.5f));
    }
}
