import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../../core/config/app-config';
import { CreateTaskRequest, Task } from './task.model';

@Injectable({ providedIn: 'root' })
export class TasksApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(APP_CONFIG).tasksApiUrl;

  list(): Observable<Task[]> {
    return this.http.get<Task[]>(`${this.baseUrl}/api/v1/tasks`);
  }

  create(req: CreateTaskRequest): Observable<Task> {
    return this.http.post<Task>(`${this.baseUrl}/api/v1/tasks`, req);
  }
}
