import { test, expect } from '@playwright/test';
import fs from 'fs';
import path from 'path';

const BASE_URL = process.env.BASE_URL || 'https://demo.staybot.co.za/';
const ADMIN_EMAIL = process.env.ADMIN_EMAIL || 'test@admin.com';
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'Password123!';
const AUTH_STATE_PATH = path.join(process.cwd(), 'tools', 'snapshots', '.auth', 'admin.json');

// Preload saved auth state if available so we don't have to login again
try {
  if (fs.existsSync(AUTH_STATE_PATH)) {
    test.use({ storageState: AUTH_STATE_PATH });
    console.log('[auth] Using saved authentication state');
  }
} catch {}

async function tryLogin(page) {
  console.log(`[login] Attempting login with email: ${ADMIN_EMAIL}`);
  const candidates = ['/auth/login', '/auth', '/login'];
  const start = Date.now();
  const overallTimeoutMs = 30000;
  
  for (const loginPath of candidates) {
    const loginUrl = new URL(loginPath, BASE_URL).toString();
    console.log(`[login] Trying login path: ${loginUrl}`);
    await page.goto(loginUrl, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000); // Wait for page to settle

    const maybeSidebar = page.locator('nav, aside, .sidebar, [role="navigation"]').first();
    if (await maybeSidebar.isVisible().catch(() => false)) {
      console.log('[login] Already logged in (sidebar visible)');
      return;
    }

    const email = page.locator('input[type="email"], input[name="email"], input[formcontrolname="email"], input[placeholder*="email" i]').first();
    const password = page.locator('input[type="password"], input[name="password"], input[formcontrolname="password"], input[placeholder*="password" i]').first();
    const submit = page.locator('button[type="submit"], button:has-text("Login"), button:has-text("Sign in"), button:has-text("Log in"), input[type="submit"]').first();

    const emailVisible = await email.isVisible().catch(() => false);
    const passwordVisible = await password.isVisible().catch(() => false);
    console.log(`[login] Email field visible: ${emailVisible}, Password field visible: ${passwordVisible}`);

    // Check if credentials are already filled
    const emailValue = await email.inputValue().catch(() => '');
    const passwordValue = await password.inputValue().catch(() => '');
    console.log(`[login] Email pre-filled: ${emailValue ? 'yes' : 'no'}, Password pre-filled: ${passwordValue ? 'yes' : 'no'}`);
    
    if (emailVisible && passwordVisible) {
      // Only fill if not already populated
      if (!emailValue) {
        console.log(`[login] Filling email...`);
        await email.fill(ADMIN_EMAIL);
      }
      if (!passwordValue) {
        console.log(`[login] Filling password...`);
        await password.fill(ADMIN_PASSWORD);
      }
      
      const submitVisible = await submit.isVisible().catch(() => false);
      console.log(`[login] Submit button visible: ${submitVisible}`);
      
      if (submitVisible) {
        console.log('[login] Clicking submit...');
        await submit.click();
        
        // Wait longer for navigation
        await page.waitForTimeout(5000);
        
        const currentUrl = page.url();
        console.log(`[login] Current URL after submit: ${currentUrl}`);
        
        const sidebarNowVisible = await maybeSidebar.isVisible().catch(() => false);
        const notOnAuthPage = !/\/auth(\/|$)|\/login(\/|$)/.test(new URL(currentUrl).pathname);
        
        console.log(`[login] Sidebar visible: ${sidebarNowVisible}, Not on auth page: ${notOnAuthPage}`);
        
        if (sidebarNowVisible || notOnAuthPage) {
          console.log('[login] Login successful!');
          try {
            const dir = path.dirname(AUTH_STATE_PATH);
            if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
            await page.context().storageState({ path: AUTH_STATE_PATH });
            console.log('[login] Auth state saved');
          } catch (e) {
            console.log(`[login] Failed to save auth state: ${e}`);
          }
          return;
        } else {
          console.log('[login] Login failed - still on auth page');
        }
      }
    }
    if (Date.now() - start > overallTimeoutMs) {
      console.log('[login] Login timeout exceeded');
      break;
    }
  }
  console.log('[login] All login attempts failed');
}

function ensureScreenshotDir() {
  // When run with --prefix tools/snapshots, CWD is repo root
  const repoRoot = path.resolve(process.cwd());
  const dir = path.join(repoRoot, 'docs', 'screenshots');
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  return dir;
}

