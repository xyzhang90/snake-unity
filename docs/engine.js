/*
 * Snake 引擎（纯逻辑，不依赖 DOM）
 * 规则与 Unity 工程里的 Assets/Scripts/GameCore.cs 一一对应，
 * 修改玩法时两边要同步改。
 */
(function (global) {
  'use strict';

  var WIDTH = 40;
  var HEIGHT = 20;
  var BONUS_EVERY = 5;        // 每吃 5 个果子出一次金果
  var BONUS_SECONDS = 6;      // 金果存活秒数
  var BONUS_MULT = 3;

  var DIFFICULTIES = [
    { label: 'EASY', tick: 300, points: 1, obstacles: false, hint: '0.30s / 1 分' },
    { label: 'NORMAL', tick: 180, points: 2, obstacles: false, hint: '0.18s / 2 分' },
    { label: 'HARD', tick: 110, points: 3, obstacles: true, hint: '0.11s / 3 分 · 4 个障碍' },
    { label: 'HELL', tick: 70, points: 4, obstacles: true, hint: '0.07s / 4 分 · 4 个障碍' }
  ];

  var DELTAS = {
    up: { x: 0, y: 1 },
    right: { x: 1, y: 0 },
    down: { x: 0, y: -1 },
    left: { x: -1, y: 0 }
  };

  var OPPOSITE = { up: 'down', down: 'up', left: 'right', right: 'left' };

  function key(x, y) { return x + ',' + y; }

  // 可复现的随机数（测试用），默认用 Math.random
  function mulberry32(seed) {
    var a = seed >>> 0;
    return function () {
      a += 0x6D2B79F5;
      var t = a;
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  function Game(diffIndex, wrap, options) {
    options = options || {};
    this.rng = options.seed != null ? mulberry32(options.seed) : Math.random;
    this.diffIndex = clampDiff(diffIndex);
    this.wrap = !!wrap;
    this.reset(this.diffIndex, this.wrap);
  }

  function clampDiff(i) {
    i = Math.floor(i);
    if (!isFinite(i)) i = 1;
    return Math.max(0, Math.min(DIFFICULTIES.length - 1, i));
  }

  Game.prototype.reset = function (diffIndex, wrap) {
    if (diffIndex != null) this.diffIndex = clampDiff(diffIndex);
    if (wrap != null) this.wrap = !!wrap;

    this.diff = DIFFICULTIES[this.diffIndex];
    this.body = [];
    this.obstacles = {};

    var cx = WIDTH / 2;             // 20
    var cy = HEIGHT / 2;            // 10
    this.body.push({ x: cx, y: cy });
    this.body.push({ x: cx - 1, y: cy });
    this.body.push({ x: cx - 2, y: cy });

    this.dir = 'right';
    this.queued = 'right';

    this.score = 0;
    this.eaten = 0;
    this.dead = false;
    this.foodIsBonus = false;
    this.bonusLeft = 0;

    if (this.diff.obstacles) {
      this.addBlock(10, 5);
      this.addBlock(10, HEIGHT - 7);
      this.addBlock(WIDTH - 12, 5);
      this.addBlock(WIDTH - 12, HEIGHT - 7);
    }

    this.spawnFood();
  };

  Game.prototype.addBlock = function (x, y) {
    for (var dy = 0; dy < 2; dy++) {
      for (var dx = 0; dx < 2; dx++) {
        var nx = x + dx;
        var ny = y + dy;
        if (nx > 0 && nx < WIDTH - 1 && ny > 0 && ny < HEIGHT - 1) {
          this.obstacles[key(nx, ny)] = true;
        }
      }
    }
  };

  Game.prototype.queue = function (dir) {
    if (!DELTAS[dir]) return;
    if (this.body.length > 1 && OPPOSITE[dir] === this.dir) return;
    this.queued = dir;
  };

  Game.prototype.isFree = function (x, y) {
    if (this.obstacles[key(x, y)]) return false;
    for (var i = 0; i < this.body.length; i++) {
      if (this.body[i].x === x && this.body[i].y === y) return false;
    }
    return true;
  };

  Game.prototype.spawnFood = function () {
    this.foodIsBonus = false;
    this.bonusLeft = 0;

    for (var attempt = 0; attempt < 400; attempt++) {
      var x = Math.floor(this.rng() * WIDTH);
      var y = Math.floor(this.rng() * HEIGHT);
      if (this.isFree(x, y)) {
        this.food = { x: x, y: y };
        return;
      }
    }

    for (var j = 0; j < HEIGHT; j++) {
      for (var i = 0; i < WIDTH; i++) {
        if (this.isFree(i, j)) {
          this.food = { x: i, y: j };
          return;
        }
      }
    }
  };

  Game.prototype.tickBonus = function (dtMs) {
    if (!this.foodIsBonus) return false;
    this.bonusLeft -= dtMs;
    if (this.bonusLeft > 0) return false;
    this.bonusLeft = 0;
    this.foodIsBonus = false;
    return true;
  };

  // 返回值：'move' | 'eat' | 'eatBonus' | 'dead'
  Game.prototype.step = function () {
    if (this.dead) return 'dead';

    this.dir = this.queued;
    var head = this.body[0];
    var d = DELTAS[this.dir];
    var nx = head.x + d.x;
    var ny = head.y + d.y;

    if (this.wrap) {
      nx = ((nx % WIDTH) + WIDTH) % WIDTH;
      ny = ((ny % HEIGHT) + HEIGHT) % HEIGHT;
    } else if (nx < 0 || nx >= WIDTH || ny < 0 || ny >= HEIGHT) {
      this.dead = true;
      return 'dead';
    }

    if (this.obstacles[key(nx, ny)]) {
      this.dead = true;
      return 'dead';
    }

    var eats = (this.food && this.food.x === nx && this.food.y === ny);
    var tailIndex = this.body.length - 1;

    for (var i = 0; i < this.body.length; i++) {
      if (!eats && i === tailIndex) continue;
      if (this.body[i].x === nx && this.body[i].y === ny) {
        this.dead = true;
        return 'dead';
      }
    }

    this.body.unshift({ x: nx, y: ny });
    if (!eats) this.body.pop();

    if (!eats) return 'move';

    this.eaten++;
    if (this.foodIsBonus) {
      this.score += this.diff.points * BONUS_MULT;
      this.foodIsBonus = false;
      this.bonusLeft = 0;
      this.spawnFood();
      return 'eatBonus';
    }

    this.score += this.diff.points;
    this.spawnFood();
    if (this.eaten % BONUS_EVERY === 0) {
      this.foodIsBonus = true;
      this.bonusLeft = BONUS_SECONDS * 1000;
    }
    return 'eat';
  };

  var api = {
    Game: Game,
    DIFFICULTIES: DIFFICULTIES,
    WIDTH: WIDTH,
    HEIGHT: HEIGHT,
    BONUS_EVERY: BONUS_EVERY,
    BONUS_SECONDS: BONUS_SECONDS,
    BONUS_MULT: BONUS_MULT,
    key: key
  };

  global.SnakeEngine = api;
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
