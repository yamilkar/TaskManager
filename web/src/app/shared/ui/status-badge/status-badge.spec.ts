import { TestBed } from '@angular/core/testing';
import { TaskStatus } from '../../../features/tasks/data/task.model';
import { StatusBadge } from './status-badge';

describe('StatusBadge', () => {
  it.each<[TaskStatus, string]>([
    ['ToDo', 'To do'],
    ['InProgress', 'In progress'],
    ['Done', 'Done'],
  ])('labels %s as %s', (status, label) => {
    const fixture = TestBed.createComponent(StatusBadge);
    fixture.componentRef.setInput('status', status);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe(label);
  });
});
