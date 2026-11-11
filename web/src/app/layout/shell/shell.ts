import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { AuthStore } from '../../core/auth/auth-store';
import { BrandLogo } from '../../shared/ui/brand-logo/brand-logo';
import { initials } from '../../shared/ui/initials';
import { ThemeToggle } from '../../shared/ui/theme-toggle/theme-toggle';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, BrandLogo, ThemeToggle],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);

  protected readonly user = this.auth.user;
  protected readonly initials = computed(() => initials(this.user()?.fullName ?? ''));

  protected async signOut(): Promise<void> {
    this.auth.logout();
    await this.router.navigateByUrl('/login');
  }
}
