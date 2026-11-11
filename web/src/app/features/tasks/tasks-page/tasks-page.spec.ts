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
    ctrl.expectOne(URL).flush([mk('1', 'Existing')]);
    await settle(fixture);
    expect(el.querySelectorAll('li').length).toBe(1);
    expect(el.textContent).toContain('Existing');
  });

  it('TasksPage: after create shows "Task created" snackbar and new task first', async () => {
    const { fixture, el } = setup();
    ctrl.expectOne(URL).flush([mk('1', 'Existing')]);
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
    ctrl.expectOne(URL).flush([mk('1', 'Existing')]);
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
  describe('editing', () => {
    const rows = (el: HTMLElement) => Array.from(el.querySelectorAll('li')).map((li) => li.textContent ?? '');
    const heading = (el: HTMLElement) => el.querySelector('mat-card-title')!.textContent!.trim();
    const click = (el: HTMLElement, label: string) =>
      el.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`)!.click();
    const button = (el: HTMLElement, text: string) =>
      Array.from(el.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent!.trim() === text)!;
    const chooseMenuItem = (text: string) =>
      Array.from(document.querySelectorAll<HTMLElement>('[role=menuitem]'))
        .find((i) => i.textContent!.trim() === text)!
        .click();

    async function loaded(tasks: Task[]) {
      const ctx = setup();
      ctrl.expectOne(URL).flush(tasks);
      await settle(ctx.fixture);
      return ctx;
    }

    it('TasksPage: edit saves changes in place', async () => {
      const { fixture, el } = await loaded([mk('a', 'First'), mk('b', 'Second')]);

      click(el, 'Edit Second');
      await settle(fixture);
      expect(heading(el)).toBe('Edit task');
      type(el, 'New');
      button(el, 'Save changes').click();
      const req = ctrl.expectOne({ method: 'PUT', url: `${URL}/b` });
      expect(req.request.body).toEqual({ title: 'New', description: null, status: 'ToDo', dueDate: null });
      req.flush({ ...mk('b', 'New') });
      await settle(fixture);

      const list = rows(el);
      expect(list[0]).toContain('First');
      expect(list[1]).toContain('New');
      expect(document.body.textContent).toContain('Task updated');
      expect(heading(el)).toBe('Add a task');
      expect(el.querySelector<HTMLInputElement>('input[name=title]')!.value).toBe('');
    });

    it('TasksPage: failed save stays in edit mode with field errors', async () => {
      const { fixture, el } = await loaded([mk('b', 'Second')]);

      click(el, 'Edit Second');
      await settle(fixture);
      type(el, 'Typed');
      button(el, 'Save changes').click();
      ctrl
        .expectOne({ method: 'PUT', url: `${URL}/b` })
        .flush({ title: 'Bad', errors: { title: ['Title is required.'] } }, { status: 400, statusText: 'Bad Request' });
      await settle(fixture);
      fixture.detectChanges();

      expect(heading(el)).toBe('Edit task');
      expect(el.querySelector<HTMLInputElement>('input[name=title]')!.value).toBe('Typed');
      expect(el.querySelector('mat-card')!.textContent).toContain('Title is required.');
      expect(rows(el)[0]).toContain('Second');
    });

    it('TasksPage: cancel discards edits', async () => {
      const { fixture, el } = await loaded([mk('b', 'Second')]);

      click(el, 'Edit Second');
      await settle(fixture);
      type(el, 'Unsaved');
      button(el, 'Cancel').click();
      await settle(fixture);

      expect(heading(el)).toBe('Add a task');
      expect(el.querySelector<HTMLInputElement>('input[name=title]')!.value).toBe('');
      expect(rows(el)[0]).toContain('Second');
    });

    it('TasksPage: move menu changes status', async () => {
      const a: Task = { ...mk('a', 'First'), description: 'd', dueDate: '2099-01-01' };
      const { fixture, el } = await loaded([a]);

      click(el, 'Move First');
      await settle(fixture);
      chooseMenuItem('In progress');
      const req = ctrl.expectOne({ method: 'PUT', url: `${URL}/a` });
      expect(req.request.body).toEqual({ title: 'First', description: 'd', status: 'InProgress', dueDate: '2099-01-01' });
      req.flush({ ...a, status: 'InProgress' });
      await settle(fixture);

      expect(rows(el)[0]).toContain('In progress');
      expect(document.body.textContent).toContain('Moved to In progress');
    });

    it('TasksPage: move of an overdue task sends its unchanged due date', async () => {
      const a: Task = { ...mk('a', 'First'), dueDate: '2000-01-01' };
      const { fixture, el } = await loaded([a]);

      click(el, 'Move First');
      await settle(fixture);
      chooseMenuItem('Done');
      const req = ctrl.expectOne({ method: 'PUT', url: `${URL}/a` });
      expect(req.request.body.dueDate).toBe('2000-01-01');
      req.flush({ ...a, status: 'Done' });
      await settle(fixture);

      expect(rows(el)[0]).toContain('Done');
    });

    it('TasksPage: failed move shows the reason and keeps the list', async () => {
      const { fixture, el } = await loaded([mk('a', 'First')]);

      click(el, 'Move First');
      await settle(fixture);
      chooseMenuItem('Done');
      ctrl
        .expectOne({ method: 'PUT', url: `${URL}/a` })
        .flush({ title: 'Not Found', detail: 'Task not found.' }, { status: 404, statusText: 'Not Found' });
      await settle(fixture);

      expect(document.body.textContent).toContain("Couldn't move the task: Task not found.");
      expect(rows(el)[0]).toContain('To do');
    });
  });
  describe('pending saves', () => {
    const disabled = (el: HTMLElement, label: string) =>
      el.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`)!.disabled;

    async function loaded() {
      const ctx = setup();
      ctrl.expectOne(URL).flush([mk('a', 'First'), mk('b', 'Second')]);
      await settle(ctx.fixture);
      return ctx;
    }

    it('TasksPage: row actions and Cancel are disabled while an edit save is pending', async () => {
      const { fixture, el } = await loaded();
      el.querySelector<HTMLButtonElement>('button[aria-label="Edit Second"]')!.click();
      await settle(fixture);

      Array.from(el.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent!.trim() === 'Save changes')!.click();
      fixture.detectChanges();
      const req = ctrl.expectOne({ method: 'PUT', url: `${URL}/b` });

      expect(disabled(el, 'Edit First')).toBe(true);
      expect(disabled(el, 'Move First')).toBe(true);
      expect(Array.from(el.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent!.trim() === 'Cancel')!.disabled).toBe(true);

      req.flush(mk('b', 'Second'));
      await settle(fixture);
      await settle(fixture);
      expect(disabled(el, 'Edit First')).toBe(false);
      expect(disabled(el, 'Move First')).toBe(false);
    });

    it('TasksPage: row actions are disabled while a create is pending', async () => {
      const { fixture, el } = await loaded();
      type(el, 'Brand new');
      el.querySelector<HTMLButtonElement>('button[type=submit]')!.click();
      fixture.detectChanges();
      const req = ctrl.expectOne((r) => r.method === 'POST');

      expect(disabled(el, 'Edit First')).toBe(true);

      req.flush(mk('c', 'Brand new'));
      await settle(fixture);
      await settle(fixture);
      expect(disabled(el, 'Edit First')).toBe(false);
    });

    it('TasksPage: Edit is disabled for a task whose move is pending', async () => {
      const { fixture, el } = await loaded();
      el.querySelector<HTMLButtonElement>('button[aria-label="Move First"]')!.click();
      await settle(fixture);
      Array.from(document.querySelectorAll<HTMLElement>('[role=menuitem]')).find((i) => i.textContent!.trim() === 'Done')!.click();
      fixture.detectChanges();
      const req = ctrl.expectOne({ method: 'PUT', url: `${URL}/a` });

      expect(disabled(el, 'Edit First')).toBe(true);
      expect(disabled(el, 'Edit Second')).toBe(false);

      req.flush({ ...mk('a', 'First'), status: 'Done' });
      await settle(fixture);
      await settle(fixture);
      expect(disabled(el, 'Edit First')).toBe(false);
    });
  });
});
