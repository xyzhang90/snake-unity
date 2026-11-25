/*
 * 渲染 + 输入 + 音效。逻辑全在 engine.js，这里只负责“把状态画出来”。
 */
(function () {
  'use strict';

  var E = window.SnakeEngine;
  var W = E.WIDTH;
  var H = E.HEIGHT;

  var DIFF_NAMES = ['简单', '普通', '困难', '地狱'];
  var BEST_KEY = 'snake_unity_best';
  var SOUND_KEY = 'snake_unity_sound';
  var DIFF_KEY = 'snake_unity_diff';
  var WRAP_KEY = 'snake_unity_wrap';

  function $(id) { return document.getElementById(id); }

  var ui = {
    stage: $('stage'),
    canvas: $('game'),
    score: $('hudScore'),
    best: $('hudBest'),
    diff: $('hudDiff'),
    wrap: $('hudWrap'),
    bonusBox: $('hudBonusBox'),
    bonus: $('hudBonus'),
    menu: $('panelMenu'),
    pause: $('panelPause'),
    over: $('panelOver'),
    diffs: $('diffs'),
    wrapToggle: $('wrapToggle'),
    menuBest: $('menuBest'),
    overScore: $('overScore'),
    overBest: $('overBest'),
    overRecord: $('overRecord'),
    btnSound: $('btnSound'),
    dpad: $('dpad')
  };

  var ctx = ui.canvas.getContext('2d', { alpha: false });

  var state = {
    phase: 'menu',          // menu | playing | paused | over
    diffIndex: readInt(DIFF_KEY, 1),
    wrap: readBool(WRAP_KEY, false),
    best: readInt(BEST_KEY, 0),
    sound: readBool(SOUND_KEY, true),
    game: null,
    tickTimer: 0,
    clock: 0,
    shake: 0,
    flash: 0,
    overDelay: 0,
    newRecord: false
  };

  var segView = [];          // 蛇身渲染位置（浮点格子坐标）
  var particles = [];
  var layoutInfo = { cs: 1, bx: 0, by: 0 };

  // ---------- 持久化 ----------

  function readInt(key, def) {
    try {
      var v = parseInt(window.localStorage.getItem(key), 10);
      return isNaN(v) ? def : v;
    } catch (e) { return def; }
  }

  function readBool(key, def) {
    try {
      var v = window.localStorage.getItem(key);
      return v === null ? def : v === '1';
    } catch (e) { return def; }
  }

  function writeValue(key, value) {
    try { window.localStorage.setItem(key, String(value)); } catch (e) { /* 隐私模式忽略 */ }
  }

  // ---------- 音效（WebAudio 现场合成，不带音频文件） ----------

  var audio = {
    ctx: null,
    on: true,
    unlock: function () {
      if (!this.on) return;
      if (!this.ctx) {
        var AC = window.AudioContext || window.webkitAudioContext;
        if (!AC) return;
        this.ctx = new AC();
      }
      if (this.ctx.state === 'suspended') this.ctx.resume();
    },
    blip: function (o) {
      if (!this.on || !this.ctx) return;
      var c = this.ctx;
      var t0 = c.currentTime;
      var osc = c.createOscillator();
      var gain = c.createGain();
      osc.type = o.type || 'sine';
      osc.frequency.setValueAtTime(o.freq, t0);
      if (o.to) osc.frequency.exponentialRampToValueAtTime(Math.max(30, o.to), t0 + o.dur);
      gain.gain.setValueAtTime(0.0001, t0);
      gain.gain.exponentialRampToValueAtTime(o.gain || 0.16, t0 + 0.008);
      gain.gain.exponentialRampToValueAtTime(0.0001, t0 + o.dur);
      osc.connect(gain);
      gain.connect(c.destination);
      osc.start(t0);
      osc.stop(t0 + o.dur + 0.03);
    },
    eat: function () { this.blip({ freq: 520, to: 940, dur: 0.09, gain: 0.16, type: 'triangle' }); },
    bonus: function () {
      this.blip({ freq: 880, dur: 0.08, gain: 0.2, type: 'square' });
      var self = this;
      setTimeout(function () { self.blip({ freq: 1320, dur: 0.12, gain: 0.18, type: 'square' }); }, 70);
    },
    die: function () { this.blip({ freq: 420, to: 70, dur: 0.5, gain: 0.2, type: 'sawtooth' }); },
    tap: function () { this.blip({ freq: 660, dur: 0.05, gain: 0.1, type: 'sine' }); }
  };

  // ---------- 尺寸与坐标 ----------

  function layout() {
    var cssW = ui.stage.clientWidth;
    var cssH = ui.stage.clientHeight || (cssW / 2);
    var dpr = window.devicePixelRatio || 1;

    ui.canvas.width = Math.round(cssW * dpr);
    ui.canvas.height = Math.round(cssH * dpr);
    ui.canvas.style.height = cssH + 'px';

    var pad = Math.max(8, Math.min(cssW, cssH) * 0.03);
    var cs = Math.min((cssW - pad * 2) / W, (cssH - pad * 2) / H);
    layoutInfo.cs = cs;
    layoutInfo.bx = (cssW - cs * W) / 2;
    layoutInfo.by = (cssH - cs * H) / 2;
    layoutInfo.w = cssW;
    layoutInfo.h = cssH;
  }

  function cellCenter(fx, fy) {
    var cs = layoutInfo.cs;
    return {
      x: layoutInfo.bx + (fx + 0.5) * cs,
      y: layoutInfo.by + (H - 0.5 - fy) * cs
    };
  }

  function roundRect(x, y, w, h, r) {
    r = Math.min(r, w / 2, h / 2);
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
  }

  function lerpColor(c1, c2, t) {
    return 'rgb(' + Math.round(c1[0] + (c2[0] - c1[0]) * t) + ',' +
      Math.round(c1[1] + (c2[1] - c1[1]) * t) + ',' +
      Math.round(c1[2] + (c2[2] - c1[2]) * t) + ')';
  }

  var HEAD_RGB = [124, 255, 200];
  var BODY_RGB_A = [79, 224, 180];
  var BODY_RGB_B = [26, 140, 120];

  // ---------- 流程 ----------

  function startGame() {
    audio.unlock();
    state.game = new E.Game(state.diffIndex, state.wrap);
    state.phase = 'playing';
    state.tickTimer = state.game.diff.tick;
    state.overDelay = 0;
    state.newRecord = false;
    state.shake = 0;
    state.flash = 0;
    segView.length = 0;
    particles.length = 0;
    hidePanels();
    updateHud();
    audio.tap();
  }

  function backToMenu() {
    state.phase = 'menu';
    state.game = new E.Game(state.diffIndex, state.wrap);
    segView.length = 0;
    particles.length = 0;
    hidePanels();
    buildDiffButtons();
    ui.menuBest.textContent = state.best;
    ui.wrapToggle.checked = state.wrap;
    ui.menu.hidden = false;
    updateHud();
  }

  function hidePanels() {
    ui.menu.hidden = true;
    ui.pause.hidden = true;
    ui.over.hidden = true;
  }

  function togglePause() {
    if (state.phase === 'playing') {
      state.phase = 'paused';
      ui.pause.hidden = false;
      audio.tap();
    } else if (state.phase === 'paused') {
      state.phase = 'playing';
      ui.pause.hidden = true;
      audio.tap();
    }
  }

  function onDead() {
    state.phase = 'over';
    state.overDelay = 450;
    state.shake = 1;
    state.flash = 1;
    audio.die();

    var g = state.game;
    burst(g.body[0].x, g.body[0].y, '#ff5a63', 22, 3.2);
    burst(g.body[0].x, g.body[0].y, '#ffffff', 10, 2.4);

    state.newRecord = g.score > state.best;
    if (state.newRecord) {
      state.best = g.score;
      writeValue(BEST_KEY, state.best);
    }
  }

  function showOverPanel() {
    ui.overScore.textContent = state.game.score;
    ui.overBest.textContent = state.best;
    ui.overRecord.hidden = !state.newRecord;
    ui.over.hidden = false;
    updateHud();
  }

  function step() {
    var g = state.game;
    var r = g.step();
    var head = g.body[0];

    if (r === 'eat') {
      audio.eat();
      burst(head.x, head.y, '#ff5a63', 12, 2.6);
    } else if (r === 'eatBonus') {
      audio.bonus();
      burst(head.x, head.y, '#ffc642', 24, 3.6);
    } else if (r === 'dead') {
      onDead();
    }
  }

  // ---------- 粒子 ----------

  function burst(cx, cy, color, count, speed) {
    for (var i = 0; i < count; i++) {
      var a = Math.random() * Math.PI * 2;
      var s = (0.4 + Math.random() * 0.6) * (speed || 2.5);
      particles.push({
        x: cx,
        y: cy,
        vx: Math.cos(a) * s,
        vy: Math.sin(a) * s,
        life: 0.28 + Math.random() * 0.25,
        max: 0.53,
        color: color,
        size: 0.1 + Math.random() * 0.16
      });
    }
  }

  function updateParticles(dt) {
    for (var i = particles.length - 1; i >= 0; i--) {
      var p = particles[i];
      p.life -= dt;
      if (p.life <= 0) { particles.splice(i, 1); continue; }
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.vx *= 0.92;
      p.vy *= 0.92;
    }
  }

  // ---------- 每帧更新 ----------

  function update(dtMs) {
    var dt = dtMs / 1000;
    state.clock += dtMs;

    state.shake = Math.max(0, state.shake - dt * 4.2);
    state.flash = Math.max(0, state.flash - dt * 2.4);

    if (state.phase === 'playing') {
      var g = state.game;
      g.tickBonus(dtMs);
      state.tickTimer -= dtMs;

      var guard = 0;
      while (state.tickTimer <= 0 && guard < 3 && state.phase === 'playing') {
        state.tickTimer += g.diff.tick;
        step();
        guard++;
      }
      if (state.phase === 'playing') updateHud();
    }

    if (state.phase === 'over' && state.overDelay > 0) {
      state.overDelay -= dtMs;
      if (state.overDelay <= 0) showOverPanel();
    }

    syncSegments(dt);
    updateParticles(dt);
  }

  function syncSegments(dt) {
    var g = state.game;
    var body = g.body;

    while (segView.length < body.length) {
      var src = body[segView.length];
      segView.push({ x: src.x, y: src.y });
    }
    while (segView.length > body.length) segView.pop();

    var k = 1 - Math.exp(-14 * dt);
    for (var i = 0; i < segView.length; i++) {
      segView[i].x += (body[i].x - segView[i].x) * k;
      segView[i].y += (body[i].y - segView[i].y) * k;
    }
  }

  // ---------- 绘制 ----------

  function draw() {
    var dpr = window.devicePixelRatio || 1;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, layoutInfo.w, layoutInfo.h);

    ctx.fillStyle = '#080d17';
    ctx.fillRect(0, 0, layoutInfo.w, layoutInfo.h);

    var sx = 0;
    var sy = 0;
    if (state.shake > 0) {
      var amp = state.shake * state.shake * 7;
      sx = (Math.random() * 2 - 1) * amp;
      sy = (Math.random() * 2 - 1) * amp;
    }

    ctx.save();
    ctx.translate(sx, sy);

    drawBoard();
    drawObstacles();
    drawFood();
    drawSnake();
    drawParticles();

    ctx.restore();

    if (state.flash > 0) {
      ctx.fillStyle = 'rgba(255,90,99,' + (state.flash * 0.3).toFixed(3) + ')';
      ctx.fillRect(0, 0, layoutInfo.w, layoutInfo.h);
    }
  }

  function drawBoard() {
    var cs = layoutInfo.cs;
    var bw = cs * W;
    var bh = cs * H;
    var bx = layoutInfo.bx;
    var by = layoutInfo.by;

    ctx.save();
    ctx.shadowColor = 'rgba(124,255,200,0.22)';
    ctx.shadowBlur = 26;
    ctx.fillStyle = '#0c1424';
    roundRect(bx, by, bw, bh, Math.max(6, cs * 0.5));
    ctx.fill();
    ctx.restore();

    // 网格
    ctx.strokeStyle = 'rgba(120,190,255,0.07)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    for (var x = 1; x < W; x++) {
      var px = bx + x * cs;
      ctx.moveTo(px, by);
      ctx.lineTo(px, by + bh);
    }
    for (var y = 1; y < H; y++) {
      var py = by + y * cs;
      ctx.moveTo(bx, py);
      ctx.lineTo(bx + bw, py);
    }
    ctx.stroke();

    // 边框
    ctx.strokeStyle = 'rgba(124,255,200,0.22)';
    ctx.lineWidth = 1.5;
    roundRect(bx + 0.5, by + 0.5, bw - 1, bh - 1, Math.max(6, cs * 0.5));
    ctx.stroke();
  }

  function drawObstacles() {
    var cs = layoutInfo.cs;
    for (var k in state.game.obstacles) {
      if (!Object.prototype.hasOwnProperty.call(state.game.obstacles, k)) continue;
      var parts = k.split(',');
      var c = cellCenter(+parts[0], +parts[1]);
      var size = cs * 0.94;
      var r = cs * 0.16;

      ctx.fillStyle = '#39465f';
      roundRect(c.x - size / 2, c.y - size / 2, size, size, r);
      ctx.fill();

      ctx.fillStyle = 'rgba(255,255,255,0.09)';
      roundRect(c.x - size / 2 + 1, c.y - size / 2 + 1, size - 2, size * 0.4, r * 0.8);
      ctx.fill();
    }
  }

  function drawSnake() {
    if (state.phase === 'menu') return;
    var cs = layoutInfo.cs;
    var n = segView.length;

    ctx.save();
    for (var i = n - 1; i >= 0; i--) {
      var p = segView[i];
      var c = cellCenter(p.x, p.y);
      var isHead = i === 0;
      var size = cs * (isHead ? 0.94 : 0.86);
      var r = size * 0.32;

      if (isHead) {
        ctx.shadowColor = 'rgba(124,255,200,0.55)';
        ctx.shadowBlur = cs * 0.8;
      } else {
        ctx.shadowBlur = 0;
      }

      var t = n <= 2 ? 0 : (i - 1) / (n - 2);
      ctx.fillStyle = isHead ? 'rgb(124,255,200)' : lerpColor(BODY_RGB_A, BODY_RGB_B, Math.max(0, Math.min(1, t)));
      roundRect(c.x - size / 2, c.y - size / 2, size, size, r);
      ctx.fill();

      if (isHead) {
        ctx.shadowBlur = 0;
        drawEyes(c, cs);
      }
    }
    ctx.restore();
  }

  function drawEyes(c, cs) {
    var d = state.game.dir;
    var dx = d === 'left' ? -1 : d === 'right' ? 1 : 0;
    var dy = d === 'down' ? -1 : d === 'up' ? 1 : 0;
    var px = -dy;   // 与前进方向垂直
    var py = dx;
    var off = cs * 0.17;
    var fwd = cs * 0.19;
    var rad = cs * 0.075;

    var ex = c.x + dx * fwd;
    var ey = c.y - dy * fwd;

    ctx.fillStyle = '#06130f';
    ctx.beginPath();
    ctx.arc(ex + px * off, ey + py * off, rad, 0, Math.PI * 2);
    ctx.arc(ex - px * off, ey - py * off, rad, 0, Math.PI * 2);
    ctx.fill();
  }

  function drawFood() {
    var g = state.game;
    if (state.phase === 'menu' || !g.food) return;

    var cs = layoutInfo.cs;
    var c = cellCenter(g.food.x, g.food.y);
    var clock = state.clock / 1000;

    ctx.save();

    if (g.foodIsBonus) {
      var t = Math.max(0, Math.min(1, g.bonusLeft / (E.BONUS_SECONDS * 1000)));
      var rOut = cs * 0.4 * (0.94 + 0.06 * Math.sin(clock * 10));

      // 倒计时环
      ctx.strokeStyle = 'rgba(255,198,66,0.9)';
      ctx.lineWidth = cs * 0.07;
      ctx.beginPath();
      ctx.arc(c.x, c.y, cs * 0.46, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * t);
      ctx.stroke();

      ctx.shadowColor = 'rgba(255,198,66,0.85)';
      ctx.shadowBlur = cs * 1.2;
      ctx.fillStyle = '#ffc642';
      starPath(c.x, c.y, 5, rOut, rOut * 0.44, clock * 0.9);
      ctx.fill();
    } else {
      var pulse = 0.9 + 0.1 * Math.sin(clock * 4);
      ctx.shadowColor = 'rgba(255,90,99,0.8)';
      ctx.shadowBlur = cs * 1.1;
      ctx.fillStyle = '#ff5a63';
      ctx.beginPath();
      ctx.arc(c.x, c.y, cs * 0.33 * pulse, 0, Math.PI * 2);
      ctx.fill();

      ctx.shadowBlur = 0;
      ctx.fillStyle = 'rgba(255,255,255,0.55)';
      ctx.beginPath();
      ctx.arc(c.x - cs * 0.1, c.y - cs * 0.11, cs * 0.1, 0, Math.PI * 2);
      ctx.fill();
    }

    ctx.restore();
  }

  function starPath(cx, cy, spikes, outer, inner, rot) {
    var step = Math.PI / spikes;
    ctx.beginPath();
    for (var i = 0; i < spikes * 2; i++) {
      var r = (i % 2 === 0) ? outer : inner;
      var a = rot + i * step - Math.PI / 2;
      var x = cx + Math.cos(a) * r;
      var y = cy + Math.sin(a) * r;
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    }
    ctx.closePath();
  }

  function drawParticles() {
    var cs = layoutInfo.cs;
    for (var i = 0; i < particles.length; i++) {
      var p = particles[i];
      var c = cellCenter(p.x, p.y);
      var alpha = Math.max(0, Math.min(1, p.life / p.max));
      ctx.globalAlpha = alpha;
      ctx.fillStyle = p.color;
      ctx.beginPath();
      ctx.arc(c.x, c.y, cs * p.size * Math.max(0.35, alpha), 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  // ---------- HUD ----------

  function updateHud() {
    var g = state.game;
    if (!g) return;
    ui.score.textContent = g.score;
    ui.best.textContent = state.best;
    ui.diff.textContent = DIFF_NAMES[state.diffIndex] || '普通';
    ui.wrap.textContent = state.wrap ? '开' : '关';

    if (g.foodIsBonus) {
      ui.bonusBox.hidden = false;
      ui.bonus.textContent = (g.bonusLeft / 1000).toFixed(1) + 's';
    } else {
      ui.bonusBox.hidden = true;
    }
    ui.menuBest.textContent = state.best;
  }

  // ---------- 菜单难度按钮 ----------

  function buildDiffButtons() {
    ui.diffs.innerHTML = '';
    E.DIFFICULTIES.forEach(function (d, i) {
      var b = document.createElement('button');
      b.type = 'button';
      b.className = 'diff';
      b.setAttribute('aria-pressed', i === state.diffIndex ? 'true' : 'false');
      b.innerHTML = '<span><span class="key">' + (i + 1) + '</span><b>' + DIFF_NAMES[i] +
        '</b> <small>' + d.label + '</small></span><small>' + d.hint + '</small>';
      b.addEventListener('click', function () {
        state.diffIndex = i;
        writeValue(DIFF_KEY, i);
        buildDiffButtons();
        state.game = new E.Game(state.diffIndex, state.wrap);
        segView.length = 0;
        updateHud();
        audio.unlock();
        audio.tap();
      });
      ui.diffs.appendChild(b);
    });
  }

  // ---------- 输入 ----------

  var KEYMAP = {
    arrowup: 'up', w: 'up', arrowdown: 'down', s: 'down',
    arrowleft: 'left', a: 'left', arrowright: 'right', d: 'right'
  };

  window.addEventListener('keydown', function (e) {
    var k = (e.key || '').toLowerCase();

    if (['arrowup', 'arrowdown', 'arrowleft', 'arrowright', ' '].indexOf(k) >= 0) e.preventDefault();

    audio.unlock();

    if (state.phase === 'menu') {
      if (k >= '1' && k <= '4') {
        var idx = parseInt(k, 10) - 1;
        state.diffIndex = idx;
        writeValue(DIFF_KEY, idx);
        buildDiffButtons();
        state.game = new E.Game(state.diffIndex, state.wrap);
        segView.length = 0;
        updateHud();
        audio.tap();
        return;
      }
      if (k === 'w' || k === 'arrowup') {
        toggleWrap();
        return;
      }
      if (k === 'enter' || k === ' ') { startGame(); return; }
      return;
    }

    if (k === 'escape') { backToMenu(); return; }

    if (state.phase === 'over') {
      if (k === 'r' || k === 'enter' || k === ' ') startGame();
      return;
    }

    if (k === 'r') { startGame(); return; }
    if (k === ' ') { togglePause(); return; }

    if (state.phase !== 'playing') return;

    var dir = KEYMAP[k];
    if (dir) state.game.queue(dir);
  });

  function toggleWrap() {
    state.wrap = !state.wrap;
    writeValue(WRAP_KEY, state.wrap ? '1' : '0');
    ui.wrapToggle.checked = state.wrap;
    state.game = new E.Game(state.diffIndex, state.wrap);
    segView.length = 0;
    updateHud();
    audio.tap();
  }

  // 滑动 / 点击
  var pointer = { active: false, x: 0, y: 0, t: 0 };

  ui.canvas.addEventListener('pointerdown', function (e) {
    pointer.active = true;
    pointer.x = e.clientX;
    pointer.y = e.clientY;
    pointer.t = Date.now();
    audio.unlock();
  });

  ui.canvas.addEventListener('pointerup', function (e) {
    if (!pointer.active) return;
    pointer.active = false;

    var dx = e.clientX - pointer.x;
    var dy = e.clientY - pointer.y;
    var threshold = Math.max(24, Math.min(window.innerWidth, window.innerHeight) * 0.05);

    if (Math.abs(dx) < threshold && Math.abs(dy) < threshold) {
      if (state.phase === 'paused') togglePause();
      return;
    }

    if (state.phase !== 'playing') return;
    if (Math.abs(dx) > Math.abs(dy)) state.game.queue(dx > 0 ? 'right' : 'left');
    else state.game.queue(dy > 0 ? 'down' : 'up');
  });

  // 手机方向键
  Array.prototype.forEach.call(ui.dpad.querySelectorAll('button'), function (b) {
    var fire = function (e) {
      e.preventDefault();
      audio.unlock();
      if (state.phase === 'playing') state.game.queue(b.dataset.dir);
    };
    b.addEventListener('pointerdown', fire);
  });

  // 按钮
  $('btnStart').addEventListener('click', startGame);
  $('btnAgain').addEventListener('click', startGame);
  $('btnResume').addEventListener('click', togglePause);
  $('btnMenu').addEventListener('click', backToMenu);
  ui.wrapToggle.addEventListener('change', function () {
    state.wrap = ui.wrapToggle.checked;
    writeValue(WRAP_KEY, state.wrap ? '1' : '0');
    state.game = new E.Game(state.diffIndex, state.wrap);
    segView.length = 0;
    updateHud();
  });

  // 音效开关
  function refreshSoundButton() {
    ui.btnSound.textContent = '音效 ' + (state.sound ? '开' : '关');
    audio.on = state.sound;
  }

  ui.btnSound.addEventListener('click', function () {
    state.sound = !state.sound;
    writeValue(SOUND_KEY, state.sound ? '1' : '0');
    refreshSoundButton();
    audio.unlock();
    audio.tap();
  });

  // ---------- 启动 ----------

  state.game = new E.Game(state.diffIndex, state.wrap);
  ui.wrapToggle.checked = state.wrap;
  buildDiffButtons();
  refreshSoundButton();
  ui.menuBest.textContent = state.best;
  updateHud();

  layout();
  window.addEventListener('resize', layout);
  if (window.ResizeObserver) new window.ResizeObserver(layout).observe(ui.stage);

  var last = performance.now();
  function frame(now) {
    var dt = Math.min(120, Math.max(0, now - last));
    last = now;
    update(dt);
    draw();
    window.requestAnimationFrame(frame);
  }
  window.requestAnimationFrame(frame);

  // 方便调试
  window.__snake = { state: state, startGame: startGame, layoutInfo: layoutInfo };
})();
