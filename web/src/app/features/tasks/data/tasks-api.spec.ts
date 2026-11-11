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

  it('list GETs /api/v1/tasks on the tasks API', () => {
    let result: unknown;
    api.list().subscribe((r) => (result = r));
    const req = ctrl.expectOne('http://tasks/api/v1/tasks');
    expect(req.request.method).toBe('GET');
    req.flush([task]);
    expect(result).toEqual([task]);
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

  it('update PUTs the request body to /api/v1/tasks/{id}', () => {
    let result: unknown;
    const body = { title: 'B', description: null, status: 'Done' as const, dueDate: '2026-12-01' };
    api.update('t1', body).subscribe((r) => (result = r));
    const req = ctrl.expectOne({ method: 'PUT', url: 'http://tasks/api/v1/tasks/t1' });
    expect(req.request.body).toEqual(body);
    req.flush(task);
    expect(result).toEqual(task);
  });

  it('delete sends DELETE to /api/v1/tasks/{id}', () => {
    let done = false;
    api.delete('7').subscribe({ complete: () => (done = true) });
    ctrl.expectOne({ method: 'DELETE', url: 'http://tasks/api/v1/tasks/7' }).flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });
});
