import { Observable, of, throwError } from 'rxjs';
import { TestBed } from '@angular/core/testing';
import { AppError } from '../http/app-error';
import { AuthApi } from './auth-api';
import { AuthResponse, User } from './auth.models';
import { AuthStore } from './auth-store';

const KEY = 'tm.session';
const user: User = { id: '1', fullName: 'Ana', email: 'a@x.com', createdAt: '2026-01-01T00:00:00Z' };
const future = () => new Date(Date.now() + 3_600_000).toISOString();
const past = () => new Date(Date.now() - 3_600_000).toISOString();

describe('AuthStore', () => {
  let api: {
    login: ReturnType<typeof vi.fn<(r: unknown) => Observable<AuthResponse>>>;
    register: ReturnType<typeof vi.fn<(r: unknown) => Observable<User>>>;
    me: ReturnType<typeof vi.fn<() => Observable<User>>>;
  };

  function createStore(): AuthStore {
    TestBed.configureTestingModule({ providers: [{ provide: AuthApi, useValue: api }] });
    return TestBed.inject(AuthStore);
  }

  beforeEach(() => {
    sessionStorage.clear();
    api = { login: vi.fn(), register: vi.fn(), me: vi.fn() };
  });

  it('AuthStore.login stores session in sessionStorage and sets user', async () => {
    const expiresAt = future();
    api.login.mockReturnValue(of({ accessToken: 'tok', expiresAt, user }));
    const store = createStore();

    await store.login({ email: 'a@x.com', password: 'pw' });

    expect(store.user()).toEqual(user);
    expect(store.token()).toBe('tok');
    expect(store.isAuthenticated()).toBe(true);
    expect(JSON.parse(sessionStorage.getItem(KEY)!)).toEqual({ token: 'tok', expiresAt, user });
  });

  it('login rejects with the AppError from the API and leaves the store unauthenticated', async () => {
    const appError: AppError = { status: 401, message: 'Invalid email or password.', fieldErrors: {} };
    api.login.mockReturnValue(throwError(() => appError));
    const store = createStore();

    await expect(store.login({ email: 'a@x.com', password: 'bad' })).rejects.toBe(appError);
    expect(store.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem(KEY)).toBeNull();
  });

  it('register registers then logs in with the same credentials', async () => {
    api.register.mockReturnValue(of(user));
    api.login.mockReturnValue(of({ accessToken: 'tok', expiresAt: future(), user }));
    const store = createStore();

    await store.register({ fullName: 'Ana', email: 'a@x.com', password: 'pw' });

    expect(api.login).toHaveBeenCalledWith({ email: 'a@x.com', password: 'pw' });
    expect(store.isAuthenticated()).toBe(true);
  });

  it('logout clears the session', async () => {
    api.login.mockReturnValue(of({ accessToken: 'tok', expiresAt: future(), user }));
    const store = createStore();
    await store.login({ email: 'a@x.com', password: 'pw' });

    store.logout();

    expect(store.user()).toBeNull();
    expect(store.token()).toBeNull();
    expect(sessionStorage.getItem(KEY)).toBeNull();
  });

  it('AuthStore.restoreSession clears an expired session without calling /me', async () => {
    sessionStorage.setItem(KEY, JSON.stringify({ token: 'tok', expiresAt: past(), user }));
    const store = createStore();

    await store.restoreSession();

    expect(api.me).not.toHaveBeenCalled();
    expect(store.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem(KEY)).toBeNull();
  });

  it('AuthStore.restoreSession clears session when /me returns 401', async () => {
    sessionStorage.setItem(KEY, JSON.stringify({ token: 'tok', expiresAt: future(), user }));
    api.me.mockReturnValue(throwError(() => ({ status: 401, message: 'x', fieldErrors: {} })));
    const store = createStore();

    await expect(store.restoreSession()).resolves.toBeUndefined();

    expect(api.me).toHaveBeenCalledOnce();
    expect(store.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem(KEY)).toBeNull();
  });

  it('restoreSession keeps a valid session and refreshes the user from /me', async () => {
    const fresh = { ...user, fullName: 'Ana Maria' };
    sessionStorage.setItem(KEY, JSON.stringify({ token: 'tok', expiresAt: future(), user }));
    api.me.mockReturnValue(of(fresh));
    const store = createStore();

    await store.restoreSession();

    expect(store.user()).toEqual(fresh);
    expect(store.token()).toBe('tok');
  });

  it('restoreSession ignores corrupt stored data', async () => {
    sessionStorage.setItem(KEY, '{not json');
    const store = createStore();

    await store.restoreSession();

    expect(api.me).not.toHaveBeenCalled();
    expect(store.isAuthenticated()).toBe(false);
  });
});
