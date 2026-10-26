import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

// A bootstrap failure is rethrown, so it reaches the global error handler instead of the console (CLAUDE.md).
bootstrapApplication(App, appConfig).catch((err: unknown) => {
  throw err;
});
