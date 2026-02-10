# MangaMesh UI

## Running the UI

1. Install dependencies:
   ```bash
   npm install
   ```

2. Start the development server:
   ```bash
   npm run dev
   ```

3. Open http://localhost:5173

## API Proxy
The UI expects the backend to be running on the same origin (port 5173) or via proxy.
To configure a proxy to the backend (e.g., if backend runs on different port), edit `vite.config.ts`.
