import { InjectionToken } from '@angular/core';

export interface AppConfig {
  usersApiUrl: string;
  tasksApiUrl: string;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');

export async function loadAppConfig(): Promise<AppConfig> {
  const response = await fetch('/config.json');
  if (!response.ok) {
    throw new Error(`Failed to load /config.json (${response.status})`);
  }
  return (await response.json()) as AppConfig;
}
