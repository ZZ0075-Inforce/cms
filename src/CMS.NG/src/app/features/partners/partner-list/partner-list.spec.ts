import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, Confirmation, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { PartnerList } from './partner-list';
import { PartnerService } from '@core/services/partner.service';
import { Partner, PartnerQuery } from '@core/models/partner.model';

describe('PartnerList', () => {
  let fixture: ComponentFixture<PartnerList>;
  let component: PartnerList;
  let service: jasmine.SpyObj<PartnerService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let messageService: jasmine.SpyObj<MessageService>;
  let router: Router;

  const partners: Partner[] = [
    {
      pkid: 1, name: 'Microsoft', appKey: 'MS', nameOnPartnerMenu: 'Microsoft 微軟',
      nameOnCourseDetailPage: '微軟', displayOrder: 10, imageFilename: 'ms.png', courseCount: 7
    },
    {
      pkid: 2, name: 'Cisco', appKey: 'CSCO', nameOnPartnerMenu: 'Cisco 思科',
      nameOnCourseDetailPage: '思科', displayOrder: 20, imageFilename: null, courseCount: 3
    }
  ];

  async function setup(): Promise<void> {
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['query', 'remove']);
    confirmationService = jasmine.createSpyObj<ConfirmationService>('ConfirmationService', ['confirm']);
    messageService = jasmine.createSpyObj<MessageService>('MessageService', ['add']);

    service.query.and.returnValue(of(partners));
    service.remove.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PartnerList],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PartnerService, useValue: service },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: MessageService, useValue: messageService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  // Reach protected members for assertions without loosening the component's API.
  function internals() {
    return component as unknown as {
      draftFilters: PartnerQuery;
      appliedFilters: PartnerQuery;
      page: { first: number; rows: number };
      sort: { sortField: string; sortOrder: number };
      applyFilters: () => void;
      clearFilters: () => void;
      confirmDelete: (partner: Partner) => void;
      onSort: (event: { field?: string; order?: number }) => void;
      onPage: (event: { first?: number; rows?: number }) => void;
    };
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('loads partners on init and renders a row per partner, including 對應課程數', async () => {
    await setup();

    expect(service.query).toHaveBeenCalled();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Microsoft');
    expect(text).toContain('CSCO');
    expect(text).toContain('微軟');
    expect(text).toContain('7');   // 對應課程數
  });

  it('queries with no filters by default', async () => {
    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: null });
  });

  it('defaults the sort to 顯示順序', async () => {
    await setup();

    expect(internals().sort).toEqual({ sortField: 'displayOrder', sortOrder: 1 });
  });

  it('applies drawer filters, normalising blanks to null, and resets to page 1', async () => {
    await setup();
    internals().page = { first: 40, rows: 20 };
    internals().draftFilters = { keyword: '  micro  ' };

    internals().applyFilters();

    expect(service.query).toHaveBeenCalledWith({ keyword: 'micro' });
    expect(internals().page.first).toBe(0);
  });

  it('persists applied filters, sort and page to sessionStorage', async () => {
    await setup();
    internals().draftFilters = { keyword: 'micro' };
    internals().applyFilters();

    internals().onSort({ field: 'name', order: -1 });
    internals().onPage({ first: 20, rows: 50 });

    expect(JSON.parse(sessionStorage.getItem('partner-list-filters')!))
      .toEqual({ keyword: 'micro' });
    expect(JSON.parse(sessionStorage.getItem('partner-list-sort')!))
      .toEqual({ sortField: 'name', sortOrder: -1 });
    expect(JSON.parse(sessionStorage.getItem('partner-list-page')!))
      .toEqual({ first: 20, rows: 50 });
  });

  it('restores filters, sort and page from sessionStorage on init', async () => {
    sessionStorage.setItem('partner-list-filters', JSON.stringify({ keyword: 'saved' }));
    sessionStorage.setItem('partner-list-sort',
      JSON.stringify({ sortField: 'name', sortOrder: -1 }));
    sessionStorage.setItem('partner-list-page', JSON.stringify({ first: 20, rows: 50 }));

    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: 'saved' });
    expect(internals().sort).toEqual({ sortField: 'name', sortOrder: -1 });
    expect(internals().page).toEqual({ first: 20, rows: 50 });
  });

  it('survives a corrupt sessionStorage entry rather than throwing', async () => {
    sessionStorage.setItem('partner-list-filters', '{not json');

    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: null });
  });

  it('clears filters back to empty', async () => {
    sessionStorage.setItem('partner-list-filters', JSON.stringify({ keyword: 'saved' }));
    await setup();

    internals().clearFilters();

    expect(service.query).toHaveBeenCalledWith({ keyword: null });
  });

  it('deletes only after confirmation, then reloads the list', async () => {
    await setup();
    const callsBefore = service.query.calls.count();

    internals().confirmDelete(partners[0]);

    // Nothing happens until the user accepts.
    expect(service.remove).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent().args[0] as Confirmation;
    expect(confirmation.message).toContain('1');           // pkid in the confirm text
    expect(confirmation.message).toContain('Microsoft');
    confirmation.accept!();

    expect(service.remove).toHaveBeenCalledWith(1);
    expect(service.query.calls.count()).toBe(callsBefore + 1);
  });

  it('reports a 409 delete as 廠商使用中, not a generic failure', async () => {
    await setup();
    service.remove.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));

    internals().confirmDelete(partners[0]);
    (confirmationService.confirm.calls.mostRecent().args[0] as Confirmation).accept!();

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', summary: '廠商使用中' })
    );
  });
});
