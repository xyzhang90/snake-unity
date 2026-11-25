using System.Collections.Generic;
using UnityEngine;

namespace SnakeGame
{
    /// <summary>
    /// 棋盘视图：运行时生成所有美术资源（贴图、精灵、格子、粒子），
    /// 仓库里不需要任何图片素材，打开就能跑。
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        public GameCore Core;

        Sprite roundedSprite;
        Sprite cellSprite;
        Camera cam;

        Transform segmentRoot;
        Transform obstacleRoot;
        Transform gridRoot;
        Transform particleRoot;
        Transform foodTransform;
        SpriteRenderer foodRenderer;
        SpriteRenderer bgRenderer;
        SpriteRenderer frameRenderer;

        readonly List<Transform> segTransforms = new List<Transform>();
        readonly List<SpriteRenderer> segRenderers = new List<SpriteRenderer>();
        readonly List<Vector3> segPositions = new List<Vector3>();

        class Particle
        {
            public Transform T;
            public SpriteRenderer R;
            public Vector3 Vel;
            public float Life;
            public float MaxLife;
            public Color C;
        }

        readonly List<Particle> particles = new List<Particle>();

        float clock;
        float flash;
        float lastAspect = -1f;

        static readonly Color BgColor = new Color32(14, 18, 32, 255);
        static readonly Color BoardColor = new Color32(20, 26, 44, 255);
        static readonly Color GridColor = new Color32(70, 90, 120, 40);
        static readonly Color FrameColor = new Color32(90, 230, 200, 120);
        static readonly Color HeadColor = new Color32(130, 255, 170, 255);
        static readonly Color BodyColor2 = new Color32(40, 150, 130, 255);
        static readonly Color BodyColor1 = new Color32(70, 215, 175, 255);
        static readonly Color FoodColor = new Color32(255, 86, 96, 255);
        static readonly Color BonusColor = new Color32(255, 198, 66, 255);
        static readonly Color ObstacleColor = new Color32(62, 78, 104, 255);

        public void Init(GameCore core)
        {
            Core = core;

            cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(8, 10, 18, 255);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            roundedSprite = CreateRoundedSprite();
            cellSprite = CreatePlainSprite();

            segmentRoot = NewChild("Snake");
            obstacleRoot = NewChild("Obstacles");
            gridRoot = NewChild("Grid");
            particleRoot = NewChild("Particles");

            // 背景与外框
            bgRenderer = MakeQuad("Bg", BoardColor, -100, cellSprite, Vector3.zero,
                new Vector3(GameCore.Width * 1.6f, GameCore.Height * 1.6f, 1f), transform);
            frameRenderer = MakeQuad("Frame", FrameColor, -90, cellSprite, Vector3.zero,
                new Vector3(GameCore.Width + 0.16f, GameCore.Height + 0.16f, 1f), transform);

            BuildGrid();

            foodTransform = new GameObject("Food").transform;
            foodTransform.SetParent(transform, false);
            foodRenderer = foodTransform.gameObject.AddComponent<SpriteRenderer>();
            foodRenderer.sprite = roundedSprite;
            foodRenderer.color = FoodColor;
            foodRenderer.sortingOrder = 12;

            FitCamera(true);
        }

