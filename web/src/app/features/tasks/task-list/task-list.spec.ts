import { TestBed } from '@angular/core/testing';
import { Task } from '../data/task.model';
import { TaskList } from './task-list';

const mk = (over: Partial<Task>): Task => ({
  id: '1',
  title: 'T',
  description: null,
  status: 'ToDo',
  dueDate: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  ...over,
});

describe('TaskList', () => {
  function setup(tasks: Task[]) {
    const fixture = TestBed.createComponent(TaskList);
    fixture.componentRef.setInput('tasks', tasks);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('TaskList: renders empty state when no tasks', () => {
    const el = setup([]);
    expect(el.textContent).toContain('No tasks yet — add your first one');
    expect(el.querySelectorAll('li').length).toBe(0);
  });

  it('TaskList: renders title, status badge and due date for each task', () => {
    const el = setup([
      mk({ id: '1', title: 'First', status: 'InProgress', dueDate: '2027-01-05' }),
      mk({ id: '2', title: 'Second', status: 'Done' }),
    ]);
    const items = el.querySelectorAll('li');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('First');
    expect(items[0].textContent).toContain('In progress');
    expect(items[0].textContent).toContain('Jan 5, 2027');
    expect(items[1].textContent).toContain('Second');
    expect(items[1].textContent).toContain('Done');
    expect(el.textContent).not.toContain('No tasks yet');
  });
});
