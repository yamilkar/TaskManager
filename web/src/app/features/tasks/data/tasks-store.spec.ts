import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { AppError } from '../../../core/http/app-error';
import { Task, TaskPage } from './task.model';
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

const page = (ids: string[], nextCursor: string | null = null): TaskPage => ({
  items: ids.map(mk),
  nextCursor,
});

describe('TasksStore', () => {
  const api = {
    list: vi.fn<(p?: { limit?: number; cursor?: string }) => Observable<TaskPage>>(),
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
    const subject = new Subject<TaskPage>();
    api.list.mockReturnValue(subject);
    const store = setup();

    const pending = store.load();
    expect(store.loading()).toBe(true);
    subject.next(page(['1', '2']));
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
    api.list.mockReturnValue(of(page(['1'])));
    api.create.mockReturnValue(of(mk('2')));
    const store = setup();
    await store.load();

    const created = await store.create({ title: 'Task 2' });

    expect(created.id).toBe('2');
    expect(store.tasks().map((t) => t.id)).toEqual(['2', '1']);
  });

  it('TasksStore.create rethrows failures and leaves the list unchanged', async () => {
    const err: AppError = { status: 400, message: 'bad', fieldErrors: { title: ['x'] } };
    api.list.mockReturnValue(of(page(['1'])));
    api.create.mockReturnValue(throwError(() => err));
    const store = setup();
    await store.load();

    await expect(store.create({ title: '' })).rejects.toEqual(err);
    expect(store.tasks().map((t) => t.id)).toEqual(['1']);
  });

  it('TasksStore.load replaces tasks and stores nextCursor', async () => {
    api.list.mockReturnValue(of(page(['1', '2'], 'c1')));
    const store = setup();

    await store.load();

    expect(store.tasks().map((t) => t.id)).toEqual(['1', '2']);
    expect(store.hasMore()).toBe(true);
  });

  it('TasksStore.loadMore appends the next page and clears hasMore on the last page', async () => {
    api.list.mockReturnValueOnce(of(page(['1', '2'], 'c1'))).mockReturnValueOnce(of(page(['3'])));
    const store = setup();
    await store.load();

    await store.loadMore();

    expect(api.list).toHaveBeenLastCalledWith({ cursor: 'c1' });
    expect(store.tasks().map((t) => t.id)).toEqual(['1', '2', '3']);
    expect(store.hasMore()).toBe(false);
    expect(store.loadingMore()).toBe(false);
  });

  it('TasksStore.loadMore skips tasks already in the list', async () => {
    api.list.mockReturnValueOnce(of(page(['1', '2'], 'c1'))).mockReturnValueOnce(of(page(['2', '3'])));
    const store = setup();
    await store.load();

    await store.loadMore();

    expect(store.tasks().map((t) => t.id)).toEqual(['1', '2', '3']);
  });

  it('TasksStore.loadMore does nothing without a cursor', async () => {
    api.list.mockReturnValue(of(page(['1'])));
    const store = setup();
    await store.load();

    await store.loadMore();

    expect(api.list).toHaveBeenCalledTimes(1);
  });

  it('TasksStore.loadMore ignores a second call while one is in flight', async () => {
    const subject = new Subject<TaskPage>();
    api.list.mockReturnValueOnce(of(page(['1'], 'c1'))).mockReturnValueOnce(subject);
    const store = setup();
    await store.load();

    const first = store.loadMore();
    const second = store.loadMore();
    expect(store.loadingMore()).toBe(true);
    subject.next(page(['2']));
    subject.complete();
    await Promise.all([first, second]);

    expect(api.list).toHaveBeenCalledTimes(2);
    expect(store.tasks().map((t) => t.id)).toEqual(['1', '2']);
  });

  it('TasksStore.loadMore keeps tasks and cursor and sets loadMoreError on failure', async () => {
    const err: AppError = { status: 500, message: 'boom', fieldErrors: {} };
    api.list
      .mockReturnValueOnce(of(page(['1'], 'c1')))
      .mockReturnValueOnce(throwError(() => err))
      .mockReturnValueOnce(of(page(['2'])));
    const store = setup();
    await store.load();

    await store.loadMore();

    expect(store.loadMoreError()).toEqual(err);
    expect(store.tasks().map((t) => t.id)).toEqual(['1']);
    expect(store.hasMore()).toBe(true);
    expect(store.loadingMore()).toBe(false);

    await store.loadMore();

    expect(store.loadMoreError()).toBeNull();
    expect(store.tasks().map((t) => t.id)).toEqual(['1', '2']);
  });

  it('TasksStore.create then loadMore keeps the new task first and once', async () => {
    api.list.mockReturnValueOnce(of(page(['1'], 'c1'))).mockReturnValueOnce(of(page(['9', '1', '2'])));
    api.create.mockReturnValue(of(mk('9')));
    const store = setup();
    await store.load();
    await store.create({ title: 'Task 9' });

    await store.loadMore();

    expect(store.tasks().map((t) => t.id)).toEqual(['9', '1', '2']);
  });
});
