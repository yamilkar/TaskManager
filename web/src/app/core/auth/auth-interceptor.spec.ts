import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { APP_CONFIG } from '../config/app-config';
import { authInterceptor } from './auth-interceptor';
import { AuthStore } from './auth-store';

describe('authInterceptor', () => {
  const token = signal<string | null>('tok');
  let http: HttpClient;
  let ctrl: HttpTestingController;

  beforeEach(() => {
    token.set('tok');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: { usersApiUrl: 'http://localhost:5080', tasksApiUrl: 'http://localhost:5090' } },
        { provide: AuthStore, useValue: { token } },
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => ctrl.verify());

  it('authInterceptor attaches bearer only to configured API origins', () => {
    http.get('http://localhost:5080/api/v1/users/me').subscribe();
    http.get('http://localhost:5090/api/v1/tasks').subscribe();
    http.get('http://evil.example.com/steal').subscribe();

    expect(ctrl.expectOne('http://localhost:5080/api/v1/users/me').request.headers.get('Authorization')).toBe(
      'Bearer tok',
    );
    expect(ctrl.expectOne('http://localhost:5090/api/v1/tasks').request.headers.get('Authorization')).toBe(
      'Bearer tok',
    );
    expect(ctrl.expectOne('http://evil.example.com/steal').request.headers.has('Authorization')).toBe(false);
  });

  it('does not attach the bearer to lookalike hosts or ports', () => {
    http.get('http://localhost:5080.evil.com/x').subscribe();
    http.get('http://localhost:50801/x').subscribe();

    expect(ctrl.expectOne('http://localhost:5080.evil.com/x').request.headers.has('Authorization')).toBe(false);
    expect(ctrl.expectOne('http://localhost:50801/x').request.headers.has('Authorization')).toBe(false);
  });

  it('does not attach a header when there is no token', () => {
    token.set(null);
    http.get('http://localhost:5090/api/v1/tasks').subscribe();

    expect(ctrl.expectOne('http://localhost:5090/api/v1/tasks').request.headers.has('Authorization')).toBe(false);
  });
});