function ensureAnnotatedDir() {
  const repoRoot = path.resolve(process.cwd());
  const dir = path.join(repoRoot, 'docs', 'screenshots', 'annotated');
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  return dir;
}

async function annotateIfEnabled(page, filePath: string, name: string) {
  if (!process.env.ANNOTATE || process.env.ANNOTATE === '0' || process.env.ANNOTATE === 'false') return;
  let sharpMod: any;
  try { sharpMod = require('sharp'); } catch { return; }
  const addSelectors = [
    'button:has-text("Add")',
    'button:has-text("New")',
    'button:has-text("Create")',
    'a:has-text("Add")',
    'a:has-text("New")',
    '[data-testid="add"]',
    '[aria-label="Add"]'
  ];
  const editSelectors = [
    'a:has-text("Edit")',
    'button:has-text("Edit")',
    '[data-testid="edit"]',
    'a[href*="/edit"], a[href*="/update"]'
  ];
  const viewSelectors = [
    'a:has-text("View")',
    'button:has-text("View")',
    '[data-testid="view"]',
    'a[href*="/view"], a[href*="/details"], a[href*="/show"]'
  ];
  // Broader, common controls so we always have useful markers
  const miscSelectors = [
    'input[placeholder*="search" i]',
    'button:has-text("Export")',
    'button:has-text("Print")',
    'button:has-text("Save")',
    '[role="combobox"]',
    'input[type="date"], .mat-date-range-input-start-wrapper input, .mat-date-range-input-end-wrapper input',
    '.breadcrumb .active, nav.breadcrumb .active',
    '.sidebar .active a, nav .active a'
  ];
  const entries: Array<{ x: number; y: number; width: number; height: number; label: string }>=[];
  const scroll = await page.evaluate(() => ({ x: window.scrollX || 0, y: window.scrollY || 0 }));
  async function pushFirst(selectors: string[], label: string) {
    for (const sel of selectors) {
      const el = page.locator(sel).first();
      if (await el.count()) {
        const box = await el.boundingBox();
        if (box) { entries.push({ x: box.x + scroll.x, y: box.y + scroll.y, width: box.width, height: box.height, label }); }
        break;
      }
    }
  }
  await pushFirst(addSelectors, 'Add/New');
  await pushFirst(editSelectors, 'Edit');
  await pushFirst(viewSelectors, 'View');
  await pushFirst(miscSelectors, 'Primary/Filter');
  const overwrite = process.env.ANNOTATE_OVERWRITE === '1' || process.env.ANNOTATE_OVERWRITE === 'true';
  const annotatedDir = ensureAnnotatedDir();
  const outPath = overwrite ? filePath : path.join(annotatedDir, `${name}.png`);
  const tmpOutPath = overwrite ? `${filePath}.tmp` : outPath;
  if (!entries.length) {
    // Draw a small label so annotation is always visible
    const meta = await sharpMod(filePath).metadata();
    const w = meta.width || 1200;
    const h = meta.height || 800;
    const label = `Route: ${name}`;
    const labelWidth = Math.max(40, label.length * 8 + 18);
    const svg = `<svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" xmlns="http://www.w3.org/2000/svg">
<rect x="10" y="10" rx="4" ry="4" width="${labelWidth}" height="24" fill="#ff4d4f" opacity="0.9"/>
<text x="18" y="27" font-size="12" font-family="Arial" fill="#ffffff">${label}</text>
</svg>`;
    const svgBuf = Buffer.from(svg);
    await sharpMod(filePath).composite([{ input: svgBuf, top: 0, left: 0 }]).toFile(tmpOutPath);
    if (overwrite) {
      try { fs.renameSync(tmpOutPath, filePath); } catch { fs.copyFileSync(tmpOutPath, filePath); fs.unlinkSync(tmpOutPath); }
      console.log(`[snap] annotated (label) ${filePath}`);
    } else {
      console.log(`[snap] annotated (label) ${outPath}`);
    }
    return;
  }
  const meta = await sharpMod(filePath).metadata();
  const w = meta.width || 1200;
  const h = meta.height || 800;
  // Build callouts: rectangle on target + numbered label in margin with arrow line
  const marginLeft = 10;
  const marginTop = 48; // leave space for route label if present
  const lineHeight = 24;
  const gap = 8;
  const svgRects = entries.map((e, i) => {
    const x = Math.max(0, Math.floor(e.x));
    const y = Math.max(0, Math.floor(e.y));
    const rw = Math.max(1, Math.floor(e.width));
    const rh = Math.max(1, Math.floor(e.height));
    const n = i + 1;
    const labelText = `${n}. ${e.label}`;
    const labelWidth = Math.max(80, e.label.length * 8 + 28);
    const ly = marginTop + i * (lineHeight + gap);
    const lrect = `<rect x="${marginLeft}" y="${ly}" rx="4" ry="4" width="${labelWidth}" height="${lineHeight}" fill="#ff4d4f" opacity="0.95"/>`;
    const ltext = `<text x="${marginLeft + 8}" y="${ly + 16}" font-size="12" font-family="Arial" fill="#ffffff">${labelText}</text>`;
    const tx = x + rw / 2;
    const ty = y + rh / 2;
    const lx = marginLeft + labelWidth;
    const lyc = ly + lineHeight / 2;
    const arrow = `<defs><marker id="arrow" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto" markerUnits="strokeWidth"><path d="M0,0 L0,6 L6,3 z" fill="#ff4d4f"/></marker></defs>`;
    const line = `<line x1="${lx + 6}" y1="${lyc}" x2="${tx}" y2="${ty}" stroke="#ff4d4f" stroke-width="2.5" marker-end="url(#arrow)"/>`;
    const target = `<rect x="${x}" y="${y}" width="${rw}" height="${rh}" fill="none" stroke="#ff4d4f" stroke-width="4"/>`;
    return `<g>${arrow}${lrect}${ltext}${line}${target}</g>`;
  }).join('');
  const svg = `<svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" xmlns="http://www.w3.org/2000/svg">${svgRects}</svg>`;
  const svgBuf = Buffer.from(svg);
  await sharpMod(filePath).composite([{ input: svgBuf, top: 0, left: 0 }]).toFile(tmpOutPath);
  if (overwrite) {
    try {
      fs.renameSync(tmpOutPath, filePath);
    } catch (e) {
      // Fallback: copy and unlink
      fs.copyFileSync(tmpOutPath, filePath);
      fs.unlinkSync(tmpOutPath);
    }
    console.log(`[snap] annotated (overwrote) ${filePath}`);
  } else {
    console.log(`[snap] annotated ${outPath}`);
  }
}

