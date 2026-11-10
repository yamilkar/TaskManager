import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth-store';
import { AppError } from '../../../core/http/app-error';
import { Login } from './login';

describe('Login', () => {
  const store = { login: vi.fn<(r: unknown) => Promise<void>>() };
  let navigate: ReturnType<typeof vi.spyOn>;

  function setup(inputs: Record<string, string> = {}) {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthStore, useValue: store }],
    });
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(Login);
    for (const [k, v] of Object.entries(inputs)) {
      fixture.componentRef.setInput(k, v);
    }
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  const email = (el: HTMLElement) => el.querySelector<HTMLInputElement>('input[type=email]')!;
  const password = (el: HTMLElement) => el.querySelector<HTMLInputElement>('input[type=password]')!;
  const submit = (el: HTMLElement) => el.querySelector<HTMLButtonElement>('button[type=submit]')!;
  function type(input: HTMLInputElement, value: string) {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }
  const buttonByText = (el: HTMLElement, text: string) =>
    Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.includes(text))!;

  beforeEach(() => {
    store.login.mockReset();
  });

  it('Login: "Use demo account" fills email and password', async () => {
    const { fixture, el } = setup();
    expect(el.textContent).toContain('Demo account: demo@taskmanager.dev / Demo123!');

    buttonByText(el, 'Use demo account').click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(email(el).value).toBe('demo@taskmanager.dev');
    expect(password(el).value).toBe('Demo123!');
  });

  it('Login: on 401 shows "Invalid email or password.", keeps email, clears password, does not navigate', async () => {
    const err: AppError = { status: 401, message: 'Invalid email or password.', fieldErrors: {} };
    store.login.mockRejectedValue(err);
    const { fixture, el } = setup();
    type(email(el), 'ana@x.com');
    type(password(el), 'Wrong123!');
    fixture.detectChanges();

    submit(el).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el.querySelector('[role=alert]')?.textContent).toContain('Invalid email or password.');
    expect(email(el).value).toBe('ana@x.com');
    expect(password(el).value).toBe('');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('Login: on success navigates to returnUrl or /tasks', async () => {
    store.login.mockResolvedValue();
    const a = setup({ returnUrl: '/tasks/1' });
    type(email(a.el), 'ana@x.com');
    type(password(a.el), 'Passw0rd!');
    a.fixture.detectChanges();
    submit(a.el).click();
    await a.fixture.whenStable();
    expect(navigate).toHaveBeenCalledWith('/tasks/1');

    TestBed.resetTestingModule();
    const b = setup();
    type(email(b.el), 'ana@x.com');
    type(password(b.el), 'Passw0rd!');
    b.fixture.detectChanges();
    submit(b.el).click();
    await b.fixture.whenStable();
    expect(navigate).toHaveBeenCalledWith('/tasks');
  });

  it('Login: submit disabled while request in flight; required/email validators show messages', async () => {
    let resolve!: () => void;
    store.login.mockReturnValue(new Promise<void>((r) => (resolve = r)));
    const { fixture, el } = setup();

    submit(el).click();
    fixture.detectChanges();
    expect(store.login).not.toHaveBeenCalled();
    expect(el.textContent).toContain('Email is required.');
    expect(el.textContent).toContain('Password is required.');

    type(email(el), 'nope');
    fixture.detectChanges();
    expect(el.textContent).toContain('Enter a valid email address.');

    type(email(el), 'ana@x.com');
    type(password(el), 'Passw0rd!');
    fixture.detectChanges();
    submit(el).click();
    fixture.detectChanges();
    expect(submit(el).disabled).toBe(true);

    resolve();
    await new Promise((r) => setTimeout(r));
    fixture.detectChanges();
    expect(submit(el).disabled).toBe(false);
  });

  it('Login: shows session-expired notice when reason=expired', () => {
    const { el } = setup({ reason: 'expired' });
    expect(el.textContent).toContain('Your session has expired. Please sign in again.');
  });
});
