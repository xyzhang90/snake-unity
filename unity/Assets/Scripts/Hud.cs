using UnityEngine;
using UnityEngine.UI;

namespace SnakeGame
{
    /// <summary>
    /// 全部 HUD 都由代码构建（uGUI），不依赖 prefab。
    /// 说明：Unity 内置字体只带拉丁字形，所以界面文案用英文，中文说明放在 README。
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Text scoreText;
        Text metaText;
        Text hintText;

        GameObject menuGroup;
        Text menuText;

        GameObject overGroup;
        Text overText;

        GameObject pauseGroup;
        Text pauseText;

        static readonly Color Ink = new Color32(232, 242, 255, 255);
        static readonly Color Dim = new Color32(140, 160, 190, 255);
        static readonly Color Accent = new Color32(124, 255, 200, 255);
        static readonly Color Warn = new Color32(255, 120, 130, 255);

        static Font cachedFont;

        public void Init()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            scoreText = CreateText("Score", 40, TextAnchor.UpperLeft, Ink);
            PlaceAtCorner(scoreText, new Vector2(0f, 1f), new Vector2(26f, -20f), new Vector2(760f, 70f));

            metaText = CreateText("Meta", 26, TextAnchor.UpperRight, Dim);
            PlaceAtCorner(metaText, new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(1000f, 70f));

            hintText = CreateText("Hint", 20, TextAnchor.LowerLeft, new Color32(120, 140, 165, 255));
            PlaceAtCorner(hintText, new Vector2(0f, 0f), new Vector2(26f, 14f), new Vector2(1300f, 40f));
            hintText.text = "WASD / ARROWS    SPACE = PAUSE    R = RESTART    ESC = MENU";

            menuGroup = CreateOverlay("Menu");
            menuText = CreateText("MenuText", 30, TextAnchor.MiddleCenter, Ink, menuGroup.transform);
            PlaceCentered(menuText, new Vector2(1600f, 900f));

            overGroup = CreateOverlay("Over");
            overGroup.SetActive(false);
            overText = CreateText("OverText", 30, TextAnchor.MiddleCenter, Ink, overGroup.transform);
            PlaceCentered(overText, new Vector2(1400f, 700f));

            pauseGroup = CreateOverlay("Pause");
            pauseGroup.SetActive(false);
            pauseText = CreateText("PauseText", 40, TextAnchor.MiddleCenter, Ink, pauseGroup.transform);
            PlaceCentered(pauseText, new Vector2(900f, 300f));
            pauseText.text = "PAUSED\n<size=24><color=#8CA0BE>SPACE / TAP TO RESUME</color></size>";
        }

        GameObject CreateOverlay(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);

            Image img = go.AddComponent<Image>();
            img.color = new Color32(6, 10, 20, 210);
            img.raycastTarget = false;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return go;
        }

        Text CreateText(string name, int fontSize, TextAnchor anchor, Color color)
        {
            return CreateText(name, fontSize, anchor, color, transform);
        }

        Text CreateText(string name, int fontSize, TextAnchor anchor, Color color, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Text t = go.AddComponent<Text>();
            t.font = GetFont();
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = color;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.15f;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>把文本贴在父级某个角上，pos 是该角到锚点的偏移。</summary>
        static void PlaceAtCorner(Text t, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            RectTransform rt = t.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void PlaceCentered(Text t, Vector2 size)
        {
            RectTransform rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        static Font GetFont()
        {
            if (cachedFont != null) return cachedFont;
            try { cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { }
            if (cachedFont == null)
            {
                try { cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch { }
            }
            return cachedFont;
        }

        public void SetPlayHud(int score, int best, string diffLabel, bool wrap, int bonusSecondsLeft)
        {
            if (scoreText != null) scoreText.text = "SCORE  " + score;
            if (metaText != null)
            {
                string meta = "BEST " + best + "   \u00b7   " + diffLabel + "   \u00b7   WRAP " + (wrap ? "ON" : "OFF");
                if (bonusSecondsLeft > 0) meta += "   \u00b7   <color=#FFC642>BONUS " + bonusSecondsLeft + "s</color>";
                metaText.text = meta;
            }
        }

        public void ShowPlaying()
        {
            menuGroup.SetActive(false);
            overGroup.SetActive(false);
            pauseGroup.SetActive(false);
            scoreText.enabled = true;
            metaText.enabled = true;
            hintText.enabled = true;
        }

        public void ShowMenu(int selected, int best, bool wrap)
        {
            menuGroup.SetActive(true);
            overGroup.SetActive(false);
            pauseGroup.SetActive(false);
            scoreText.enabled = false;
            metaText.enabled = false;
            hintText.enabled = false;

            string[] labels = { "EASY   0.30s  \u00b7  1 pt", "NORMAL 0.18s  \u00b7  2 pt", "HARD   0.11s  \u00b7  3 pt  \u00b7  obstacles", "HELL   0.07s  \u00b7  4 pt  \u00b7  obstacles" };
            string body = "";
            for (int i = 0; i < labels.Length; i++)
            {
                bool on = i == selected;
                string line = (on ? "\u25b6 " : "   ") + "[" + (i + 1) + "] " + labels[i];
                body += on ? "<color=#7CFFC8>" + line + "</color>\n" : "<color=#8CA0BE>" + line + "</color>\n";
            }

            menuText.text =
                "<size=72><b><color=#7CFFC8>S N A K E</color></b></size>\n" +
                "<size=24><color=#8CA0BE>console remake \u00b7 unity edition</color></size>\n\n" +
                body +
                "\n<size=24><color=#8CA0BE>WRAP AROUND : <color=#E8F2FF>" + (wrap ? "ON" : "OFF") + "</color>    (W to toggle)</color></size>\n" +
                "<size=24><color=#8CA0BE>BEST : <color=#FFC642>" + best + "</color></size>\n\n" +
                "<size=32><color=#FFFFFF>ENTER / TAP TO START</color></size>";
        }

        public void ShowPause(bool paused)
        {
            pauseGroup.SetActive(paused);
        }

        public void ShowGameOver(int score, int best, bool newRecord)
        {
            menuGroup.SetActive(false);
            pauseGroup.SetActive(false);
            overGroup.SetActive(true);
            scoreText.enabled = false;
            metaText.enabled = false;
            hintText.enabled = false;

            overText.text =
                "<size=64><b><color=#FF7882>GAME OVER</color></b></size>\n\n" +
                "<size=40>SCORE  " + score + "</size>\n" +
                "<size=26><color=#8CA0BE>BEST  " + best + "</color></size>\n" +
                (newRecord ? "<size=30><color=#FFC642>NEW RECORD!</color></size>\n" : "\n") +
                "\n<size=28>R / TAP TO PLAY AGAIN</size>\n" +
                "<size=22><color=#8CA0BE>ESC FOR MENU</color></size>";
        }
    }
}
