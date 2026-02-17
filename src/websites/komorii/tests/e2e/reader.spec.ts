/**
 * Mocked E2E tests for the Komorii reader user flow.
 *
 * All API calls are intercepted — no backend required.
 * Run: npm run test:e2e
 */

import { test, expect, type Page } from '@playwright/test';
import { seriesList, seriesDetails, seriesChapters, chapterDetails, chapterManifest } from './fixtures/index';

// 1x1 transparent PNG for mocking image responses
const TRANSPARENT_PNG = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
    'base64'
);

const SERIES_ID = 'test-series-001';
const CHAPTER_ID = 'ch-001';
const MANIFEST_HASH = 'aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899';

/**
 * Dispatches all /api/** requests to the appropriate fixture.
 * Registered as a single handler to avoid route priority ordering issues.
 */
async function setupApiMocks(page: Page) {
    await page.route('**/api/**', async route => {
        const url = new URL(route.request().url());
        const path = url.pathname;

        // Order: most specific first
        if (/\/api\/Series\/[^/]+\/chapter\/[^/]+\/manifest\/[^/]+\/read/.test(path)) {
            await route.fulfill({ json: chapterManifest });
        } else if (/\/api\/Series\/[^/]+\/chapters\/[^/]+/.test(path)) {
            await route.fulfill({ json: chapterDetails });
        } else if (/\/api\/Series\/[^/]+\/chapters/.test(path)) {
            await route.fulfill({ json: seriesChapters });
        } else if (/\/api\/Series\/[^/?]+$/.test(path)) {
            await route.fulfill({ json: seriesDetails });
        } else if (/\/api\/Series/.test(path)) {
            await route.fulfill({ json: seriesList });
        } else {
            await route.continue();
        }
    });

    // Mock cover images (proxied through Vite to the gateway)
    await page.route('**/covers/**', route =>
        route.fulfill({ body: TRANSPARENT_PNG, contentType: 'image/webp' })
    );
}

/**
 * Mock blob image requests served from the peer node (cross-origin).
 * The Reader fetches: http://localhost:9000/api/blob/<hash>
 */
async function setupBlobMocks(page: Page) {
    await page.route('http://localhost:9000/**', route =>
        route.fulfill({ body: TRANSPARENT_PNG, contentType: 'image/jpeg' })
    );
}

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Series listing', () => {
    test('shows popular and recent series on the homepage', async ({ page }) => {
        await setupApiMocks(page);

        await page.goto('/');

        // Both popular and recent sections should show the mocked series title
        await expect(page.locator('h3', { hasText: 'Test Manga' }).first()).toBeVisible();
        await expect(page.locator('text=Popular Series')).toBeVisible();
        await expect(page.locator('text=Recently Updated')).toBeVisible();
    });

    test('search returns and displays matching series', async ({ page }) => {
        await setupApiMocks(page);

        await page.goto('/');
        await page.getByPlaceholder('Search series...').fill('test');

        // Results section should appear with the mocked series
        await expect(page.locator('text=Search Results')).toBeVisible();
        await expect(page.locator(`a[href="/series/${SERIES_ID}"]`).first()).toBeVisible();
    });
});

