/**
 * Playwright configuration for LIVE end-to-end tests.
 *
 * Unlike playwright.config.ts (mocked), this config automatically spins up
 * the GatewayApi backend before running tests — no manual setup required.
 *
 * Run: npm run test:e2e:live
 *
 * What gets started automatically:
 *   1. GatewayApi  — dotnet run, listens on https://localhost:7030 (Vite proxy target)
 *                    + http://localhost:5170 (Playwright health check)
 *   2. Komorii     — npm run dev, listens on http://localhost:5173
 *
 * If either server is already running, it is reused (reuseExistingServer: true).
 *
 * Optional env vars:
 *   KOMORII_BASE_URL   Override the Komorii frontend URL  (default: http://localhost:5173)
 *   SKIP_LIVE_TESTS=1  Skip all live tests without failure
 *   LIVE_READER_URL    A specific reader URL for the direct-navigation test
 */

import { defineConfig, devices } from '@playwright/test';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));

// Absolute path to the GatewayApi .csproj, resolved from this config file's location.
// Layout: src/websites/komorii/  →  src/MangaMesh.Peer/MangaMesh.Peer.GatewayApi/
const GATEWAY_CSPROJ = resolve(
    __dirname,
    '../../MangaMesh.Peer/MangaMesh.Peer.GatewayApi/MangaMesh.Peer.GatewayApi.csproj'
);

export default defineConfig({
    testDir: './tests/e2e',
    testMatch: '**/reader.live.spec.ts',
    // Run live tests sequentially — they depend on shared backend state
    fullyParallel: false,
    workers: 1,
    retries: 1,
    reporter: [['html', { open: 'never' }], ['list']],
    use: {
        ...devices['Desktop Chrome'],
        baseURL: process.env.KOMORII_BASE_URL ?? 'http://localhost:5173',
        // P2P fetches can be slow — give each action extra headroom
        actionTimeout: 30_000,
        navigationTimeout: 30_000,
    },

    webServer: [
        {
            // ── GatewayApi ──────────────────────────────────────────────────
            // Started with --no-launch-profile so launchSettings.json is ignored;
            // ASPNETCORE_URLS drives the ports directly:
            //   - https://localhost:7030  →  matches the Vite proxy in vite.config.ts
            //   - http://localhost:5170   →  plain-HTTP health check for Playwright
            //     (avoids TLS validation issues with the self-signed dev cert)
            //
            // First run will trigger a dotnet restore + build — allow up to 2 minutes.
            command: `dotnet run --project "${GATEWAY_CSPROJ}" --no-launch-profile`,
            url: 'http://localhost:5170/swagger/index.html',
            reuseExistingServer: true,
            timeout: 120_000,
            env: {
                ASPNETCORE_ENVIRONMENT: 'Development',
                ASPNETCORE_URLS: 'https://localhost:7030;http://localhost:5170',
            },
        },
        {
            // ── Komorii frontend ─────────────────────────────────────────────
            // Vite dev server — the same one used for mocked tests.
            // Its /api proxy will forward to the GatewayApi started above.
            command: 'npm run dev',
            url: 'http://localhost:5173',
            reuseExistingServer: true,
            timeout: 30_000,
        },
    ],
});
