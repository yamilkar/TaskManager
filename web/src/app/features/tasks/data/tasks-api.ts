import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../../core/config/app-config';
import { CreateTaskRequest, Task, TaskPage } from './task.model';

@Injectable({ providedIn: 'root' })
export class TasksApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(APP_CONFIG).tasksApiUrl;

  list(query: { limit?: number; cursor?: string } = {}): Observable<TaskPage> {
    let params = new HttpParams();
    if (query.limit !== undefined) params = params.set('limit', query.limit);
    if (query.cursor !== undefined) params = params.set('cursor', query.cursor);
    return this.http.get<TaskPage>(`${this.baseUrl}/api/v1/tasks`, { params });
  }

  create(req: CreateTaskRequest): Observable<Task> {
    return this.http.post<Task>(`${this.baseUrl}/api/v1/tasks`, req);
  }
}
