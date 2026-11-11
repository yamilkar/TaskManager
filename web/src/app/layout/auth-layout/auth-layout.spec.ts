import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthLayout } from './auth-layout';

@Component({
  imports: [AuthLayout],
  template: `<app-auth-layout><p class="projected">Form goes here</p></app-auth-layout>`,
})
class Host {}

describe('AuthLayout', () => {
  function setup() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('AuthLayout_render_showsTheBrandAndHeadline', () => {
    const el = setup();

    expect(el.textContent).toContain('TaskManager');
    expect(el.querySelector('h1')!.textContent).toContain('Your tasks, from To do to Done.');
  });

  it('AuthLayout_render_projectsTheFormIntoTheMainArea', () => {
    const el = setup();

    expect(el.querySelector('main .projected')!.textContent).toBe('Form goes here');
  });

  it('AuthLayout_render_offersTheThemeSwitch', () => {
    const el = setup();

    expect(el.querySelector('app-theme-toggle')).not.toBeNull();
  });
});
