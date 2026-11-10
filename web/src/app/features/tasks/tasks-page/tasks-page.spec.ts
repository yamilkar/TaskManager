import { HttpErrorResponse, HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { catchError, throwError } from 'rxjs';
import { APP_CONFIG } from '../../../core/config/app-config';
import { toAppError } from '../../../core/http/app-error';
import { Task } from '../data/task.model';
import { TasksPage } from './tasks-page';

// Stand-in for the real errorInterceptor (which needs auth + router): same error mapping.
const toAppErrorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((e: HttpErrorResponse) => throwError(() => toAppError(e))),
  );

const URL = 'http://tasks/api/v1/tasks';
const mk = (id: string, title: string): Task => ({
  id,
  title,
  description: null,
  status: 'ToDo',
  dueDate: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
});

describe('TasksPage', () => {
  let ctrl: HttpTestingController;

  function setup() {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([toAppErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: { usersApiUrl: 'http://users', tasksApiUrl: 'http://tasks' } },
      ],
    });
    ctrl = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(TasksPage);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  const settle = async (fixture: { whenStable(): Promise<unknown>; detectChanges(): void }) => {
    await fixture.whenStable();
    fixture.detectChanges();
  };

  function type(el: HTMLElement, value: string) {
    const input = el.querySelector<HTMLInputElement>('input[name=title]')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  afterEach(() => ctrl.verify());

  it('TasksPage: loads tasks on init', async () => {
    const { fixture, el } = setup();
    ctrl.expectOne(URL).flush({ items: [mk('1', 'Existing')], nextCursor: null });
    await settle(fixture);
    expect(el.querySelectorAll('li').length).toBe(1);
    expect(el.textContent).toContain('Existing');
  });

  it('TasksPage: after create shows "Task created" snackbar and new task first', async () => {
    const { fixture, el } = setup();
    ctrl.expectOne(URL).flush({ items: [mk('1', 'Existing')], nextCursor: null });
    await settle(fixture);

    type(el, 'Brand new');
    el.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    const req = ctrl.expectOne((r) => r.method === 'POST' && r.url === URL);
    expect(req.request.body).toEqual({ title: 'Brand new', description: null, status: 'ToDo', dueDate: null });
    req.flush(mk('2', 'Brand new'));
    await settle(fixture);

    const titles = Array.from(el.querySelectorAll('li')).map((li) => li.textContent);
    expect(titles[0]).toContain('Brand new');
    expect(titles[1]).toContain('Existing');
    expect(el.querySelector<HTMLInputElement>('input[name=title]')!.value).toBe('');
    // MatSnackBar renders in the overlay container, outside the fixture; works without provideAnimationsAsync.
    expect(document.body.textContent).toContain('Task created');
  });

  it('TasksPage: maps server field errors onto the form and keeps the list', async () => {
    const { fixture, el } = setup();
    ctrl.expectOne(URL).flush({ items: [mk('1', 'Existing')], nextCursor: null });
    await settle(fixture);

    type(el, 'x');
    el.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
    ctrl
      .expectOne((r) => r.method === 'POST')
      .flush({ title: 'Bad', errors: { title: ['Title is invalid.'] } }, { status: 400, statusText: 'Bad Request' });
    await settle(fixture);
    fixture.detectChanges();

    expect(el.textContent).toContain('Title is invalid.');
    expect(el.querySelectorAll('li').length).toBe(1);
  });
});
