import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';

import { RowAuditBadge } from './row-audit-badge';
import { RowAuditService } from '@core/services/row-audit.service';
import { RowAuditEntry } from '@core/models/row-audit.model';

describe('RowAuditBadge', () => {
  let fixture: ComponentFixture<RowAuditBadge>;
  let service: jasmine.SpyObj<RowAuditService>;

  const entries: RowAuditEntry[] = [
    // Newest first, as the endpoint returns them.
    { dateTime: '2026-06-04T14:30:00', userName: 'alice', actionType: 'Update', actionDesc: 'Title,Description' },
    { dateTime: '2026-06-01T09:00:00', userName: 'bob', actionType: 'Insert', actionDesc: '原始標題' }
  ];

  async function setup(rows: RowAuditEntry[] = entries): Promise<void> {
    service = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['history']);
    service.history.and.returnValue(of(rows));

    await TestBed.configureTestingModule({
      imports: [RowAuditBadge],
      providers: [provideNoopAnimations(), { provide: RowAuditService, useValue: service }]
    }).compileComponents();

    fixture = TestBed.createComponent(RowAuditBadge);
    // Attach to the document so the p-dialog content is queryable wherever PrimeNG renders it.
    document.body.appendChild(fixture.nativeElement);
    fixture.componentRef.setInput('tableName', 'Course');
    fixture.componentRef.setInput('pkid', 123);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => fixture?.destroy());

  function openDialog(): void {
    (fixture.nativeElement.querySelector('.audit-badge') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  it('fetches this record\'s history with its tableName and pkid', async () => {
    await setup();
    expect(service.history).toHaveBeenCalledWith('Course', 123);
  });

  it('shows the most recent entry inline on the badge', async () => {
    await setup();

    const text = (fixture.nativeElement.querySelector('.audit-badge__sub') as HTMLElement).textContent!;
    expect(text).toContain('Update');
    expect(text).toContain('alice');
    expect(text).toContain('2026-06-04 14:30');   // the newest entry
    expect(text).not.toContain('bob');            // not the older one
  });

  it('opens a dialog listing the full trail, newest first', async () => {
    await setup();

    openDialog();

    const items = document.querySelectorAll('.audit-trail__item');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('alice');   // newest first
    expect(items[1].textContent).toContain('bob');
    // every row surfaces the four fields
    expect(items[0].textContent).toContain('Update');
    expect(items[0].textContent).toContain('Title,Description');
    expect(items[1].textContent).toContain('Insert');
  });

  it('renders the "no history" empty state when there is none', async () => {
    await setup([]);

    // Inline on the badge…
    const sub = fixture.nativeElement.querySelector('.audit-badge__sub') as HTMLElement;
    expect(sub.textContent).toContain('No history');

    // …and inside the dialog.
    openDialog();
    const empty = document.querySelector('.audit-trail__state--empty');
    expect(empty).not.toBeNull();
    expect(empty!.textContent).toContain('No history yet');
  });
});
