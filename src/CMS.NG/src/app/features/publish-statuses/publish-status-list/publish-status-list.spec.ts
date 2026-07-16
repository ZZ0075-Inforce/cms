import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, Confirmation, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { PublishStatusList } from './publish-status-list';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus, PublishStatusQuery } from '@core/models/publish-status.model';

describe('PublishStatusList', () => {
  let fixture: ComponentFixture<PublishStatusList>;
  let component: PublishStatusList;
  let service: jasmine.SpyObj<PublishStatusService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let messageService: jasmine.SpyObj<MessageService>;
  let router: Router;

  const statuses: PublishStatus[] = [
    {
      pkid: 1, description: '草稿', isDraft: true, isPublished: false,
      isDiscontinued: false, courseCount: 3
    },
    {
      pkid: 2, description: '已上架', isDraft: false, isPublished: true,
      isDiscontinued: false, courseCount: 7
    }
  ];

  async function setup(): Promise<void> {
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['query', 'remove']);
    confirmationService = jasmine.createSpyObj<ConfirmationService>('ConfirmationService', ['confirm']);
    messageService = jasmine.createSpyObj<MessageService>('MessageService', ['add']);

    service.query.and.returnValue(of(statuses));
    service.remove.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PublishStatusList],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PublishStatusService, useValue: service },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: MessageService, useValue: messageService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  // Reach protected members for assertions without loosening the component's API.
  function internals() {
    return component as unknown as {
      draftFilters: PublishStatusQuery;
      appliedFilters: PublishStatusQuery;
      page: { first: number; rows: number };
      sort: { sortField: string; sortOrder: number };
      applyFilters: () => void;
      clearFilters: () => void;
      confirmDelete: (status: PublishStatus) => void;
      onSort: (event: { field?: string; order?: number }) => void;
      onPage: (event: { first?: number; rows?: number }) => void;
    };
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('loads statuses on init and renders a row per status, including 對應課程數', async () => {
    await setup();

    expect(service.query).toHaveBeenCalled();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('草稿');
    expect(text).toContain('已上架');
    expect(text).toContain('7');   // 對應課程數
  });

  it('queries with no filters by default', async () => {
    await setup();

    expect(service.query).toHaveBeenCalledWith(
      { keyword: null, isDraft: null, isPublished: null, isDiscontinued: null });
  });

  it('defaults the sort to 主代碼 (pkid) ascending', async () => {
    await setup();

    expect(internals().sort).toEqual({ sortField: 'pkid', sortOrder: 1 });
  });

  it('applies drawer filters, normalising blanks to null, and resets to page 1', async () => {
    await setup();
    internals().page = { first: 40, rows: 20 };
    internals().draftFilters = {
      keyword: '  上架  ', isDraft: null, isPublished: true, isDiscontinued: null
    };

    internals().applyFilters();

    expect(service.query).toHaveBeenCalledWith(
      { keyword: '上架', isDraft: null, isPublished: true, isDiscontinued: null });
    expect(internals().page.first).toBe(0);
  });

  it('persists applied filters, sort and page to sessionStorage', async () => {
    await setup();
    internals().draftFilters = {
      keyword: '上架', isDraft: null, isPublished: true, isDiscontinued: null
    };
    internals().applyFilters();

    internals().onSort({ field: 'description', order: -1 });
    internals().onPage({ first: 20, rows: 50 });

    expect(JSON.parse(sessionStorage.getItem('publish-status-list-filters')!))
      .toEqual({ keyword: '上架', isDraft: null, isPublished: true, isDiscontinued: null });
    expect(JSON.parse(sessionStorage.getItem('publish-status-list-sort')!))
      .toEqual({ sortField: 'description', sortOrder: -1 });
    expect(JSON.parse(sessionStorage.getItem('publish-status-list-page')!))
      .toEqual({ first: 20, rows: 50 });
  });

  it('restores filters, sort and page from sessionStorage on init', async () => {
    sessionStorage.setItem('publish-status-list-filters',
      JSON.stringify({ keyword: 'saved', isDraft: true, isPublished: null, isDiscontinued: null }));
    sessionStorage.setItem('publish-status-list-sort',
      JSON.stringify({ sortField: 'description', sortOrder: -1 }));
    sessionStorage.setItem('publish-status-list-page', JSON.stringify({ first: 20, rows: 50 }));

    await setup();

    expect(service.query).toHaveBeenCalledWith(
      { keyword: 'saved', isDraft: true, isPublished: null, isDiscontinued: null });
    expect(internals().sort).toEqual({ sortField: 'description', sortOrder: -1 });
    expect(internals().page).toEqual({ first: 20, rows: 50 });
  });

  it('survives a corrupt sessionStorage entry rather than throwing', async () => {
    sessionStorage.setItem('publish-status-list-filters', '{not json');

    await setup();

    expect(service.query).toHaveBeenCalledWith(
      { keyword: null, isDraft: null, isPublished: null, isDiscontinued: null });
  });

  it('deletes only after confirmation, then reloads the list', async () => {
    await setup();
    const callsBefore = service.query.calls.count();

    internals().confirmDelete(statuses[0]);

    // Nothing happens until the user accepts.
    expect(service.remove).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent().args[0] as Confirmation;
    expect(confirmation.message).toContain('1');       // pkid in the confirm text
    expect(confirmation.message).toContain('草稿');
    confirmation.accept!();

    expect(service.remove).toHaveBeenCalledWith(1);
    expect(service.query.calls.count()).toBe(callsBefore + 1);
  });

  it('reports a 409 delete as 上架狀態使用中, not a generic failure', async () => {
    await setup();
    service.remove.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));

    internals().confirmDelete(statuses[0]);
    (confirmationService.confirm.calls.mostRecent().args[0] as Confirmation).accept!();

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', summary: '上架狀態使用中' })
    );
  });
});
