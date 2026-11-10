import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatToolbar } from '@angular/material/toolbar';
import { Router, RouterOutlet } from '@angular/router';
import { AuthStore } from '../../core/auth/auth-store';

@Component({
  selector: 'app-shell',
  imports: [MatToolbar, MatButton, RouterOutlet],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);

  protected readonly user = this.auth.user;

  protected async signOut(): Promise<void> {
    this.auth.logout();
    await this.router.navigateByUrl('/login');
  }
}