        Transform NewChild(string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        static void ClearChildren(Transform t)
        {
            // Unity 的 Destroy 是延迟到帧末执行的，childCount 不会立刻变小，
            // 所以先拍一份快照再逐个销毁，避免边遍历边改动。
            int count = t.childCount;
            Transform[] kids = new Transform[count];
            for (int i = 0; i < count; i++) kids[i] = t.GetChild(i);
            for (int i = 0; i < count; i++)
                if (kids[i] != null) Destroy(kids[i].gameObject);
        }

        void BuildGrid()
        {
            ClearChildren(gridRoot);
            for (int x = 0; x <= GameCore.Width; x++)
            {
                MakeQuad("gx", GridColor, -80, cellSprite,
                    new Vector3(x - GameCore.Width * 0.5f, 0f, 0f),
                    new Vector3(0.045f, GameCore.Height, 1f), gridRoot);
            }
            for (int y = 0; y <= GameCore.Height; y++)
            {
                MakeQuad("gy", GridColor, -80, cellSprite,
                    new Vector3(0f, y - GameCore.Height * 0.5f, 0f),
                    new Vector3(GameCore.Width, 0.045f, 1f), gridRoot);
            }
        }

        SpriteRenderer MakeQuad(string name, Color color, int order, Sprite sprite, Vector3 pos, Vector3 scale, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>换难度或换局时重建障碍与蛇身。</summary>
        public void OnNewGame()
        {
            ClearChildren(obstacleRoot);
            foreach (Vector2Int cell in Core.Obstacles)
            {
                MakeQuad("obs", ObstacleColor, 0, cellSprite, CellToWorld(cell),
                    new Vector3(0.96f, 0.96f, 1f), obstacleRoot);
            }

            ClearChildren(segmentRoot);
            segTransforms.Clear();
            segRenderers.Clear();
            segPositions.Clear();

            for (int i = 0; i < particles.Count; i++)
                if (particles[i].T != null) Destroy(particles[i].T.gameObject);
            particles.Clear();

            flash = 0f;
            SyncSegments(true);
            foodTransform.position = CellToWorld(Core.Food);
        }

        public static Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(cell.x - GameCore.Width * 0.5f + 0.5f,
                               cell.y - GameCore.Height * 0.5f + 0.5f, 0f);
        }

        void SyncSegments(bool snap)
        {
            int want = Core.Body.Count;
            while (segTransforms.Count < want)
            {
                GameObject go = new GameObject("seg");
                go.transform.SetParent(segmentRoot, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = roundedSprite;
                sr.sortingOrder = 10;

                Vector3 p = CellToWorld(Core.Body[segTransforms.Count]);
                go.transform.position = p;

                segTransforms.Add(go.transform);
                segRenderers.Add(sr);
                segPositions.Add(p);
            }

            while (segTransforms.Count > want)
            {
                int last = segTransforms.Count - 1;
                Destroy(segTransforms[last].gameObject);
                segTransforms.RemoveAt(last);
                segRenderers.RemoveAt(last);
                segPositions.RemoveAt(last);
            }

            for (int i = 0; i < want; i++)
            {
                Color c;
                if (i == 0) c = HeadColor;
                else
                {
                    float t = want <= 2 ? 0f : (float)(i - 1) / (want - 2);
                    c = Color.Lerp(BodyColor1, BodyColor2, Mathf.Clamp01(t));
                }
                segRenderers[i].color = c;

                float scale = i == 0 ? 0.92f : 0.84f;
                segTransforms[i].localScale = new Vector3(scale, scale, 1f);

                if (snap)
                {
                    Vector3 p = CellToWorld(Core.Body[i]);
                    segTransforms[i].position = p;
                    segPositions[i] = p;
                }
            }
        }

        public void Render(float dt)
        {
            clock += dt;
            FitCamera(false);

            if (Core == null || Core.Body.Count == 0) return;

            SyncSegments(false);

            // 蛇身平滑跟随：格子逻辑是一格一格走，渲染用指数插值抹平
            float k = 1f - Mathf.Exp(-14f * dt);
            for (int i = 0; i < segTransforms.Count; i++)
            {
                Vector3 target = CellToWorld(Core.Body[i]);
                segPositions[i] = Vector3.Lerp(segPositions[i], target, k);
                segTransforms[i].position = segPositions[i];
            }

            // 食物呼吸 + 金果倒计时闪烁
            if (Core.FoodIsBonus)
            {
                foodRenderer.color = BonusColor;
                float t = Mathf.Clamp01(Core.BonusLeft / GameCore.BonusSeconds);
                foodTransform.localScale = Vector3.one * (0.9f + 0.12f * Mathf.Sin(clock * 14f)) * (t > 0.999f ? 1f : Mathf.Max(0.75f, t));
            }
            else
            {
                foodRenderer.color = FoodColor;
                foodTransform.localScale = Vector3.one * (0.78f + 0.06f * Mathf.Sin(clock * 5f));
            }
            foodTransform.position = CellToWorld(Core.Food);

            UpdateParticles(dt);

            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - dt * 2.2f);
                bgRenderer.color = Color.Lerp(BoardColor, new Color32(120, 40, 60, 255), flash * 0.6f);
            }
            else
            {
                bgRenderer.color = BoardColor;
            }
        }

        void UpdateParticles(float dt)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    Destroy(p.T.gameObject);
                    particles.RemoveAt(i);
                    continue;
                }
                p.T.position += p.Vel * dt;
                p.Vel *= Mathf.Clamp01(1f - 3f * dt);
                float a = Mathf.Clamp01(p.Life / p.MaxLife);
                p.R.color = new Color(p.C.r, p.C.g, p.C.b, a);
                p.T.localScale = Vector3.one * (0.16f * Mathf.Max(0.25f, a));
            }
        }

        public void Burst(Vector3 at, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("p");
                go.transform.SetParent(particleRoot, false);
                go.transform.position = at;
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = roundedSprite;
                sr.sortingOrder = 20;
                sr.color = color;

                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;

                Particle p = new Particle();
                p.T = go.transform;
                p.R = sr;
                p.C = color;
                p.Vel = new Vector3(dir.x, dir.y, 0f) * Random.Range(2.5f, 6.5f);
                p.Life = Random.Range(0.28f, 0.5f);
                p.MaxLife = p.Life;
                particles.Add(p);
            }
        }

        public void Flash()
        {
            flash = 1f;
        }

        void FitCamera(bool force)
        {
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.777f;
            if (!force && Mathf.Abs(aspect - lastAspect) < 0.001f) return;
            lastAspect = aspect;

            float halfH = GameCore.Height * 0.5f + 1.6f;
            float halfW = GameCore.Width * 0.5f + 1.6f;
            cam.orthographicSize = Mathf.Max(halfH, halfW / Mathf.Max(0.2f, aspect));
        }

        static Sprite CreatePlainSprite()
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color32 white = new Color32(255, 255, 255, 255);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        static Sprite CreateRoundedSprite()
        {
            const int size = 12;
            const int radius = 3;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    bool corner = (px < radius && py < radius) ||
                                  (px > size - radius && py < radius) ||
                                  (px < radius && py > size - radius) ||
                                  (px > size - radius && py > size - radius);
                    float a = 1f;
                    if (corner)
                    {
                        float cx = px < radius ? radius : size - radius;
                        float cy = py < radius ? radius : size - radius;
                        float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                        a = Mathf.Clamp01(radius - d + 0.5f);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
