import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APP_CONFIG } from '../config/app-config';
import { AuthApi } from './auth-api';

describe('AuthApi', () => {
  let api: AuthApi;
  let ctrl: HttpTestingController;
  const user = { id: '1', fullName: 'Ana', email: 'a@x.com', createdAt: '2026-01-01T00:00:00Z' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: { usersApiUrl: 'http://users', tasksApiUrl: 'http://tasks' } },
      ],
    });
    api = TestBed.inject(AuthApi);
    ctrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => ctrl.verify());

  it('login POSTs credentials to the users API', () => {
    let result: unknown;
    api.login({ email: 'a@x.com', password: 'pw' }).subscribe((r) => (result = r));
    const req = ctrl.expectOne('http://users/api/v1/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'a@x.com', password: 'pw' });
    const body = { accessToken: 't', expiresAt: '2030-01-01T00:00:00Z', user };
    req.flush(body);
    expect(result).toEqual(body);
  });

  it('register POSTs to /auth/register', () => {
    api.register({ fullName: 'Ana', email: 'a@x.com', password: 'pw' }).subscribe();
    const req = ctrl.expectOne('http://users/api/v1/auth/register');
    expect(req.request.method).toBe('POST');
    req.flush(user);
  });

  it('me GETs /users/me', () => {
    api.me().subscribe();
    const req = ctrl.expectOne('http://users/api/v1/users/me');
    expect(req.request.method).toBe('GET');
    req.flush(user);
  });
});
