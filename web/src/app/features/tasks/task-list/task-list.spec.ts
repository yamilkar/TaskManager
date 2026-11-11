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

  describe('board', () => {
    const column = (el: HTMLElement, label: string) => el.querySelector<HTMLElement>(`section[aria-label="${label}"]`)!;
    const titlesIn = (col: HTMLElement) => Array.from(col.querySelectorAll('li .title')).map((t) => t.textContent!.trim());

    it('TaskList_tasks_areGroupedIntoStatusColumnsInOrder', () => {
      const el = setup([
        mk({ id: '1', title: 'A', status: 'Done' }),
        mk({ id: '2', title: 'B', status: 'ToDo' }),
        mk({ id: '3', title: 'C', status: 'InProgress' }),
        mk({ id: '4', title: 'D', status: 'ToDo' }),
      ]);

      const labels = Array.from(el.querySelectorAll('section')).map((s) => s.getAttribute('aria-label'));
      expect(labels).toEqual(['To do', 'In progress', 'Done']);
      expect(titlesIn(column(el, 'To do'))).toEqual(['B', 'D']);
      expect(titlesIn(column(el, 'In progress'))).toEqual(['C']);
      expect(titlesIn(column(el, 'Done'))).toEqual(['A']);
    });

    it('TaskList_columns_showHowManyTasksTheyHold', () => {
      const el = setup([mk({ id: '1', status: 'ToDo' }), mk({ id: '2', status: 'ToDo' })]);

      expect(column(el, 'To do').querySelector('.count')!.textContent!.trim()).toBe('2');
      expect(column(el, 'Done').querySelector('.count')!.textContent!.trim()).toBe('0');
    });

    it('TaskList_emptyColumnWhileOthersHaveTasks_saysNoTasks', () => {
      const el = setup([mk({ id: '1', status: 'ToDo' })]);

      expect(column(el, 'Done').textContent).toContain('No tasks');
      expect(column(el, 'Done').textContent).not.toContain('add your first one');
    });

    it('TaskList_statusTab_choosesTheColumnShownOnPhones', () => {
      const fixture = setupFixture([mk({ id: '1', status: 'ToDo' })]);
      const el = fixture.nativeElement as HTMLElement;
      const tab = (label: string) =>
        Array.from(el.querySelectorAll<HTMLButtonElement>('[role=tab]')).find((t) => t.textContent!.includes(label))!;

      expect(tab('To do').getAttribute('aria-selected')).toBe('true');
      expect(column(el, 'To do').classList).toContain('active');

      tab('Done').click();
      fixture.detectChanges();

      expect(tab('Done').getAttribute('aria-selected')).toBe('true');
      expect(tab('To do').getAttribute('aria-selected')).toBe('false');
      expect(column(el, 'Done').classList).toContain('active');
      expect(column(el, 'To do').classList).not.toContain('active');
    });
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

  const button = (el: HTMLElement, label: string) => el.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`);

  it('TaskList: Delete asks to confirm and emits delete only on confirm', () => {
    const fixture = setupFixture([mk({ id: '1', title: 'A' })]);
    const el = fixture.nativeElement as HTMLElement;
    const deleted: Task[] = [];
    fixture.componentInstance.delete.subscribe((t) => deleted.push(t));

    button(el, 'Delete A')!.click();
    fixture.detectChanges();
    expect(el.textContent).toContain('Delete this task?');
    expect(deleted).toEqual([]);

    button(el, 'Confirm delete A')!.click();
    expect(deleted.map((t) => t.id)).toEqual(['1']);
  });

  it('TaskList: Keep cancels the confirmation without emitting', () => {
    const fixture = setupFixture([mk({ id: '1', title: 'A' })]);
    const el = fixture.nativeElement as HTMLElement;
    const deleted: Task[] = [];
    fixture.componentInstance.delete.subscribe((t) => deleted.push(t));

    button(el, 'Delete A')!.click();
    fixture.detectChanges();
    Array.from(el.querySelectorAll('button')).find((b) => b.textContent!.trim() === 'Keep')!.click();
    fixture.detectChanges();

    expect(deleted).toEqual([]);
    expect(el.textContent).not.toContain('Delete this task?');
    expect(button(el, 'Delete A')).not.toBeNull();
  });

  it('TaskList: the row being edited has Delete disabled', () => {
    const el = setupFixture([mk({ id: '1', title: 'A' })], '1').nativeElement as HTMLElement;

    expect(button(el, 'Delete A')!.disabled).toBe(true);
  });

  it('TaskList: focus moves to Confirm when asking and back to Delete on Keep', async () => {
    const fixture = setupFixture([mk({ id: '1', title: 'A' })]);
    const el = fixture.nativeElement as HTMLElement;
    document.body.appendChild(el);

    button(el, 'Delete A')!.click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.activeElement).toBe(button(el, 'Confirm delete A'));

    Array.from(el.querySelectorAll('button')).find((b) => b.textContent!.trim() === 'Keep')!.click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.activeElement).toBe(button(el, 'Delete A'));
    el.remove();
  });
});
