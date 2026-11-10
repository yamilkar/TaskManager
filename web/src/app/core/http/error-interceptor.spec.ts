import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthStore } from '../auth/auth-store';
import { AppError } from './app-error';
import { errorInterceptor } from './error-interceptor';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;
  let navigate: ReturnType<typeof vi.fn>;
  let logout: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    navigate = vi.fn().mockResolvedValue(true);
    logout = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: { url: '/tasks', navigate } },
        { provide: AuthStore, useValue: { logout } },
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => ctrl.verify());

  function failWith(url: string, status: number): Promise<AppError> {
    const result = new Promise<AppError>((resolve) => {
      http.get(url).subscribe({ error: (e: AppError) => resolve(e) });
    });
    ctrl.expectOne(url).flush({ detail: 'nope' }, { status, statusText: 'Error' });
    return result;
  }

  it('errorInterceptor redirects to /login with reason=expired on 401 from tasks API', async () => {
    const err = await failWith('http://localhost:5090/api/v1/tasks', 401);

    expect(logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/tasks', reason: 'expired' },
    });
    expect(err.status).toBe(401);
    expect(err.message).toBe('nope');
  });

  it('errorInterceptor does NOT redirect on 401 from /api/v1/auth/login', async () => {
    const err = await failWith('http://localhost:5080/api/v1/auth/login', 401);

    expect(logout).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
    expect(err.status).toBe(401);
  });

  it('errorInterceptor does NOT redirect on 401 from /api/v1/users/me', async () => {
    await failWith('http://localhost:5080/api/v1/users/me', 401);

    expect(logout).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not redirect for non-401 errors but still rethrows an AppError', async () => {
    const err = await failWith('http://localhost:5090/api/v1/tasks', 500);

    expect(err.status).toBe(500);
    expect(navigate).not.toHaveBeenCalled();
  });
});
