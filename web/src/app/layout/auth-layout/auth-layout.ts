import { ChangeDetectionStrategy, Component } from '@angular/core';
import { BrandLogo } from '../../shared/ui/brand-logo/brand-logo';
import { ThemeToggle } from '../../shared/ui/theme-toggle/theme-toggle';

/** The signed-out landing: a purple hero beside the projected sign-in or register card. */
@Component({
  selector: 'app-auth-layout',
  imports: [BrandLogo, ThemeToggle],
  templateUrl: './auth-layout.html',
  styleUrl: './auth-layout.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthLayout {}
