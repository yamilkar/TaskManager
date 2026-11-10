import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APP_CONFIG } from '../../../core/config/app-config';
import { Task } from './task.model';
import { TasksApi } from './tasks-api';

describe('TasksApi', () => {
  let api: TasksApi;
  let ctrl: HttpTestingController;
  const task: Task = {
    id: '1',
    title: 'A',
    description: null,
    status: 'ToDo',
    dueDate: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: { usersApiUrl: 'http://users', tasksApiUrl: 'http://tasks' } },
      ],
    });
    api = TestBed.inject(TasksApi);
    ctrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => ctrl.verify());

  it('list GETs /api/v1/tasks without params by default', () => {
    let result: unknown;
    api.list().subscribe((r) => (result = r));
    const req = ctrl.expectOne('http://tasks/api/v1/tasks');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.keys()).toEqual([]);
    req.flush({ items: [task], nextCursor: null });
    expect(result).toEqual({ items: [task], nextCursor: null });
  });

  it('list sends cursor and limit as query params', () => {
    api.list({ limit: 5, cursor: 'c1' }).subscribe();
    const req = ctrl.expectOne((r) => r.url === 'http://tasks/api/v1/tasks');
    expect(req.request.params.get('cursor')).toBe('c1');
    expect(req.request.params.get('limit')).toBe('5');
    req.flush({ items: [], nextCursor: null });
  });

  it('create POSTs the request body', () => {
    let result: unknown;
    api.create({ title: 'A', status: 'ToDo' }).subscribe((r) => (result = r));
    const req = ctrl.expectOne('http://tasks/api/v1/tasks');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ title: 'A', status: 'ToDo' });
    req.flush(task);
    expect(result).toEqual(task);
  });
});