test.describe('Series details', () => {
    test('shows series title and chapter list', async ({ page }) => {
        await setupApiMocks(page);

        await page.goto(`/series/${SERIES_ID}`);

        // Series heading
        await expect(page.locator('h1', { hasText: 'Test Manga' })).toBeVisible();

        // Chapter row header
        await expect(page.locator('text=Chapter 1')).toBeVisible();
        await expect(page.locator('text=The Beginning').first()).toBeVisible();
    });

    test('chapter rows show language and quality badges', async ({ page }) => {
        await setupApiMocks(page);

        await page.goto(`/series/${SERIES_ID}`);

        // Language badge
        await expect(page.locator('span', { hasText: 'en' }).first()).toBeVisible();

        // Quality badge
        await expect(page.locator('span', { hasText: 'HQ' }).first()).toBeVisible();

        // Scan group name
        await expect(page.locator('text=TestScans').first()).toBeVisible();
    });

    test('clicking a manifest link navigates to the reader', async ({ page }) => {
        await setupApiMocks(page);

        await page.goto(`/series/${SERIES_ID}`);

        // Wait for manifests to load, then click the first manifest row
        const manifestLink = page.locator(`a[href*="/read/${CHAPTER_ID}"]`).first();
        await expect(manifestLink).toBeVisible();
        await manifestLink.click();

        // URL should contain the reader path and manifest query param
        await expect(page).toHaveURL(new RegExp(`/series/${SERIES_ID}/read/${CHAPTER_ID}`));
        await expect(page).toHaveURL(/[?&]manifest=/);
    });
});

test.describe('Reader', () => {
    const readerUrl = `/series/${SERIES_ID}/read/${CHAPTER_ID}?manifest=${MANIFEST_HASH}`;

    test('renders chapter heading and page images', async ({ page }) => {
        await setupApiMocks(page);
        await setupBlobMocks(page);

        await page.goto(readerUrl);

        // Chapter heading in the sticky header
        await expect(page.locator('h1', { hasText: 'Chapter 1' })).toBeVisible();

        // Two page images should be rendered (one per file in the fixture)
        const images = page.locator('img[alt^="Page"]');
        await expect(images).toHaveCount(2);
    });

    test('shows error when API returns no nodes', async ({ page }) => {
        await setupApiMocks(page);

        // Override the read-chapter endpoint to return empty nodes
        await page.route('**/api/Series/**/manifest/**/read', route =>
            route.fulfill({ json: { ...chapterManifest, nodes: [] } })
        );

        await page.goto(readerUrl);

        await expect(page.locator('text=No available nodes found')).toBeVisible();
    });

    test('shows error when read-chapter API returns 500', async ({ page }) => {
        await setupApiMocks(page);

        // Override the read-chapter endpoint to simulate a server error
        await page.route('**/api/Series/**/manifest/**/read', route =>
            route.fulfill({ status: 500, body: 'Internal Server Error' })
        );

        await page.goto(readerUrl);

        await expect(page.locator('text=Failed to load chapter content')).toBeVisible();
    });

    test('returns to series page via the "Return to Series" link', async ({ page }) => {
        await setupApiMocks(page);
        await setupBlobMocks(page);

        await page.goto(readerUrl);
        await expect(page.locator('h1', { hasText: 'Chapter 1' })).toBeVisible();

        await page.locator('a', { hasText: 'Return to Series' }).click();

        await expect(page).toHaveURL(new RegExp(`/series/${SERIES_ID}$`));
    });
});

test.describe('Navigation flow', () => {
    test('full path: homepage → series details → reader', async ({ page }) => {
        await setupApiMocks(page);
        await setupBlobMocks(page);

        // 1. Start at homepage
        await page.goto('/');
        await expect(page.locator('h3', { hasText: 'Test Manga' }).first()).toBeVisible();

        // 2. Click through to series details (popular card or recent list card)
        await page.locator(`a[href="/series/${SERIES_ID}"]`).first().click();
        await expect(page).toHaveURL(`/series/${SERIES_ID}`);
        await expect(page.locator('h1', { hasText: 'Test Manga' })).toBeVisible();

        // 3. Click a chapter manifest to open the reader
        const manifestLink = page.locator(`a[href*="/read/${CHAPTER_ID}"]`).first();
        await expect(manifestLink).toBeVisible();
        await manifestLink.click();

        // 4. Reader loads the chapter
        await expect(page).toHaveURL(new RegExp(`/series/${SERIES_ID}/read/${CHAPTER_ID}`));
        await expect(page.locator('h1', { hasText: 'Chapter 1' })).toBeVisible();
    });
});
