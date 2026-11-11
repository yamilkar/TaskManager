import { TestBed } from '@angular/core/testing';
import { Task, TaskStatus } from '../data/task.model';
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
  function setupFixture(tasks: Task[], editingId: string | null = null) {
    const fixture = TestBed.createComponent(TaskList);
    fixture.componentRef.setInput('tasks', tasks);
    fixture.componentRef.setInput('editingId', editingId);
    fixture.detectChanges();
    return fixture;
  }

  function setup(tasks: Task[]) {
    return setupFixture(tasks).nativeElement as HTMLElement;
  }

  const menuItems = () => Array.from(document.querySelectorAll<HTMLElement>('[role=menuitem]'));

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

  it('TaskList: each row has an Edit button labelled with the task title', () => {
    const task = mk({ id: '1', title: 'Buy milk' });
    const fixture = setupFixture([task]);
    const edited: Task[] = [];
    fixture.componentInstance.edit.subscribe((t) => edited.push(t));
    const el = fixture.nativeElement as HTMLElement;

    const button = el.querySelector<HTMLButtonElement>('button[aria-label="Edit Buy milk"]')!;
    expect(button.textContent!.trim()).toBe('Edit');
    button.click();

    expect(edited).toEqual([task]);
  });

  it('TaskList: Move menu lists only the other two statuses', async () => {
    const fixture = setupFixture([mk({ id: '1', title: 'Buy milk', status: 'ToDo' })]);
    const el = fixture.nativeElement as HTMLElement;

    el.querySelector<HTMLButtonElement>('button[aria-label="Move Buy milk"]')!.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(menuItems().map((i) => i.textContent!.trim())).toEqual(['In progress', 'Done']);
  });

  it('TaskList: choosing a status emits move', async () => {
    const task = mk({ id: '1', title: 'Buy milk', status: 'ToDo' });
    const fixture = setupFixture([task]);
    const moves: { task: Task; status: TaskStatus }[] = [];
    fixture.componentInstance.move.subscribe((m) => moves.push(m));
    const el = fixture.nativeElement as HTMLElement;

    el.querySelector<HTMLButtonElement>('button[aria-label="Move Buy milk"]')!.click();
    fixture.detectChanges();
    await fixture.whenStable();
    menuItems().find((i) => i.textContent!.trim() === 'Done')!.click();

    expect(moves).toEqual([{ task, status: 'Done' }]);
  });

  it('TaskList: the row being edited has Move disabled', () => {
    const el = setupFixture(
      [mk({ id: '1', title: 'First' }), mk({ id: '2', title: 'Second' })],
      '1',
    ).nativeElement as HTMLElement;

    expect(el.querySelector<HTMLButtonElement>('button[aria-label="Move First"]')!.disabled).toBe(true);
    expect(el.querySelector<HTMLButtonElement>('button[aria-label="Move Second"]')!.disabled).toBe(false);
  });
});
