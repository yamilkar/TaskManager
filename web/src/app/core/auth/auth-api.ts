import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';
import { AuthResponse, LoginRequest, RegisterRequest, User } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(APP_CONFIG).usersApiUrl;

  login(req: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/api/v1/auth/login`, req);
  }

  register(req: RegisterRequest): Observable<User> {
    return this.http.post<User>(`${this.baseUrl}/api/v1/auth/register`, req);
  }

  me(): Observable<User> {
    return this.http.get<User>(`${this.baseUrl}/api/v1/users/me`);
  }
}
