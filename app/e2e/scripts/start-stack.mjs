// Starts the end-to-end test stack and waits until it is ready.
//
//   1. PostgreSQL in its own compose project ("trip-e2e"), with its own volume, database and port.
//   2. The Angular app, built for production.
//   3. The API on the test database (migrations run on start), serving the built app from the same origin.
//
// It never reads app/.env, and it refuses the developer's dev database name and port.
// Run it on its own (npm run stack, from app/e2e), or let Playwright start it through playwright.config.ts.
//
// Environment variables:
//   E2E_POSTGRES_USER      required  test database user (no default)
//   E2E_POSTGRES_PASSWORD  required  test database password (no default; avoid ';' in it)
//   E2E_POSTGRES_DB        optional  default trip_tracker_e2e
//   E2E_POSTGRES_PORT      optional  default 5433 (must not be 5432, the usual dev port)
//   E2E_APP_PORT           optional  default 5100 (API and web app)

import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const appDir = path.resolve(here, '..', '..');
const composeFile = path.join(appDir, 'docker-compose.yml');
const apiProject = path.join(appDir, 'src', 'api', 'TripExpenseTracker.Api');
const webDir = path.join(appDir, 'src', 'web');
const webDist = path.join(webDir, 'dist', 'trip-expense-tracker-web', 'browser');
const logDir = path.join(here, '..', 'test-results', 'stack');
const composeProject = 'trip-e2e';
const devDatabaseName = 'trip_tracker_dev';
const devPort = '5432';

const log = (message) => process.stdout.write(`[e2e stack] ${message}\n`);
const fail = (message) => {
  process.stderr.write(`[e2e stack] ${message}\n`);
  process.exit(1);
};

// Required values come only from the environment. Nothing is written to disk.
const user = process.env.E2E_POSTGRES_USER?.trim();
const password = process.env.E2E_POSTGRES_PASSWORD;
if (!user || !password) {
  fail('Set E2E_POSTGRES_USER and E2E_POSTGRES_PASSWORD. See app/e2e/README.md.');
}

const database = process.env.E2E_POSTGRES_DB?.trim() || 'trip_tracker_e2e';
const dbPort = process.env.E2E_POSTGRES_PORT?.trim() || '5433';
const appPort = process.env.E2E_APP_PORT?.trim() || '5100';

if (database === devDatabaseName || dbPort === devPort) {
  fail(`Refusing to run: E2E_POSTGRES_DB/E2E_POSTGRES_PORT point at the dev database. Use a separate database and port.`);
}
if (!fs.existsSync(path.join(webDir, 'node_modules'))) {
  fail('Run "npm ci" in app/src/web first.');
}

fs.mkdirSync(logDir, { recursive: true });

// 1. Test PostgreSQL. The compose variables come from this process only, so app/.env is not used for them.
log(`starting PostgreSQL (compose project ${composeProject}, database ${database}, port ${dbPort})`);
const compose = spawnSync(
  'docker',
  ['compose', '-p', composeProject, '-f', composeFile, 'up', '-d', '--wait', 'db'],
  {
    stdio: 'inherit',
    env: {
      ...process.env,
      POSTGRES_DB: database,
      POSTGRES_USER: user,
      POSTGRES_PASSWORD: password,
      POSTGRES_PORT: dbPort,
    },
  },
);
if (compose.status !== 0) {
  fail('docker compose failed. Is Docker running?');
}

// 2. Angular production build, served by the API.
log('building the web app');
// One fixed command string, no user input. A string avoids the Node warning about args with shell: true.
const build = spawnSync('npm run build', { cwd: webDir, shell: true, stdio: 'inherit' });
if (build.status !== 0 || !fs.existsSync(path.join(webDist, 'index.html'))) {
  fail('web build failed.');
}

// 3. API on the test database. Migrations run on start because Database:MigrateOnStartup is true here.
//    ASPNETCORE_ENVIRONMENT=E2E keeps the Development user secrets and appsettings.Development.json out of the run.
const connectionString = `Host=localhost;Port=${dbPort};Database=${database};Username=${user};Password=${password}`;
const apiLog = fs.openSync(path.join(logDir, 'api.log'), 'w');
log(`starting the API on http://localhost:${appPort} (output in app/e2e/test-results/stack/api.log)`);
const api = spawn('dotnet', ['run', '--project', apiProject, '--no-launch-profile'], {
  stdio: ['ignore', apiLog, apiLog],
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'E2E',
    ASPNETCORE_URLS: `http://localhost:${appPort}`,
    ASPNETCORE_WEBROOT: webDist,
    ConnectionStrings__Default: connectionString,
    Database__MigrateOnStartup: 'true',
  },
});

let apiExited = false;
let apiExitCode = null;
api.on('exit', (code) => {
  apiExited = true;
  apiExitCode = code;
});

// Kills the API and its children. "dotnet run" starts the app as a child process, so on Windows the whole tree is stopped.
function stopApi() {
  if (apiExited || api.pid === undefined) return;
  if (process.platform === 'win32') {
    spawnSync('taskkill', ['/pid', String(api.pid), '/T', '/F'], { stdio: 'ignore' });
  } else {
    api.kill('SIGTERM');
  }
}
process.on('SIGINT', () => { stopApi(); process.exit(130); });
process.on('SIGTERM', () => { stopApi(); process.exit(143); });
process.on('exit', stopApi);

// 4. Wait for the health check and the web app. Fail fast if the API exits (for example, a migration error).
const healthUrl = `http://localhost:${appPort}/api/health`;
const homeUrl = `http://localhost:${appPort}/`;
const deadline = Date.now() + 120_000;
let ready = false;
while (Date.now() < deadline) {
  if (apiExited) {
    fail(`the API exited with code ${apiExitCode}. See app/e2e/test-results/stack/api.log.`);
  }
  try {
    const health = await fetch(healthUrl);
    const home = await fetch(homeUrl);
    if (health.ok && home.ok) {
      ready = true;
      break;
    }
  } catch {
    // Not listening yet. Try again.
  }
  await new Promise((resolve) => setTimeout(resolve, 1000));
}
if (!ready) {
  stopApi();
  fail('the stack did not become ready within 120 seconds. See app/e2e/test-results/stack/api.log.');
}

log(`ready: ${homeUrl}`);
// Keep running until the API stops (Playwright stops it at the end of a run, or Ctrl+C here).
await new Promise((resolve) => api.on('exit', resolve));
