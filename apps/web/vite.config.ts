import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

/**
 * The dev server proxies /api and /healthz to the ASP.NET Core API on 8787, so
 * the browser only ever talks to one origin. That keeps cookie-based sessions
 * (Phase 4) working in development exactly as they will behind Caddy in
 * production.
 */
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: 'http://localhost:8787', changeOrigin: true },
      '/healthz': { target: 'http://localhost:8787', changeOrigin: true },
    },
  },
});
