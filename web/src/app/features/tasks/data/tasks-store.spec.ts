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
});
