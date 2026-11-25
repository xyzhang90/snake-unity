using System.Collections.Generic;
using UnityEngine;

namespace SnakeGame
{
    public enum Dir { Up = 0, Right = 1, Down = 2, Left = 3 }

    public enum StepResult { Move, Eat, EatBonus, Dead }

    /// <summary>
    /// 一档难度。参数对应控制台原版 snake.h 里的 iRefreshTimes[] 与 food.score。
    /// </summary>
    public class Difficulty
    {
        public readonly string Label;
        public readonly float Tick;
        public readonly int Points;
        public readonly bool Obstacles;

        public Difficulty(string label, float tick, int points, bool obstacles)
        {
            Label = label;
            Tick = tick;
            Points = points;
            Obstacles = obstacles;
        }
    }

    /// <summary>
    /// 纯逻辑的贪吃蛇：不依赖任何渲染，可在任意视图层之上跑。
    /// 坐标系为左下角原点的格子坐标，格子 (x, y) 的圆心在 (x + 0.5, y + 0.5)。
    /// </summary>
    public class GameCore
    {
        public const int Width = 40;
        public const int Height = 20;
        public const int BonusEvery = 5;          // 每吃 5 个果子出一次金色果子
        public const float BonusSeconds = 6f;     // 金色果子的存活时间
        public const int BonusMultiplier = 3;     // 金色果子分数倍率

        public static readonly Difficulty[] Difficulties =
        {
            new Difficulty("EASY",   0.30f, 1, false),
            new Difficulty("NORMAL", 0.18f, 2, false),
            new Difficulty("HARD",   0.11f, 3, true),
            new Difficulty("HELL",   0.07f, 4, true),
        };

        /// <summary>蛇身，index 0 为蛇头。</summary>
        public readonly List<Vector2Int> Body = new List<Vector2Int>();
        public readonly HashSet<Vector2Int> Obstacles = new HashSet<Vector2Int>();

        public Difficulty Diff = Difficulties[1];
        public bool Wrap;
        public Dir Direction = Dir.Right;
        public Dir Queued = Dir.Right;

        public Vector2Int Food;
        public bool FoodIsBonus;
        public float BonusLeft;

        public int Score;
        public int Eaten;
        public bool IsDead;
        public bool AteThisStep;

        System.Random rng = new System.Random();

        public int Seed = 0;

        public void Reset(int difficultyIndex, bool wrap)
        {
            difficultyIndex = Mathf.Clamp(difficultyIndex, 0, Difficulties.Length - 1);
            Diff = Difficulties[difficultyIndex];
            Wrap = wrap;

            Body.Clear();
            Obstacles.Clear();
            Score = 0;
            Eaten = 0;
            IsDead = false;
            AteThisStep = false;
            FoodIsBonus = false;
            BonusLeft = 0f;

            // 与原版一致：从棋盘中间向右出发（原版是 2 节，这里 3 节，视觉更顺）
            Vector2Int head = new Vector2Int(Width / 2, Height / 2);
            Body.Add(head);
            Body.Add(head + new Vector2Int(-1, 0));
            Body.Add(head + new Vector2Int(-2, 0));

            Direction = Dir.Right;
            Queued = Dir.Right;

            if (Diff.Obstacles)
            {
                // 原版 initGame() 里 4 个 2x2 障碍，位置一一对应（y 轴上下翻转以适配 Unity）
                AddObstacleBlock(10, 5);
                AddObstacleBlock(10, Height - 7);
                AddObstacleBlock(Width - 12, 5);
                AddObstacleBlock(Width - 12, Height - 7);
            }

            SpawnFood();
        }

        void AddObstacleBlock(int x, int y)
        {
            for (int dy = 0; dy < 2; dy++)
            {
                for (int dx = 0; dx < 2; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx > 0 && nx < Width - 1 && ny > 0 && ny < Height - 1)
                        Obstacles.Add(new Vector2Int(nx, ny));
                }
            }
        }

        /// <summary>只接受不反向的输入；同一 tick 内多次按键以最后一次为准。</summary>
        public void Queue(Dir d)
        {
            if (Body.Count > 1 && IsOpposite(d, Direction)) return;
            Queued = d;
        }

        static bool IsOpposite(Dir a, Dir b) { return ((int)a + 2) % 4 == (int)b; }

        static Vector2Int Delta(Dir d)
        {
            switch (d)
            {
                case Dir.Up: return new Vector2Int(0, 1);
                case Dir.Down: return new Vector2Int(0, -1);
                case Dir.Left: return new Vector2Int(-1, 0);
                default: return new Vector2Int(1, 0);
            }
        }

        /// <summary>金色果子倒计时，返回 true 表示这一帧刚刚失效。</summary>
        public bool TickBonus(float dt)
        {
            if (!FoodIsBonus) return false;
            BonusLeft -= dt;
            if (BonusLeft > 0f) return false;
            BonusLeft = 0f;
            FoodIsBonus = false;
            return true;
        }

        public StepResult Step()
        {
            if (IsDead) return StepResult.Dead;

            Direction = Queued;
            Vector2Int head = Body[0];
            Vector2Int next = head + Delta(Direction);

            if (Wrap)
            {
                next.x = (next.x % Width + Width) % Width;
                next.y = (next.y % Height + Height) % Height;
            }
            else if (next.x < 0 || next.x >= Width || next.y < 0 || next.y >= Height)
            {
                IsDead = true;
                return StepResult.Dead;
            }

            if (Obstacles.Contains(next))
            {
                IsDead = true;
                return StepResult.Dead;
            }

            bool eats = (next == Food);
            int tailIndex = Body.Count - 1;

            // 撞自己：不吃东西时尾巴会让位，所以尾节点不算障碍
            for (int i = 0; i < Body.Count; i++)
            {
                if (!eats && i == tailIndex) continue;
                if (Body[i] == next)
                {
                    IsDead = true;
                    return StepResult.Dead;
                }
            }

            Body.Insert(0, next);
            if (!eats) Body.RemoveAt(Body.Count - 1);

            AteThisStep = eats;
            if (!eats) return StepResult.Move;

            Eaten++;
            if (FoodIsBonus)
            {
                Score += Diff.Points * BonusMultiplier;
                FoodIsBonus = false;
                BonusLeft = 0f;
                SpawnFood();
                return StepResult.EatBonus;
            }

            Score += Diff.Points;
            if (Eaten % BonusEvery == 0)
            {
                // 每 5 个果子之后，下一颗升级成金色果子：换一个空格子并开始限时
                SpawnFood();
                FoodIsBonus = true;
                BonusLeft = BonusSeconds;
            }
            else
            {
                SpawnFood();
            }

            return StepResult.Eat;
        }

        public void SpawnFood()
        {
            FoodIsBonus = false;
            BonusLeft = 0f;

            for (int attempt = 0; attempt < 400; attempt++)
            {
                Vector2Int p = new Vector2Int(rng.Next(0, Width), rng.Next(0, Height));
                if (IsFree(p))
                {
                    Food = p;
                    return;
                }
            }

            // 极端情况（蛇几乎占满棋盘）：线性扫一遍
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector2Int p = new Vector2Int(x, y);
                    if (IsFree(p))
                    {
                        Food = p;
                        return;
                    }
                }
            }
        }

        bool IsFree(Vector2Int p)
        {
            if (Obstacles.Contains(p)) return false;
            for (int i = 0; i < Body.Count; i++)
                if (Body[i] == p) return false;
            return true;
        }

        public bool Occupies(Vector2Int p)
        {
            for (int i = 0; i < Body.Count; i++)
                if (Body[i] == p) return true;
            return false;
        }
    }
}
