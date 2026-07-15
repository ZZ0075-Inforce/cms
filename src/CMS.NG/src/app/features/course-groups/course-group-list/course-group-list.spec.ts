import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, Confirmation, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { CourseGroupList } from './course-group-list';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup, CourseGroupQuery } from '@core/models/course-group.model';

describe('CourseGroupList', () => {
  let fixture: ComponentFixture<CourseGroupList>;
  let component: CourseGroupList;
  let service: jasmine.SpyObj<CourseGroupService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let messageService: jasmine.SpyObj<MessageService>;
  let router: Router;

  // Referenced by identity in the delete tests, never by index: p-table's default sort (pkid DESC
  // here) reorders the bound array IN PLACE, so groups[0] would not survive a render.
  const cloud: CourseGroup = { pkid: 1, description: '雲端', courseCount: 7 };
  const security: CourseGroup = { pkid: 2, description: '資安', courseCount: 0 };
  const groups: CourseGroup[] = [cloud, security];

  async function setup(): Promise<void> {
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['query', 'remove']);
    confirmationService = jasmine.createSpyObj<ConfirmationService>('ConfirmationService', ['confirm']);
    messageService = jasmine.createSpyObj<MessageService>('MessageService', ['add']);

    // Fresh copy per call so in-place sorting doesn't accumulate across tests.
    service.query.and.returnValue(of([...groups]));
    service.remove.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [CourseGroupList],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: CourseGroupService, useValue: service },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: MessageService, useValue: messageService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseGroupList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  // Reach protected members for assertions without loosening the component's API.
  function internals() {
    return component as unknown as {
      draftFilters: CourseGroupQuery;
      appliedFilters: CourseGroupQuery;
      page: { first: number; rows: number };
      sort: { sortField: string; sortOrder: number };
      applyFilters: () => void;
      clearFilters: () => void;
      confirmDelete: (group: CourseGroup) => void;
      onSort: (event: { field?: string; order?: number }) => void;
      onPage: (event: { first?: number; rows?: number }) => void;
    };
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('loads groups on init and renders a row per group, including 對應課程數', async () => {
    await setup();

    expect(service.query).toHaveBeenCalled();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('雲端');
    expect(text).toContain('資安');
    expect(text).toContain('7');   // 對應課程數
  });

  it('queries with no filters by default', async () => {
    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: null });
  });

  it('defaults the sort to pkid descending (no DisplayOrder column)', async () => {
    await setup();

    expect(internals().sort).toEqual({ sortField: 'pkid', sortOrder: -1 });
  });

  it('applies drawer filters, normalising blanks to null, and resets to page 1', async () => {
    await setup();
    internals().page = { first: 40, rows: 20 };
    internals().draftFilters = { keyword: '  雲  ' };

    internals().applyFilters();

    expect(service.query).toHaveBeenCalledWith({ keyword: '雲' });
    expect(internals().page.first).toBe(0);
  });

  it('persists applied filters, sort and page to sessionStorage', async () => {
    await setup();
    internals().draftFilters = { keyword: '雲' };
    internals().applyFilters();

    internals().onSort({ field: 'description', order: 1 });
    internals().onPage({ first: 20, rows: 50 });

    expect(JSON.parse(sessionStorage.getItem('course-group-list-filters')!))
      .toEqual({ keyword: '雲' });
    expect(JSON.parse(sessionStorage.getItem('course-group-list-sort')!))
      .toEqual({ sortField: 'description', sortOrder: 1 });
    expect(JSON.parse(sessionStorage.getItem('course-group-list-page')!))
      .toEqual({ first: 20, rows: 50 });
  });

  it('restores filters, sort and page from sessionStorage on init', async () => {
    sessionStorage.setItem('course-group-list-filters', JSON.stringify({ keyword: 'saved' }));
    sessionStorage.setItem('course-group-list-sort',
      JSON.stringify({ sortField: 'description', sortOrder: 1 }));
    sessionStorage.setItem('course-group-list-page', JSON.stringify({ first: 20, rows: 50 }));

    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: 'saved' });
    expect(internals().sort).toEqual({ sortField: 'description', sortOrder: 1 });
    expect(internals().page).toEqual({ first: 20, rows: 50 });
  });

  it('survives a corrupt sessionStorage entry rather than throwing', async () => {
    sessionStorage.setItem('course-group-list-filters', '{not json');

    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: null });
  });

  it('clears filters back to empty', async () => {
    sessionStorage.setItem('course-group-list-filters', JSON.stringify({ keyword: 'saved' }));
    await setup();

    internals().clearFilters();

    expect(service.query).toHaveBeenCalledWith({ keyword: null });
  });

  it('deletes only after confirmation, then reloads the list', async () => {
    await setup();
    const callsBefore = service.query.calls.count();

    internals().confirmDelete(cloud);

    // Nothing happens until the user accepts.
    expect(service.remove).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent().args[0] as Confirmation;
    expect(confirmation.message).toContain('1');       // pkid in the confirm text
    expect(confirmation.message).toContain('雲端');
    confirmation.accept!();

    expect(service.remove).toHaveBeenCalledWith(1);
    expect(service.query.calls.count()).toBe(callsBefore + 1);
  });

  it('warns that N courses will be cascade-deleted when courseCount > 0', async () => {
    await setup();

    internals().confirmDelete(cloud);   // courseCount 7

    const confirmation = confirmationService.confirm.calls.mostRecent().args[0] as Confirmation;
    expect(confirmation.message).toContain('7');
    expect(confirmation.message).toContain('門課程');
  });

  it('omits the cascade warning when the group has no courses', async () => {
    await setup();

    internals().confirmDelete(security);   // courseCount 0

    const confirmation = confirmationService.confirm.calls.mostRecent().args[0] as Confirmation;
    expect(confirmation.message).not.toContain('門課程');
  });

  it('reports a 409 delete as 群組使用中, not a generic failure', async () => {
    await setup();
    service.remove.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));

    internals().confirmDelete(cloud);
    (confirmationService.confirm.calls.mostRecent().args[0] as Confirmation).accept!();

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', summary: '群組使用中' })
    );
  });
});
