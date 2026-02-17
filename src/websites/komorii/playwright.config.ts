import { defineConfig, devices } from '@playwright/test';

const komoriiUrl = process.env.KOMORII_BASE_URL ?? 'http://localhost:5173';

export default defineConfig({
    testDir: './tests/e2e',
    fullyParallel: true,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 2 : 0,
    workers: process.env.CI ? 1 : undefined,
    reporter: [['html', { open: 'never' }], ['list']],
    use: {
        trace: 'on-first-retry',
    },
    projects: [
        {
            // Fast mocked tests — API intercepted, no backend required.
            // Run: npm run test:e2e
            name: 'mocked',
            testMatch: '**/reader.spec.ts',
            use: {
                ...devices['Desktop Chrome'],
                baseURL: 'http://localhost:5173',
            },
        },
        {
            // Live tests — hit a real running GatewayApi + peer node.
            // Run: KOMORII_BASE_URL=http://localhost:5173 npm run test:e2e:live
            // Set SKIP_LIVE_TESTS=1 to skip without error.
            name: 'live',
            testMatch: '**/reader.live.spec.ts',
            use: {
                ...devices['Desktop Chrome'],
                baseURL: komoriiUrl,
                // Longer default timeout for P2P latency
                actionTimeout: 30_000,
                navigationTimeout: 30_000,
            },
        },
    ],
    webServer: {
        command: 'npm run dev',
        url: 'http://localhost:5173',
        // Reuse an already-running dev server (required for live tests where user starts the full stack)
        reuseExistingServer: true,
        timeout: 30_000,
    },
});
