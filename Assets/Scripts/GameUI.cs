using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TankBattle
{
    // UGUI 复刻网页版界面：顶栏、双人面板、开始/暂停/结算弹窗、触屏按键
    public class GameUI : MonoBehaviour
    {
        GameController gc;
        Canvas canvas;
        CanvasScaler scaler;
        Font font;

        Text stageValue;
        Text topScores;
        readonly Image[] basePips = new Image[3];
        readonly List<Image> enemyPips = new List<Image>();

        GameObject panelP1;
        GameObject panelP2;
        CanvasGroup panelP2Group;
        Text p1Score;
        Text p2Score;

        readonly Image[] p1Lives = new Image[9];
        readonly Image[] p2Lives = new Image[9];
        readonly Image[] p1Power = new Image[3];
        readonly Image[] p2Power = new Image[3];
        readonly Image[] p1Ult = new Image[3];
        readonly Image[] p2Ult = new Image[3];

        GameObject overlayStart;
        GameObject overlayPause;
        GameObject overlayEnd;
        Text endTitle;
        Text endSub;
        Text endStage;
        Text endP1;
        Text endP2;
        Text endNextLabel;
        GameObject victoryStats;
        Text vKills;
        Text vUlts;
        Text vShots;
        Text vScore;
        Text soundLabel;

        int lastStage = -1;
        int lastP1Score = -1;
        int lastP2Score = -1;
        int lastBaseHp = -1;
        int lastEnemyCount = -1;
        string lastLives = "";
        string lastPower = "";
        string lastUlt = "";

        /* ---------------- 构建 ---------------- */

        public void Build(GameController controller)
        {
            gc = controller;
            font = LoadFont();

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            BuildTopBar();
            BuildPanel(true);
            BuildPanel(false);
            BuildFooter();
            BuildTouch();
            BuildStartOverlay();
            BuildPauseOverlay();
            BuildEndOverlay();

            overlayStart.SetActive(true);
            overlayPause.SetActive(false);
            overlayEnd.SetActive(false);
            SetSoundLabel(AudioSynth.Enabled);
        }

        static Font LoadFont()
        {
            Font f = null;
            string[] names =
            {
                "PingFang SC", "Hiragino Sans GB", "Heiti SC", "Microsoft YaHei",
                "SimHei", "Noto Sans CJK SC", "Arial Unicode MS"
            };
            try { f = Font.CreateDynamicFontFromOSFont(names, 24); } catch { f = null; }
            if (f == null)
            {
                try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { f = null; }
            }
            if (f == null)
            {
                try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { f = null; }
            }
            return f;
        }

        /* ---------------- 构建工具 ---------------- */

        RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float l, float b, float r, float t)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
        }

        static void TopRight(RectTransform rt, float x, float y, float w, float h)
        {
            Place(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-x, -y), new Vector2(w, h));
        }

        Image Panel(Transform parent, Color color)
        {
            var rt = NewRect("Panel", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteFactory.Rounded();
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Image Solid(Transform parent, Color color)
        {
            var rt = NewRect("Solid", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var rt = NewRect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        Button Btn(Transform parent, string label, Vector2 size, Color bg, Color fg, UnityAction onClick)
        {
            var rt = NewRect("Btn", parent);
            rt.sizeDelta = size;

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteFactory.Rounded();
            img.type = Image.Type.Sliced;
            img.color = bg;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.82f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(onClick);

            Text txt = Label(rt, label, 20, fg, TextAnchor.MiddleCenter);
            Stretch((RectTransform)txt.transform, 10f, 4f, 10f, 4f);
            return btn;
        }

        /* ---------------- 顶栏 ---------------- */

        void BuildTopBar()
        {
            Image bar = Panel(transform, Conf.PanelFill);
            Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -16f), new Vector2(-44f, 86f));

            Image mark = Panel(bar.transform, Conf.Accent);
            Place(mark.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(42f, 42f));

            Text title = Label(bar.transform, "TANK BATTLE", 24, Conf.TextMain, TextAnchor.MiddleLeft);
            Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(76f, -16f), new Vector2(340f, 30f));

            Text sub = Label(bar.transform, "坦克大战 · FC 经典重制", 12, Conf.TextMuted, TextAnchor.MiddleLeft);
            Place((RectTransform)sub.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(76f, -48f), new Vector2(340f, 20f));

            Text stageLabel = Label(bar.transform, "关卡", 11, Conf.TextMuted, TextAnchor.MiddleCenter);
            Place((RectTransform)stageLabel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-290f, 22f), new Vector2(110f, 16f));

            stageValue = Label(bar.transform, "1", 26, Conf.Gold, TextAnchor.MiddleCenter);
            Place((RectTransform)stageValue.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-290f, -6f), new Vector2(110f, 32f));

            Text baseLabel = Label(bar.transform, "基地", 11, Conf.TextMuted, TextAnchor.MiddleCenter);
            Place((RectTransform)baseLabel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-130f, 22f), new Vector2(110f, 16f));

            var baseRow = NewRect("BaseRow", bar.transform);
            Place(baseRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-130f, -8f), new Vector2(60f, 16f));
            for (int i = 0; i < 3; i++)
            {
                Image pip = Solid(baseRow, Conf.Gold);
                Place(pip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((i - 1) * 15f, 0f), new Vector2(11f, 13f));
                basePips[i] = pip;
            }

            Text enemyLabel = Label(bar.transform, "剩余敌军", 11, Conf.TextMuted, TextAnchor.MiddleCenter);
            Place((RectTransform)enemyLabel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(100f, 22f), new Vector2(120f, 16f));

            var enemyRow = NewRect("EnemyRow", bar.transform);
            Place(enemyRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(100f, -8f), new Vector2(132f, 28f));
            for (int i = 0; i < Conf.TotalEnemies; i++)
            {
                Image pip = Solid(enemyRow, Conf.Danger);
                Place(pip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((i % 10) * 13f - 58f, -(i / 10) * 13f + 6f), new Vector2(10f, 10f));
                enemyPips.Add(pip);
            }

            topScores = Label(bar.transform, "P1 0  ·  P2 0", 13, Conf.TextMuted, TextAnchor.MiddleCenter);
            Place((RectTransform)topScores.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(330f, 0f), new Vector2(240f, 30f));

            Button sound = Btn(bar.transform, "声音", new Vector2(88f, 44f), Conf.Rgba(255, 255, 255, 0.06f), Conf.TextMain, gc.ToggleSound);
            Place((RectTransform)sound.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-118f, 0f), new Vector2(88f, 44f));
            soundLabel = sound.GetComponentInChildren<Text>();

            Button pause = Btn(bar.transform, "暂停", new Vector2(88f, 44f), Conf.Rgba(255, 255, 255, 0.06f), Conf.TextMain, gc.TogglePause);
            Place((RectTransform)pause.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-18f, 0f), new Vector2(88f, 44f));
        }

        /* ---------------- 玩家面板 ---------------- */

        void BuildPanel(bool isP1)
        {
            Image panel = Panel(transform, Conf.PanelFill);
            float x = isP1 ? 147f : -147f;
            Place(panel.rectTransform, new Vector2(isP1 ? 0f : 1f, 0.5f), new Vector2(isP1 ? 0f : 1f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(x, -30f), new Vector2(250f, 560f));

            if (isP1) panelP1 = panel.gameObject;
            else
            {
                panelP2 = panel.gameObject;
                panelP2Group = panel.gameObject.AddComponent<CanvasGroup>();
            }

            Color accent = isP1 ? Conf.Gold : Conf.Green;

            Image dot = Solid(panel.transform, accent);
            TopLeft(dot.rectTransform, 18f, 22f, 12f, 12f);

            Text name = Label(panel.transform, isP1 ? "玩家 1" : "玩家 2", 17, Conf.TextMain, TextAnchor.MiddleLeft);
            TopLeft((RectTransform)name.transform, 40f, 18f, 140f, 24f);

            Image badge = Panel(panel.transform, Conf.Rgba(255, 255, 255, 0.06f));
            TopRight(badge.rectTransform, 18f, 20f, 46f, 22f);
            Text badgeText = Label(badge.transform, isP1 ? "P1" : "P2", 11, Conf.TextMuted, TextAnchor.MiddleCenter);
            Stretch((RectTransform)badgeText.transform, 0f, 0f, 0f, 0f);

            Text scoreLabel = Label(panel.transform, "得分", 12, Conf.TextMuted, TextAnchor.MiddleLeft);
            TopLeft((RectTransform)scoreLabel.transform, 18f, 68f, 80f, 20f);

            Text score = Label(panel.transform, "0", 22, Color.white, TextAnchor.MiddleRight);
            TopRight((RectTransform)score.transform, 18f, 60f, 110f, 32f);
            if (isP1) p1Score = score;
            else p2Score = score;

            Text livesLabel = Label(panel.transform, "生命", 12, Conf.TextMuted, TextAnchor.MiddleLeft);
            TopLeft((RectTransform)livesLabel.transform, 18f, 110f, 80f, 20f);

            var livesRow = NewRect("Lives", panel.transform);
            TopRight(livesRow, 18f, 112f, 180f, 16f);
            for (int i = 0; i < 9; i++)
            {
                Image pip = Solid(livesRow, accent);
                Place(pip.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-(8 - i) * 19f, 0f), new Vector2(14f, 12f));
                if (isP1) p1Lives[i] = pip;
                else p2Lives[i] = pip;
            }

            Text powerLabel = Label(panel.transform, "火力", 12, Conf.TextMuted, TextAnchor.MiddleLeft);
            TopLeft((RectTransform)powerLabel.transform, 18f, 154f, 80f, 20f);

            var powerRow = NewRect("Power", panel.transform);
            TopRight(powerRow, 18f, 158f, 180f, 12f);
            for (int i = 0; i < 3; i++)
            {
                Image pip = Solid(powerRow, Conf.Rgba(255, 255, 255, 0.12f));
                Place(pip.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-(2 - i) * 14f, 0f), new Vector2(8f, 8f));
                if (isP1) p1Power[i] = pip;
                else p2Power[i] = pip;
            }

            Text ultLabel = Label(panel.transform, "绝招", 12, Conf.TextMuted, TextAnchor.MiddleLeft);
            TopLeft((RectTransform)ultLabel.transform, 18f, 196f, 80f, 20f);

            var ultRow = NewRect("Ult", panel.transform);
            TopRight(ultRow, 18f, 198f, 180f, 16f);
            for (int i = 0; i < 3; i++)
            {
                Image pip = Solid(ultRow, Conf.Rgba(255, 255, 255, 0.12f));
                Place(pip.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-(2 - i) * 14f, 0f), new Vector2(9f, 14f));
                if (isP1) p1Ult[i] = pip;
                else p2Ult[i] = pip;
            }

            Image divider = Solid(panel.transform, Conf.PanelBorder);
            TopLeft(divider.rectTransform, 18f, 240f, 214f, 1f);

            KeyHint(panel.transform, 258f, isP1 ? "W A S D" : "↑ ← ↓ →", "移动");
            KeyHint(panel.transform, 294f, isP1 ? "空格" : "Enter", "射击");
            KeyHint(panel.transform, 330f, "按住 2 秒", "蓄力死光");
        }

        void KeyHint(Transform panel, float y, string key, string desc)
        {
            Image box = Solid(panel.transform, Conf.Rgba(255, 255, 255, 0.06f));
            TopLeft(box.rectTransform, 18f, y, 96f, 24f);
            Text keyText = Label(box.transform, key, 11, Conf.TextMain, TextAnchor.MiddleCenter);
            Stretch((RectTransform)keyText.transform, 0f, 0f, 0f, 0f);

            Text descText = Label(panel.transform, desc, 12, Conf.TextMuted, TextAnchor.MiddleRight);
            TopRight((RectTransform)descText.transform, 18f, y + 2f, 96f, 20f);
        }

        void BuildFooter()
        {
            Text footer = Label(transform, "P / ESC 暂停   ·   按住发射 2 秒蓄力破坏死光   ·   守护基地",
                12, Conf.TextMuted, TextAnchor.MiddleCenter);
            Place((RectTransform)footer.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 14f), new Vector2(900f, 20f));
        }

        /* ---------------- 弹窗 ---------------- */

        GameObject MakeOverlay(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt, 0f, 0f, 0f, 0f);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.06f, 0.62f);
            bg.raycastTarget = true;
            return go;
        }

        Image Modal(GameObject overlay, float w, float h)
        {
            Image modal = Panel(overlay.transform, Conf.Alpha(Conf.PanelFill, 0.97f));
            Place(modal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(w, h));
            return modal;
        }

        Text ModalTitle(Image modal, string text, float y, int size)
        {
            Text t = Label(modal.transform, text, size, Conf.TextMain, TextAnchor.MiddleCenter);
            Place((RectTransform)t.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, y), new Vector2(modal.rectTransform.sizeDelta.x - 60f, size + 10f));
            return t;
        }

        void BuildStartOverlay()
        {
            overlayStart = MakeOverlay("OverlayStart");
            Image modal = Modal(overlayStart, 520f, 420f);

            ModalTitle(modal, "TANK BATTLE", -38f, 34);
            Text subtitle = Label(modal.transform, "坦克大战", 15, Conf.TextMuted, TextAnchor.MiddleCenter);
            Place((RectTransform)subtitle.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -84f), new Vector2(460f, 24f));

            Button single = Btn(modal.transform, "单人模式    WASD + 空格", new Vector2(440f, 72f),
                Conf.Rgba(255, 255, 255, 0.05f), Conf.TextMain, () => gc.StartGame(1));
            Place((RectTransform)single.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 16f), new Vector2(440f, 72f));

            Button dual = Btn(modal.transform, "双人模式    P1 WASD · P2 方向键", new Vector2(440f, 72f),
                Conf.Rgba(255, 255, 255, 0.05f), Conf.TextMain, () => gc.StartGame(2));
            Place((RectTransform)dual.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -72f), new Vector2(440f, 72f));

            Text hint = Label(modal.transform, "选择模式开始游戏", 12, Conf.TextMuted, TextAnchor.MiddleCenter);
            Place((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(440f, 20f));
        }

        void BuildPauseOverlay()
        {
            overlayPause = MakeOverlay("OverlayPause");
            Image modal = Modal(overlayPause, 380f, 320f);

            ModalTitle(modal, "已暂停", -36f, 30);

            Button resume = Btn(modal.transform, "继续游戏", new Vector2(300f, 56f),
                Conf.Accent, Color.white, () => gc.TogglePause());
            Place((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 4f), new Vector2(300f, 56f));

            Button restart = Btn(modal.transform, "重新开始", new Vector2(300f, 56f),
                Conf.Rgba(255, 255, 255, 0.06f), Conf.TextMain, () => gc.StartGame(gc.G.Mode));
            Place((RectTransform)restart.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -66f), new Vector2(300f, 56f));
        }

        void BuildEndOverlay()
        {
            overlayEnd = MakeOverlay("OverlayEnd");
            Image modal = Modal(overlayEnd, 540f, 560f);

            endTitle = ModalTitle(modal, "阶段完成", -34f, 30);

            endSub = Label(modal.transform, "", 14, Conf.TextMuted, TextAnchor.MiddleCenter);
            Place((RectTransform)endSub.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -78f), new Vector2(480f, 24f));

            StatCell(modal.transform, new Vector2(-160f, 120f), new Vector2(150f, 76f), "关卡", out endStage);
            StatCell(modal.transform, new Vector2(0f, 120f), new Vector2(150f, 76f), "P1 得分", out endP1);
            StatCell(modal.transform, new Vector2(160f, 120f), new Vector2(150f, 76f), "P2 得分", out endP2);

            victoryStats = new GameObject("VictoryStats", typeof(RectTransform));
            victoryStats.transform.SetParent(modal.transform, false);
            var vsRt = (RectTransform)victoryStats.transform;
            Place(vsRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(480f, 170f));

            StatCell(victoryStats.transform, new Vector2(-100f, 42f), new Vector2(200f, 70f), "消灭坦克", out vKills);
            StatCell(victoryStats.transform, new Vector2(100f, 42f), new Vector2(200f, 70f), "使用绝招", out vUlts);
            StatCell(victoryStats.transform, new Vector2(-100f, -42f), new Vector2(200f, 70f), "普通射击", out vShots);
            StatCell(victoryStats.transform, new Vector2(100f, -42f), new Vector2(200f, 70f), "总得分", out vScore);

            Button next = Btn(modal.transform, "下一关", new Vector2(320f, 56f), Conf.Accent, Color.white, () => gc.NextOrRestart());
            Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 108f), new Vector2(320f, 56f));
            endNextLabel = next.GetComponentInChildren<Text>();

            Button home = Btn(modal.transform, "返回主菜单", new Vector2(320f, 48f),
                Conf.Rgba(255, 255, 255, 0.06f), Conf.TextMain, () => gc.GoHome());
            Place((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 42f), new Vector2(320f, 48f));
        }

        void StatCell(Transform parent, Vector2 pos, Vector2 size, string label, out Text value)
        {
            Image cell = Panel(parent, Conf.Rgba(255, 255, 255, 0.04f));
            Place(cell.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, size);

            Text lb = Label(cell.transform, label, 11, Conf.TextMuted, TextAnchor.MiddleCenter);
            Place((RectTransform)lb.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(size.x - 12f, 16f));

            value = Label(cell.transform, "0", 20, Conf.Gold, TextAnchor.MiddleCenter);
            Place((RectTransform)value.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(size.x - 12f, 26f));
        }

        /* ---------------- 触屏控制 ---------------- */

        void BuildTouch()
        {
            if (!Application.isMobilePlatform && !Input.touchSupported) return;

            var rootRt = NewRect("TouchControls", transform);
            Stretch(rootRt, 0f, 0f, 0f, 0f);

            AddTouchButton(rootRt, "▲", new Vector2(180f, 300f), new Vector2(110f, 110f), KeyCode.UpArrow);
            AddTouchButton(rootRt, "◀", new Vector2(90f, 190f), new Vector2(110f, 110f), KeyCode.LeftArrow);
            AddTouchButton(rootRt, "▼", new Vector2(180f, 190f), new Vector2(110f, 110f), KeyCode.DownArrow);
            AddTouchButton(rootRt, "▶", new Vector2(270f, 190f), new Vector2(110f, 110f), KeyCode.RightArrow);

            var fire = Panel(rootRt, Conf.Rgba(210, 43, 58, 0.85f));
            Place(fire.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-150f, 190f), new Vector2(130f, 130f));
            fire.raycastTarget = true;
            Text fireText = Label(fire.transform, "●", 34, Color.white, TextAnchor.MiddleCenter);
            Stretch((RectTransform)fireText.transform, 0f, 0f, 0f, 0f);
            var fireBtn = fire.gameObject.AddComponent<UiTouchButton>();
            fireBtn.OnDown = gc.PressVirtualFire;
            fireBtn.OnUp = gc.ReleaseVirtualFire;
        }

        void AddTouchButton(Transform parent, string label, Vector2 pos, Vector2 size, KeyCode key)
        {
            Image img = Panel(parent, Conf.Rgba(16, 24, 46, 0.8f));
            Place(img.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), pos, size);
            img.raycastTarget = true;

            Text text = Label(img.transform, label, 26, Conf.TextMain, TextAnchor.MiddleCenter);
            Stretch((RectTransform)text.transform, 0f, 0f, 0f, 0f);

            var tb = img.gameObject.AddComponent<UiTouchButton>();
            tb.OnDown = () => gc.PressVirtual(key);
            tb.OnUp = () => gc.ReleaseVirtual(key);
        }

        /* ---------------- HUD 刷新 ---------------- */

        public void UpdateHud(Game g)
        {
            if (g == null || stageValue == null) return;

            if (g.Stage != lastStage)
            {
                stageValue.text = g.Stage.ToString();
                lastStage = g.Stage;
            }

            int s1 = g.Profiles[0].Score;
            int s2 = g.Profiles[1].Score;
            if (s1 != lastP1Score || s2 != lastP2Score)
            {
                if (p1Score != null) p1Score.text = s1.ToString();
                if (p2Score != null) p2Score.text = s2.ToString();
                if (topScores != null) topScores.text = "P1 " + s1 + "  ·  P2 " + s2;
                lastP1Score = s1;
                lastP2Score = s2;
            }

            if (g.BaseHp != lastBaseHp)
            {
                for (int i = 0; i < basePips.Length; i++)
                {
                    if (basePips[i] == null) continue;
                    basePips[i].color = i < g.BaseHp ? Conf.Gold : Conf.Rgba(255, 255, 255, 0.12f);
                }
                lastBaseHp = g.BaseHp;
            }

            if (g.EnemiesLeft != lastEnemyCount)
            {
                for (int i = 0; i < enemyPips.Count; i++)
                {
                    enemyPips[i].enabled = i < g.EnemiesLeft;
                }
                lastEnemyCount = g.EnemiesLeft;
            }

            string livesKey = g.Profiles[0].Lives + "-" + g.Profiles[1].Lives + "-" + g.Mode;
            if (livesKey != lastLives)
            {
                SetPips(p1Lives, g.Profiles[0].Lives, Conf.Gold);
                SetPips(p2Lives, g.Profiles[1].Lives, Conf.Green);
                lastLives = livesKey;
            }

            string powerKey = g.Profiles[0].Power + "-" + g.Profiles[1].Power;
            if (powerKey != lastPower)
            {
                SetPips(p1Power, g.Profiles[0].Power, Conf.Gold);
                SetPips(p2Power, g.Profiles[1].Power, Conf.Green);
                lastPower = powerKey;
            }

            int u1 = 0;
            int u2 = 0;
            foreach (Tank t in g.Tanks)
            {
                if (t.IsEnemy) continue;
                if (t.PlayerIndex == 0) u1 = t.UltCharges;
                else u2 = t.UltCharges;
            }

            string ultKey = u1 + "-" + u2;
            if (ultKey != lastUlt)
            {
                SetPips(p1Ult, u1, Conf.Gold);
                SetPips(p2Ult, u2, Conf.Green);
                lastUlt = ultKey;
            }
        }

        static void SetPips(Image[] pips, int count, Color on)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                pips[i].color = i < count ? on : Conf.Rgba(255, 255, 255, 0.12f);
            }
        }

        /* ---------------- 弹窗控制 ---------------- */

        public void ShowStart()
        {
            HideOverlays();
            if (overlayStart != null) overlayStart.SetActive(true);
        }

        public void ShowPause()
        {
            HideOverlays();
            if (overlayPause != null) overlayPause.SetActive(true);
        }

        public void HideOverlays()
        {
            if (overlayStart != null) overlayStart.SetActive(false);
            if (overlayPause != null) overlayPause.SetActive(false);
            if (overlayEnd != null) overlayEnd.SetActive(false);
            // 避免按钮保持选中状态：空格/回车会再次触发点击
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void ShowEnd(Game g)
        {
            bool victory = g.EndKind == "victory";
            bool clear = g.EndKind == "clear";

            endTitle.text = victory ? "通关！" : (clear ? "阶段完成" : "游戏结束");
            endSub.text = victory ? "恭喜通关全部关卡" : (clear ? "第 " + g.Stage + " 关通关" : g.EndReason);
            endStage.text = g.Stage.ToString();
            endP1.text = g.Profiles[0].Score.ToString();
            endP2.text = g.Profiles[1].Score.ToString();
            endNextLabel.text = clear ? "下一关" : "再来一局";

            if (victoryStats != null) victoryStats.SetActive(victory);
            if (victory)
            {
                vKills.text = g.Stats.Kills.ToString();
                vUlts.text = g.Stats.UltUses.ToString();
                vShots.text = g.Stats.Shots.ToString();
                vScore.text = (g.Profiles[0].Score + g.Profiles[1].Score).ToString();
            }

            HideOverlays();
            if (overlayEnd != null) overlayEnd.SetActive(true);
        }

        public void SetP2Off(bool off)
        {
            if (panelP2Group != null) panelP2Group.alpha = off ? 0.45f : 1f;
        }

        public void SetSoundLabel(bool enabled)
        {
            if (soundLabel != null) soundLabel.text = enabled ? "声音" : "静音";
        }

        /* ---------------- 相机取景 ---------------- */

        void LateUpdate()
        {
            if (gc == null || gc.View == null || gc.View.Cam == null) return;

            bool narrow = Screen.width < 980;
            if (panelP1 != null) panelP1.SetActive(!narrow);
            if (panelP2 != null) panelP2.SetActive(!narrow);

            float scale = canvas != null ? canvas.scaleFactor : 1f;
            float reserve = narrow ? 0f : 2f * (250f * scale + 40f * scale);
            float availW = Mathf.Max(200f, Screen.width - reserve);
            bool touch = Application.isMobilePlatform || Input.touchSupported;
            float side = Mathf.Min(Screen.height * (touch ? 0.52f : 0.72f), availW * 0.99f);
            side = Mathf.Max(220f, side);

            gc.View.Cam.orthographicSize = 13f * Screen.height / (2f * side);
            // 3D 俯视相机：高度与触屏偏移由 SceneView 统一管理
            gc.View.ApplyCameraMount(touch);

            // 剩余敌军图标呼吸闪烁（对应网页版的 blink 动画）
            float pulse = 0.55f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f)) * 0.45f;
            Color blink = new Color(1f, 0.42f, 0.47f, pulse);
            for (int i = 0; i < enemyPips.Count; i++)
            {
                if (enemyPips[i].enabled) enemyPips[i].color = blink;
            }
        }
    }

    // 触屏按键：按下/抬起映射为虚拟键
    public class UiTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public System.Action OnDown;
        public System.Action OnUp;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (OnDown != null) OnDown();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (OnUp != null) OnUp();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (OnUp != null) OnUp();
        }
    }
}
