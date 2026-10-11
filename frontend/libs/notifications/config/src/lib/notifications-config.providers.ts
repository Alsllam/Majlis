import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';

/** No sidebar entry: the notification center lives in the top bar (the bell) and at `/notifications`. */
export function provideNotificationsConfig(): EnvironmentProviders {
  return makeEnvironmentProviders([]);
}
