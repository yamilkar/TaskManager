import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { AppError } from '../../../core/http/app-error';
import { Task } from './task.model';
import { TasksApi } from './tasks-api';
import { TasksStore } from './tasks-store';

const mk = (id: string): Task => ({
  id,
  title: `Task ${id}`,
  description: null,
  status: 'ToDo',
  dueDate: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
});

describe('TasksStore', () => {
  const api = {
    list: vi.fn<() => Observable<Task[]>>(),
    create: vi.fn<(r: unknown) => Observable<Task>>(),
    update: vi.fn<(id: string, r: unknown) => Observable<Task>>(),
    delete: vi.fn<(id: string) => Observable<void>>(),
  };

  function setup() {
    TestBed.configureTestingModule({
      providers: [TasksStore, { provide: TasksApi, useValue: api }],
    });
    return TestBed.inject(TasksStore);
  }

  beforeEach(() => {
    api.list.mockReset();
    api.create.mockReset();
    api.update.mockReset();
    api.delete.mockReset();
  });

  it('TasksStore.load sets loading and stores tasks', async () => {
    const subject = new Subject<Task[]>();
    api.list.mockReturnValue(subject);
    const store = setup();

    const pending = store.load();
    expect(store.loading()).toBe(true);
    subject.next([mk('1'), mk('2')]);
    subject.complete();
    await pending;

    expect(store.loading()).toBe(false);
    expect(store.tasks().map((t) => t.id)).toEqual(['1', '2']);
    expect(store.error()).toBeNull();
  });

  it('TasksStore.load sets error on failure', async () => {
    const err: AppError = { status: 500, message: 'boom', fieldErrors: {} };
    api.list.mockReturnValue(throwError(() => err));
    const store = setup();

    await store.load();

    expect(store.loading()).toBe(false);
    expect(store.error()).toEqual(err);
    expect(store.tasks()).toEqual([]);
  });

  it('TasksStore.create prepends task to list', async () => {
    api.list.mockReturnValue(of([mk('1')]));
    api.create.mockReturnValue(of(mk('2')));
    const store = setup();
    await store.load();

    const created = await store.create({ title: 'Task 2' });

    expect(created.id).toBe('2');
    expect(store.tasks().map((t) => t.id)).toEqual(['2', '1']);
  });

  it('TasksStore.create rethrows failures and leaves the list unchanged', async () => {
    const err: AppError = { status: 400, message: 'bad', fieldErrors: { title: ['x'] } };
    api.list.mockReturnValue(of([mk('1')]));
    api.create.mockReturnValue(throwError(() => err));
    const store = setup();
    await store.load();

    await expect(store.create({ title: '' })).rejects.toEqual(err);
    expect(store.tasks().map((t) => t.id)).toEqual(['1']);
  });

  const edit = { title: 'Changed', description: null, status: 'Done' as const, dueDate: null };

  it('TasksStore.update replaces the task in place', async () => {
    api.list.mockReturnValue(of([mk('a'), mk('b'), mk('c')]));
    api.update.mockReturnValue(of({ ...mk('b'), title: 'Changed', status: 'Done' }));
    const store = setup();
    await store.load();

    const updated = await store.update('b', edit);

    expect(api.update).toHaveBeenCalledWith('b', edit);
    expect(updated.title).toBe('Changed');
    expect(store.tasks().map((t) => [t.id, t.title])).toEqual([
      ['a', 'Task a'],
      ['b', 'Changed'],
      ['c', 'Task c'],
    ]);
  });

  it('TasksStore.update rethrows failures and leaves the list unchanged', async () => {
    const err: AppError = { status: 400, message: 'bad', fieldErrors: { title: ['x'] } };
    api.list.mockReturnValue(of([mk('a'), mk('b'), mk('c')]));
    api.update.mockReturnValue(throwError(() => err));
    const store = setup();
    await store.load();
    const before = store.tasks();

    await expect(store.update('b', edit)).rejects.toEqual(err);
    expect(store.tasks()).toBe(before);
  });

  it('TasksStore.remove drops the task from the list', async () => {
    api.list.mockReturnValue(of([mk('a'), mk('b')]));
    api.delete.mockReturnValue(of(undefined));
    const store = setup();
    await store.load();

    await store.remove('a');

    expect(api.delete).toHaveBeenCalledWith('a');
    expect(store.tasks().map((t) => t.id)).toEqual(['b']);
  });

  it('TasksStore.remove treats 404 as already removed', async () => {
    const gone: AppError = { status: 404, message: 'Task not found.', code: 'tasks.not_found', fieldErrors: {} };
    api.list.mockReturnValue(of([mk('a')]));
    api.delete.mockReturnValue(throwError(() => gone));
    const store = setup();
    await store.load();

    await store.remove('a');

    expect(store.tasks()).toEqual([]);
  });

  it('TasksStore.remove rethrows a 404 that is not task-not-found and keeps the task', async () => {
    const wrongRoute: AppError = { status: 404, message: 'Not Found', fieldErrors: {} };
    api.list.mockReturnValue(of([mk('a')]));
    api.delete.mockReturnValue(throwError(() => wrongRoute));
    const store = setup();
    await store.load();

    await expect(store.remove('a')).rejects.toEqual(wrongRoute);
    expect(store.tasks().map((t) => t.id)).toEqual(['a']);
  });

  it('TasksStore.remove rethrows other failures and leaves the list unchanged', async () => {
    const err: AppError = { status: 500, message: 'boom', fieldErrors: {} };
    api.list.mockReturnValue(of([mk('a')]));
    api.delete.mockReturnValue(throwError(() => err));
    const store = setup();
    await store.load();
    const before = store.tasks();

    await expect(store.remove('a')).rejects.toEqual(err);
    expect(store.tasks()).toBe(before);
  });
});
