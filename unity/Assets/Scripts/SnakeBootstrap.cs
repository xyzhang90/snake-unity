using UnityEngine;

namespace SnakeGame
{
    /// <summary>
    /// 入口：把棋盘、HUD、音效拼起来，并驱动「菜单 → 游戏 → 暂停 → 结束」状态机。
    /// 场景里只需要一个挂着本脚本的空物体，其余全部运行时生成。
    /// </summary>
    public class SnakeBootstrap : MonoBehaviour
    {
        enum Phase { Menu, Playing, Paused, Over }

        const string BestKey = "snake_best_score";

        readonly GameCore core = new GameCore();

        BoardView view;
        Hud hud;
        Sfx sfx;

        Phase phase = Phase.Menu;
        int diffIndex = 1;
        bool wrapMode;
        int bestScore;
        bool lastRunNewRecord;

        float tickTimer;
        float overDelay;
        float metaTimer;

        bool pointerActive;
        Vector2 pointerStart;
        float pointerDownTime;
        bool pointerTap;
        bool pointerHasSwipe;
        Dir pointerDir;

        void Start()
        {
            Application.targetFrameRate = 60;
            bestScore = PlayerPrefs.GetInt(BestKey, 0);

            view = gameObject.AddComponent<BoardView>();
            hud = gameObject.AddComponent<Hud>();
            sfx = gameObject.AddComponent<Sfx>();

            view.Init(core);
            hud.Init();
            sfx.Init();

            core.Reset(diffIndex, wrapMode);
            view.OnNewGame();

            EnterMenu();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            ReadPointer();
            HandleKeyboard();
            HandlePointer();

            switch (phase)
            {
                case Phase.Playing:
                    tickTimer -= dt;
                    if (core.TickBonus(dt))
                    {
                        // 金色果子超时消失，退回普通果子
                    }
                    int guard = 0;
                    while (tickTimer <= 0f && guard < 3 && phase == Phase.Playing)
                    {
                        tickTimer += core.Diff.Tick;
                        StepOnce();
                        guard++;
                    }
                    break;

                case Phase.Over:
                    if (overDelay > 0f)
                    {
                        overDelay -= dt;
                        if (overDelay <= 0f) hud.ShowGameOver(core.Score, bestScore, lastRunNewRecord);
                    }
                    break;
            }

            view.Render(dt);

            metaTimer -= dt;
            if (phase == Phase.Playing && metaTimer <= 0f)
            {
                metaTimer = 0.15f;
                int bonusLeft = core.FoodIsBonus ? Mathf.CeilToInt(core.BonusLeft) : 0;
                hud.SetPlayHud(core.Score, bestScore, core.Diff.Label, wrapMode, bonusLeft);
            }
        }

        // ---------- 状态切换 ----------

        void EnterMenu()
        {
            phase = Phase.Menu;
            hud.ShowPause(false);
            hud.ShowMenu(diffIndex, bestScore, wrapMode);
        }

        void StartGame()
        {
            core.Reset(diffIndex, wrapMode);
            view.OnNewGame();
            phase = Phase.Playing;
            tickTimer = core.Diff.Tick;
            metaTimer = 0f;
            lastRunNewRecord = false;
            hud.ShowPlaying();
            hud.SetPlayHud(core.Score, bestScore, core.Diff.Label, wrapMode, 0);
            sfx.PlayUi();
        }

        void TogglePause()
        {
            if (phase == Phase.Playing)
            {
                phase = Phase.Paused;
                hud.ShowPause(true);
                sfx.PlayUi();
            }
            else if (phase == Phase.Paused)
            {
                phase = Phase.Playing;
                hud.ShowPause(false);
                sfx.PlayUi();
            }
        }

        void StepOnce()
        {
            StepResult r = core.Step();
            Vector3 headPos = BoardView.CellToWorld(core.Body[0]);

            switch (r)
            {
                case StepResult.Eat:
                    sfx.PlayEat();
                    view.Burst(headPos, new Color32(255, 86, 96, 255), 12);
                    hud.SetPlayHud(core.Score, bestScore, core.Diff.Label, wrapMode,
                        core.FoodIsBonus ? Mathf.CeilToInt(core.BonusLeft) : 0);
                    break;

                case StepResult.EatBonus:
                    sfx.PlayBonus();
                    view.Burst(headPos, new Color32(255, 198, 66, 255), 22);
                    hud.SetPlayHud(core.Score, bestScore, core.Diff.Label, wrapMode, 0);
                    break;

                case StepResult.Dead:
                    Die(headPos);
                    break;
            }
        }

        void Die(Vector3 headPos)
        {
            phase = Phase.Over;
            overDelay = 0.45f;
            sfx.PlayDie();
            view.Flash();
            view.Burst(headPos, new Color32(255, 120, 130, 255), 26);
            view.Burst(headPos, new Color32(255, 255, 255, 255), 10);

            lastRunNewRecord = core.Score > bestScore;
            if (lastRunNewRecord)
            {
                bestScore = core.Score;
                PlayerPrefs.SetInt(BestKey, bestScore);
                PlayerPrefs.Save();
            }
        }

        // ---------- 输入 ----------

        void ReadPointer()
        {
            pointerTap = false;
            pointerHasSwipe = false;

            if (Input.GetMouseButtonDown(0))
            {
                pointerActive = true;
                pointerStart = Input.mousePosition;
                pointerDownTime = Time.time;
                return;
            }

            if (!pointerActive || !Input.GetMouseButtonUp(0)) return;

            pointerActive = false;
            Vector2 delta = (Vector2)Input.mousePosition - pointerStart;
            float threshold = Mathf.Max(30f, Mathf.Min(Screen.width, Screen.height) * 0.06f);

            if (delta.magnitude < threshold && Time.time - pointerDownTime < 0.4f)
            {
                pointerTap = true;
            }
            else if (delta.magnitude >= threshold)
            {
                pointerHasSwipe = true;
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    pointerDir = delta.x > 0f ? Dir.Right : Dir.Left;
                else
                    pointerDir = delta.y > 0f ? Dir.Up : Dir.Down;
            }
        }

        void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EnterMenu();
                return;
            }

            if (phase == Phase.Menu)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) SelectDifficulty(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) SelectDifficulty(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) SelectDifficulty(2);
                if (Input.GetKeyDown(KeyCode.Alpha4)) SelectDifficulty(3);

                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                {
                    wrapMode = !wrapMode;
                    sfx.PlayUi();
                    hud.ShowMenu(diffIndex, bestScore, wrapMode);
                }

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                    StartGame();
                return;
            }

            if (phase == Phase.Over)
            {
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                    StartGame();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                TogglePause();
                return;
            }

            if (phase != Phase.Playing) return;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) core.Queue(Dir.Up);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) core.Queue(Dir.Down);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) core.Queue(Dir.Left);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) core.Queue(Dir.Right);
        }

        void HandlePointer()
        {
            if (pointerHasSwipe && phase == Phase.Playing)
            {
                core.Queue(pointerDir);
                return;
            }

            if (!pointerTap) return;

            switch (phase)
            {
                case Phase.Menu:
                case Phase.Over:
                    if (phase == Phase.Over && overDelay > 0f) return;
                    StartGame();
                    break;
                case Phase.Paused:
                    TogglePause();
                    break;
            }
        }

        void SelectDifficulty(int index)
        {
            if (diffIndex == index) return;
            diffIndex = index;
            sfx.PlayUi();
            hud.ShowMenu(diffIndex, bestScore, wrapMode);
        }
    }
}
