const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await context.newPage();

  const logs = [];
  page.on('console', msg => logs.push({ type: msg.type(), text: msg.text() }));
  page.on('pageerror', err => logs.push({ type: 'pageerror', text: err.message }));

  // 1. 打开设置页
  await page.goto('http://localhost:7002/settings', { waitUntil: 'networkidle', timeout: 15000 });

  // 2. 点击 AI 提供方
  await page.locator('text=AI 提供方').first().click();
  await page.waitForTimeout(1500);

  // 3. 展开第一个 provider 卡片
  const cardName = page.locator('.fs-provider-card__name').first();
  await cardName.click();
  await page.waitForTimeout(2000);

  // 4. 看是否模型已加载，点击第一个模型的编辑按钮
  const editBtn = page.locator('.fs-model-item__actions button:has-text("编辑")').first();
  await editBtn.waitFor({ timeout: 5000 });
  await editBtn.click();
  await page.waitForTimeout(500);

  // 5. 截图：编辑表单展开（chip 组可见）
  await page.screenshot({ path: 'temp/chip-edit-form.png', fullPage: true });
  console.log('=== 截图 1: chip 编辑表单 ===');

  // 6. 测试能力 chip 点击：toggle vision、添加 reasoning
  const chips = page.locator('.fs-chip--toggle');
  const chipCount = await chips.count();
  console.log(`chip 个数: ${chipCount}`);

  // 获取所有 chip 的文本
  const chipTexts = [];
  for (let i = 0; i < chipCount; i++) {
    chipTexts.push(await chips.nth(i).textContent());
  }
  console.log('chip 文本:', chipTexts);

  // 点击第一个能力 chip（vision）toggle 掉
  const visionChip = page.locator('.fs-chip-group').first().locator('.fs-chip--toggle').filter({ hasText: 'vision' });
  if (await visionChip.count() > 0) {
    await visionChip.click();
    await page.waitForTimeout(200);
    console.log('点击了 vision chip');
  }

  // 点击 reasoning chip 添加
  const reasoningChip = page.locator('.fs-chip-group').first().locator('.fs-chip--toggle').filter({ hasText: 'reasoning' });
  if (await reasoningChip.count() > 0) {
    await reasoningChip.click();
    await page.waitForTimeout(200);
    console.log('点击了 reasoning chip');
  }

  // 7. 点击上下文中 32K (32768 / 1024 = 32)
  const ctx32k = page.locator('.fs-chip-group').nth(1).locator('.fs-chip--toggle').filter({ hasText: '32K' });
  if (await ctx32k.count() > 0) {
    await ctx32k.click();
    await page.waitForTimeout(200);
    console.log('点击了 32K 上下文');
  }

  // 8. 截图：chip 选中状态
  await page.screenshot({ path: 'temp/chip-edit-selected.png', fullPage: true });
  console.log('=== 截图 2: chip 选中状态 ===');

  // 9. 保存
  const saveBtn = page.locator('.fs-edit-actions button:has-text("保存")');
  await saveBtn.click();
  await page.waitForTimeout(1500);

  // 10. 截图：保存后模型卡片
  await page.screenshot({ path: 'temp/chip-edit-saved.png', fullPage: true });
  console.log('=== 截图 3: 保存后模型卡片 ===');

  // 11. 重新展开编辑，验证回显
  const editBtn2 = page.locator('.fs-model-item__actions button:has-text("编辑")').first();
  await editBtn2.click();
  await page.waitForTimeout(500);
  await page.screenshot({ path: 'temp/chip-edit-reopen.png', fullPage: true });
  console.log('=== 截图 4: 重新打开编辑验证回显 ===');

  // 打印控制台
  console.log('\n=== 控制台日志 ===');
  logs.forEach(l => console.log(`[${l.type}] ${l.text.substring(0, 200)}`));

  await browser.close();
})();
