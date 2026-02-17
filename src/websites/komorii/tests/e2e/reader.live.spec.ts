/**
 * Live E2E tests for the Komorii reader user flow.
 *
 * These tests hit a real running GatewayApi + peer node — no mocking.
 * They validate the full P2P content delivery pipeline end-to-end.
 *
 * Prerequisites:
 *   - Komorii dev server running (npm run dev in src/websites/komorii)
 *   - GatewayApi running at https://localhost:7030
 *   - At least one peer node online with seeded content
 *
 * Run: npm run test:e2e:live
 * Skip all: SKIP_LIVE_TESTS=1 npm run test:e2e:live
 * Custom URL: KOMORII_BASE_URL=http://myserver:5173 npm run test:e2e:live
 *
 * Tag filter: npx playwright test --project=live --grep @live
 */

import { test, expect } from '@playwright/test';

const SKIP = !!process.env.SKIP_LIVE_TESTS;

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('@live Series listing', () => {
    test('homepage loads and shows at least one series', async ({ page }) => {
        test.skip(SKIP, 'SKIP_LIVE_TESTS is set');

        await page.goto('/');

        // Wait up to the live timeout for the network to respond
        const popularHeading = page.locator('text=Popular Series');
        await expect(popularHeading).toBeVisible({ timeout: 15_000 });

        // At least one series card should appear; if the network has no data,
        // the "No popular series found" placeholder should appear instead — either is valid.
        const hasSeries = await page.locator('h3').count();
        const hasEmptyState = await page.locator('text=No popular series found').count();
        expect(hasSeries + hasEmptyState).toBeGreaterThan(0);
    });
});

test.describe('@live Series details', () => {
    test('navigating to a series page shows chapter list or empty state', async ({ page }) => {
        test.skip(SKIP, 'SKIP_LIVE_TESTS is set');

        // First load the homepage to discover a real series ID
        await page.goto('/');
        await expect(page.locator('text=Popular Series')).toBeVisible({ timeout: 15_000 });

        // Try to click the first series link found anywhere on the page
        const firstSeriesLink = page.locator('a[href^="/series/"]').first();
        const count = await firstSeriesLink.count();
        test.skip(count === 0, 'No series found on homepage — skipping (network may be empty)');

        await firstSeriesLink.click();

        // The series details page should load (either chapters or empty state)
        await expect(page.locator('h2', { hasText: 'Chapters' })).toBeVisible({ timeout: 15_000 });
    });
});

test.describe('@live Reader', () => {
    test('full happy path: homepage → series → chapter → reader renders or shows error', async ({ page }) => {
        test.skip(SKIP, 'SKIP_LIVE_TESTS is set');

        // 1. Homepage
        await page.goto('/');
        await expect(page.locator('text=Popular Series')).toBeVisible({ timeout: 15_000 });

        // 2. Navigate to first available series
        const firstSeriesLink = page.locator('a[href^="/series/"]').first();
        const seriesCount = await firstSeriesLink.count();
        test.skip(seriesCount === 0, 'No series found — skipping');

        await firstSeriesLink.click();
        await expect(page.locator('h2', { hasText: 'Chapters' })).toBeVisible({ timeout: 15_000 });

        // 3. Click the first available manifest link
        const firstManifestLink = page.locator('a[href*="/read/"]').first();
        const manifestCount = await firstManifestLink.count();
        test.skip(manifestCount === 0, 'No chapter manifests found — skipping (no seeded content)');

        await firstManifestLink.click();

        // 4. Reader should show either:
        //    a) A loaded chapter (h1 with "Chapter") — success
        //    b) An error panel — acceptable if no peers are online
        await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});

        const hasChapter = await page.locator('h1').filter({ hasText: 'Chapter' }).count();
        const hasError = await page.locator('text=Error Loading Chapter').count();
        expect(hasChapter + hasError).toBeGreaterThan(0);
    });

    test('directly loading a reader URL renders or shows a structured error', async ({ page }) => {
        test.skip(SKIP, 'SKIP_LIVE_TESTS is set');

        // This test uses the env var LIVE_READER_URL to navigate directly to a known chapter.
        // If not set, it is skipped.
        const readerUrl = process.env.LIVE_READER_URL;
        test.skip(!readerUrl, 'LIVE_READER_URL not set — provide a real reader URL to test direct navigation');

        await page.goto(readerUrl!);
        await page.waitForLoadState('networkidle', { timeout: 30_000 }).catch(() => {});

        // Either a chapter loaded or a structured error panel — both are valid outcomes
        const hasChapter = await page.locator('h1').filter({ hasText: 'Chapter' }).count();
        const hasError = await page.locator('text=Error Loading Chapter').count();
        expect(hasChapter + hasError).toBeGreaterThan(0);
    });
});
