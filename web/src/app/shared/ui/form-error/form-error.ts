import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatError } from '@angular/material/form-field';

/** Renders each message as a `<mat-error>`; use with the `matError` attribute inside a `mat-form-field`. */
@Component({
  selector: 'app-form-error',
  imports: [MatError],
  template: `
    @for (message of errors(); track message) {
      <mat-error>{{ message }}</mat-error>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormError {
  readonly errors = input.required<string[] | undefined>();
}
