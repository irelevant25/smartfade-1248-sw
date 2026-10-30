import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { ConsoleConnectionService } from './core/console-connection.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Opens the link to the backend. It does not wait for it: the page renders at once and shows
    // "disconnected" until the backend answers.
    provideAppInitializer(() => {
      inject(ConsoleConnectionService).start();
    }),
  ],
};
