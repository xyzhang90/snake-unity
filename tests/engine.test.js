/*
 * 引擎单元测试（不依赖任何测试框架，直接 `node tests/engine.test.js`）
 * 重点验证：移动/吃食/成长、撞墙、穿墙、障碍、撞自己、金果生成与超时。
 */
const assert = require('assert');
const path = require('path');
const Engine = require(path.join(__dirname, '..', 'docs', 'engine.js'));

const tests = [];
function test(name, fn) {
  tests.push({ name, fn });
}

function makeGame(diff, wrap, seed) {
  return new Engine.Game(diff, wrap, { seed: seed || 42 });
}

function isEmptyCell(g, x, y) {
  return g.isFree(x, y);
}

test('初始状态：3 节蛇身，从中间向右，食物不压在蛇身上', () => {
  const g = makeGame(1, false, 7);
  assert.strictEqual(g.body.length, 3);
  assert.deepStrictEqual(g.body[0], { x: 20, y: 10 });
  assert.strictEqual(g.dir, 'right');
  assert.ok(isEmptyCell(g, g.food.x, g.food.y), '食物必须落在空格');
});

test('正常移动：头前进一格，长度不变', () => {
  const g = makeGame(0, false, 1);
  assert.strictEqual(g.step(), 'move');
  assert.deepStrictEqual(g.body[0], { x: 21, y: 10 });
  assert.strictEqual(g.body.length, 3);
});

test('吃食物：长度 +1，分数按难度加分', () => {
  const g = makeGame(2, false, 3);
  g.food = { x: g.body[0].x + 1, y: g.body[0].y };
  g.foodIsBonus = false;
  assert.strictEqual(g.step(), 'eat');
  assert.strictEqual(g.body.length, 4);
  assert.strictEqual(g.score, Engine.DIFFICULTIES[2].points);
  assert.strictEqual(g.eaten, 1);
});

test('反向输入会被忽略（不能 180 度掉头）', () => {
  const g = makeGame(1, false, 5);
  g.queue('left');
  assert.strictEqual(g.queued, 'right');
  g.queue('up');
  assert.strictEqual(g.queued, 'up');
});

test('不穿墙模式：撞右边界判定死亡', () => {
  const g = makeGame(1, false, 9);
  g.body = [{ x: Engine.WIDTH - 1, y: 5 }, { x: Engine.WIDTH - 2, y: 5 }];
  g.dir = 'right';
  g.queued = 'right';
  assert.strictEqual(g.step(), 'dead');
  assert.strictEqual(g.dead, true);
});

test('穿墙模式：从右边界回到左边', () => {
  const g = makeGame(1, true, 9);
  g.body = [{ x: Engine.WIDTH - 1, y: 5 }, { x: Engine.WIDTH - 2, y: 5 }];
  g.dir = 'right';
  g.queued = 'right';
  g.food = { x: 3, y: 3 };
  assert.strictEqual(g.step(), 'move');
  assert.deepStrictEqual(g.body[0], { x: 0, y: 5 }, '穿墙后头应在 x=0');
});

test('HARD/HELL 才有障碍，且障碍格不出食物', () => {
  const easy = makeGame(1, false, 11);
  assert.strictEqual(Object.keys(easy.obstacles).length, 0);
  const hard = makeGame(2, false, 11);
  assert.strictEqual(Object.keys(hard.obstacles).length, 16, '4 个 2x2 方块 = 16 格');
  assert.ok(hard.obstacles[Engine.key(10, 5)]);
  assert.ok(!hard.isFree(10, 5));

  // 强行把食物刷到障碍里应该不允许（走 spawnFood 后食物必在空格）
  for (let i = 0; i < 5; i++) {
    hard.spawnFood();
    assert.ok(hard.isFree(hard.food.x, hard.food.y));
  }
});

test('撞障碍物判定死亡', () => {
  const g = makeGame(2, false, 13);
  // 头摆到障碍格左边一格，向右撞进去
  g.body = [{ x: 9, y: 5 }, { x: 8, y: 5 }];
  g.dir = 'right';
  g.queued = 'right';
  g.food = { x: 3, y: 3 };
  assert.strictEqual(g.step(), 'dead');
});

test('撞自己判定死亡；但尾格会让位，紧贴尾巴不算死', () => {
  const g = makeGame(1, false, 17);
  // 一个 U 形：头 (5,5) 向右会撞到身体
  g.body = [
    { x: 5, y: 5 }, { x: 6, y: 5 }, { x: 7, y: 5 },
    { x: 7, y: 6 }, { x: 6, y: 6 }, { x: 5, y: 6 }
  ];
  g.dir = 'up';
  g.queued = 'right';
  g.food = { x: 0, y: 0 };
  assert.strictEqual(g.step(), 'dead');
});

test('紧贴尾巴前进不判死（尾巴同时移开）', () => {
  const g = makeGame(1, false, 19);
  // 2x2 方块，头 (0,1) 向下走到 (0,0)，而 (0,0) 是尾巴
  g.body = [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 1, y: 0 }, { x: 0, y: 0 }];
  g.dir = 'right';
  g.queued = 'down';
  g.food = { x: 5, y: 5 };
  assert.strictEqual(g.step(), 'move');
  assert.strictEqual(g.body.length, 4);
});

test('每吃 5 个普通果子刷一次金果，金果 3 倍分', () => {
  const g = makeGame(1, false, 23);
  for (let i = 0; i < 5; i++) {
    g.foodIsBonus = false;
    g.food = { x: g.body[0].x + 1, y: g.body[0].y };
    g.step();
  }
  assert.strictEqual(g.eaten, 5);
  assert.strictEqual(g.foodIsBonus, true, '第 5 个之后应出现金果');
  assert.strictEqual(g.bonusLeft, Engine.BONUS_SECONDS * 1000);
  assert.ok(g.isFree(g.food.x, g.food.y));

  const scoreBefore = g.score;
  g.food = { x: g.body[0].x + 1, y: g.body[0].y };
  assert.strictEqual(g.step(), 'eatBonus');
  assert.strictEqual(g.score, scoreBefore + Engine.DIFFICULTIES[1].points * Engine.BONUS_MULT);
});

test('金果超时会退回普通果子', () => {
  const g = makeGame(1, false, 29);
  g.foodIsBonus = true;
  g.bonusLeft = 100;
  assert.strictEqual(g.tickBonus(50), false);
  assert.strictEqual(g.tickBonus(80), true);
  assert.strictEqual(g.foodIsBonus, false);
  assert.strictEqual(g.bonusLeft, 0);
});

test('长局稳定性：随机走 3000 步不抛异常，食物永远在空格', () => {
  const g = makeGame(1, true, 31);
  const dirs = ['up', 'right', 'down', 'left'];
  let steps = 0;
  for (let i = 0; i < 3000; i++) {
    if (i % 3 === 0) g.queue(dirs[Math.floor(g.rng() * 4)]);
    if (g.dead) break;
    g.step();
    steps++;
    assert.ok(g.isFree(g.food.x, g.food.y) || g.dead, '食物不该压在蛇身上');
  }
  assert.ok(steps > 100, '至少走了一百步');
});

let passed = 0;
for (const t of tests) {
  try {
    t.fn();
    passed++;
    console.log('  \u2713 ' + t.name);
  } catch (err) {
    console.error('  \u2717 ' + t.name);
    console.error('    ' + err.message);
    process.exitCode = 1;
  }
}
console.log('\n' + passed + '/' + tests.length + ' passed');