async function screenshot(page, name: string) {
  const dir = ensureScreenshotDir();
  const filePath = path.join(dir, `${name}.png`);
  await page.screenshot({ path: filePath, fullPage: true });
  console.log(`[snap] saved ${filePath}`);
  await annotateIfEnabled(page, filePath, name);
}

// Basic flow: login and capture key screens that are reachable via common paths
// You can extend these to your actual routes once confirmed.

test('login and capture basic screens', async ({ page }) => {
  test.setTimeout(1500000);
  await test.step('Open base URL', async () => {
    await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });
  });

  await test.step('Login if required', async () => {
    await tryLogin(page);
  });

  await test.step('Capture dashboard/home', async () => {
    await screenshot(page, '01_dashboard_or_home');
  });
  await screenshot(page, '01_dashboard_or_home');

  // Use only confirmed valid routes (user-provided)
  const candidates = [
    '/dashboard',
    '/conversations/active',
    '/conversations/assignments',
    '/conversations/transfers',
    '/conversations/history',
    '/guests/bookings',
    '/guests/checkins',
    '/guests/history',
    '/guests/interaction/173',
    '/tasks/all',
    '/tasks/my',
    '/tasks/housekeeping',
    '/tasks/maintenance',
    '/tasks/frontdesk',
    '/broadcast/compose',
    '/broadcast/emergency',
    '/broadcast/templates',
    '/broadcast/history',
    '/lost-and-found',
    '/configuration/hotel-info',
    '/configuration/services',
    '/configuration/faqs',
    '/configuration/menu',
    '/configuration/emergency',
    '/configuration/templates',
    '/business-rules/dashboard',
    '/business-rules/services',
    '/business-rules/upselling',
    '/business-rules/audit-log',
    '/users/staff',
    '/users/agents',
    '/users/roles',
    '/users/activity',
    '/reports/analytics',
    '/reports/tasks',
    '/reports/satisfaction',
    '/reports/usage',
    '/analytics/business-impact'
  ];

  async function tryAddEditView(page, baseRoute: string, key: string) {
    // DISABLED: Form interactions causing hangs
    return;
    // Try common add/new buttons
    const addSelectors = [
      'button:has-text("Add")',
      'button:has-text("New")',
      'button:has-text("Create")',
      'a:has-text("Add")',
      'a:has-text("New")',
      '[data-testid="add"]',
      '[aria-label="Add"]'
    ];
    for (const sel of addSelectors) {
      const el = page.locator(sel).first();
      if (await el.count()) {
        await test.step(`Open add form via ${sel}`, async () => {
          await Promise.all([
            page.waitForLoadState('domcontentloaded').catch(() => {}),
            el.click()
          ]);
          await page.waitForTimeout(500);
          await screenshot(page, `add_${key}`);
        });
        await page.goto(new URL(baseRoute, BASE_URL).toString(), { waitUntil: 'domcontentloaded' });
        break;
      }
    }

    // Try edit of first row only if table has real data cells
    const table = page.locator('table').first();
    const dataCells = table.locator('tbody tr td');
    if (await dataCells.count()) {
      const firstRow = table.locator('tbody tr').first();
      const editCandidates = [
        'a:has-text("Edit")',
        'button:has-text("Edit")',
        '[data-testid="edit"]',
        'a[href*="/edit"], a[href*="/update"]'
      ];
      let opened = false;
      for (const sel of editCandidates) {
        const el = firstRow.locator(sel).first();
        if (await el.count() && await el.isVisible().catch(() => false) && await el.isEnabled().catch(() => false)) {
          await test.step(`Open edit form via ${sel}`, async () => {
            const clickPromise = el.click({ trial: false }).catch(() => {});
            await Promise.race([
              clickPromise,
              page.waitForTimeout(2000)
            ]);
            await page.waitForTimeout(300);
            await screenshot(page, `edit_${key}`);
          });
          opened = true;
          break;
        }
      }
      if (!opened) {
        // Try clicking the row itself
        await test.step('Open edit by clicking first row', async () => {
          await Promise.race([
            firstRow.click().catch(() => {}),
            page.waitForTimeout(2000)
          ]);
          await page.waitForTimeout(300);
          await screenshot(page, `edit_${key}`);
        });
      }

      // Try view/details of first row
      const viewCandidates = [
        'a:has-text("View")',
        'button:has-text("View")',
        '[data-testid="view"]',
        'a[href*="/view"], a[href*="/details"], a[href*="/show"]'
      ];
      let viewed = false;
      for (const sel of viewCandidates) {
        const el = page.locator(sel).first();
        if (await el.count()) {
          await test.step(`Open view via ${sel}`, async () => {
            await Promise.all([
              page.waitForLoadState('domcontentloaded').catch(() => {}),
              el.click()
            ]);
            await page.waitForTimeout(500);
            await screenshot(page, `view_${key}`);
          });
          viewed = true;
          break;
        }
      }
      if (!viewed) {
        // Try opening a possible preview pane/modal by clicking row and looking for dialog
        const dialog = page.locator('dialog[open], .cdk-overlay-pane:has(.mat-dialog-container), [role="dialog"], .modal.open, .ant-modal-content');
        if (await dialog.count()) {
          await screenshot(page, `view_${key}`);
        }
      }
    }
  }

  for (const route of candidates) {
    await test.step(`Capture route ${route}`, async () => {
      try {
        await page.goto(new URL(route, BASE_URL).toString(), { waitUntil: 'domcontentloaded' });
        await page.waitForLoadState('networkidle', { timeout: 5000 }).catch(() => {});
        await screenshot(page, `route_${route.replace(/\//g, '_').replace(/^_/, '')}`);
        const TRY_FORMS = !(process.env.SNAP_TRY_FORMS === '0' || process.env.SNAP_TRY_FORMS === 'false');
        if (TRY_FORMS) {
          const key = route.replace(/\//g, '_').replace(/^_/, '');
          await tryAddEditView(page, route, key);
        }
      } catch (e) {
        // Ignore missing routes
      }
    });
  }
});
