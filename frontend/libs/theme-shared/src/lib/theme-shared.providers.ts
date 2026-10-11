import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { HttpErrorHandler } from './errors/http-error-handler';

export function provideThemeShared(): EnvironmentProviders {
  return makeEnvironmentProviders([provideAppInitializer(() => inject(HttpErrorHandler).start())]);
}
