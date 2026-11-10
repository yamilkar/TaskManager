import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from './auth-api';
import { LoginRequest, RegisterRequest, User } from './auth.models';

const STORAGE_KEY = 'tm.session';

interface StoredSession {
  token: string;
  expiresAt: string;
  user: User;
}

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly api = inject(AuthApi);

  private readonly userState = signal<User | null>(null);
  private readonly tokenState = signal<string | null>(null);

  readonly user = this.userState.asReadonly();
  readonly token = this.tokenState.asReadonly();
  readonly isAuthenticated = computed(() => this.tokenState() !== null && this.userState() !== null);

  async login(req: LoginRequest): Promise<void> {
    const res = await firstValueFrom(this.api.login(req));
    this.setSession({ token: res.accessToken, expiresAt: res.expiresAt, user: res.user });
  }

  async register(req: RegisterRequest): Promise<void> {
    await firstValueFrom(this.api.register(req));
    await this.login({ email: req.email, password: req.password });
  }

  logout(): void {
    this.clearSession();
  }

  async restoreSession(): Promise<void> {
    const stored = this.readStoredSession();
    if (!stored) {
      return;
    }
    if (Date.parse(stored.expiresAt) <= Date.now()) {
      this.clearSession();
      return;
    }

    this.tokenState.set(stored.token);
    this.userState.set(stored.user);
    try {
      const user = await firstValueFrom(this.api.me());
      this.setSession({ ...stored, user });
    } catch {
      this.clearSession();
    }
  }

  private setSession(session: StoredSession): void {
    this.tokenState.set(session.token);
    this.userState.set(session.user);
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Storage unavailable: keep the session in memory only.
    }
  }

  private clearSession(): void {
    this.tokenState.set(null);
    this.userState.set(null);
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Nothing to clear.
    }
  }

  private readStoredSession(): StoredSession | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return null;
      }
      const parsed: Partial<StoredSession> | null = JSON.parse(raw);
      if (
        parsed &&
        typeof parsed.token === 'string' &&
        typeof parsed.expiresAt === 'string' &&
        parsed.user &&
        typeof parsed.user === 'object'
      ) {
        return { token: parsed.token, expiresAt: parsed.expiresAt, user: parsed.user };
      }
    } catch {
      // Corrupt or inaccessible storage: treat as no session.
    }
    this.clearSession();
    return null;
  }
}
