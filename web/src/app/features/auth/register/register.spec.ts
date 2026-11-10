import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth-store';
import { AppError } from '../../../core/http/app-error';
import { Register } from './register';

describe('Register', () => {
  const store = { register: vi.fn<(r: unknown) => Promise<void>>() };
  let navigate: ReturnType<typeof vi.spyOn>;

  function setup() {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthStore, useValue: store }],
    });
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(Register);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  const q = (el: HTMLElement, sel: string) => el.querySelector<HTMLInputElement>(sel)!;
  function type(input: HTMLInputElement, value: string) {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }
  function fill(el: HTMLElement, confirm = 'Passw0rd!') {
    type(q(el, 'input[name=fullName]'), 'Ana Lopez');
    type(q(el, 'input[type=email]'), 'ana@x.com');
    type(q(el, 'input[name=password]'), 'Passw0rd!');
    type(q(el, 'input[name=confirmPassword]'), confirm);
  }
  const submit = (el: HTMLElement) => q(el, 'button[type=submit]');

  beforeEach(() => {
    store.register.mockReset();
  });

  it('Register: password mismatch shows "Passwords do not match." and does not submit', () => {
    const { fixture, el } = setup();
    fill(el, 'Different1');
    fixture.detectChanges();
    submit(el).click();
    fixture.detectChanges();

    expect(el.textContent).toContain('Passwords do not match.');
    expect(store.register).not.toHaveBeenCalled();
  });

  it('Register: 409 sets server error on the email control', async () => {
    const err: AppError = { status: 409, message: 'Email already registered.', fieldErrors: {} };
    store.register.mockRejectedValue(err);
    const { fixture, el } = setup();
    fill(el);
    fixture.detectChanges();
    submit(el).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el.textContent).toContain('Email already registered.');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('Register: success navigates to /tasks', async () => {
    store.register.mockResolvedValue();
    const { fixture, el } = setup();
    fill(el);
    fixture.detectChanges();
    submit(el).click();
    await fixture.whenStable();

    expect(store.register).toHaveBeenCalledWith({
      fullName: 'Ana Lopez',
      email: 'ana@x.com',
      password: 'Passw0rd!',
    });
    expect(navigate).toHaveBeenCalledWith('/tasks');
  });
});
