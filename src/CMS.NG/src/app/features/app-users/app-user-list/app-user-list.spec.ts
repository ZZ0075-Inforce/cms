import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, Confirmation, MessageService } from 'primeng/api';
import { of } from 'rxjs';

import { AppUserList } from './app-user-list';
import { AppUserService } from '@core/services/app-user.service';
import { AppUser, AppUserQuery } from '@core/models/app-user.model';

describe('AppUserList', () => {
  let fixture: ComponentFixture<AppUserList>;
  let component: AppUserList;
  let service: jasmine.SpyObj<AppUserService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let router: Router;

  const users: AppUser[] = [
    {
      pkid: 1, userId: 'miles@uuu.com.tw', userName: 'Miles Sun', isActive: true,
      passwordUpdatedTime: null, roleCount: 3, roleIds: []
    },
    {
      pkid: 2, userId: 'helen', userName: 'Helen', isActive: false,
      passwordUpdatedTime: null, roleCount: 0, roleIds: []
    }
  ];

  async function setup(): Promise<void> {
    service = jasmine.createSpyObj<AppUserService>('AppUserService', ['query', 'remove']);
    confirmationService = jasmine.createSpyObj<ConfirmationService>('ConfirmationService', ['confirm']);

    service.query.and.returnValue(of(users));
    service.remove.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppUserList],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AppUserService, useValue: service },
        { provide: ConfirmationService, useValue: confirmationService },
        MessageService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppUserList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  // Reach protected members for assertions without loosening the component's API.
  function internals() {
    return component as unknown as {
      draftFilters: AppUserQuery;
      appliedFilters: AppUserQuery;
      page: { first: number; rows: number };
      sort: { sortField: string; sortOrder: number };
      applyFilters: () => void;
      clearFilters: () => void;
      confirmDelete: (user: AppUser) => void;
      onSort: (event: { field?: string; order?: number }) => void;
      onPage: (event: { first?: number; rows?: number }) => void;
    };
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('loads users on init and renders a row per user, including 角色數', async () => {
    await setup();

    expect(service.query).toHaveBeenCalled();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('miles@uuu.com.tw');
    expect(text).toContain('Miles Sun');
    expect(text).toContain('3');   // 角色數
  });

  it('queries with no filters by default', async () => {
    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: null, isActive: null });
  });

  it('applies drawer filters, normalising blanks to null, and resets to page 1', async () => {
    await setup();
    internals().page = { first: 40, rows: 20 };
    internals().draftFilters = { keyword: '  miles  ', isActive: false };

    internals().applyFilters();

    expect(service.query).toHaveBeenCalledWith({ keyword: 'miles', isActive: false });
    expect(internals().page.first).toBe(0);
  });

  it('persists applied filters, sort and page to sessionStorage', async () => {
    await setup();
    internals().draftFilters = { keyword: 'miles', isActive: true };
    internals().applyFilters();

    internals().onSort({ field: 'userName', order: -1 });
    internals().onPage({ first: 20, rows: 50 });

    expect(JSON.parse(sessionStorage.getItem('app-user-list-filters')!))
      .toEqual({ keyword: 'miles', isActive: true });
    expect(JSON.parse(sessionStorage.getItem('app-user-list-sort')!))
      .toEqual({ sortField: 'userName', sortOrder: -1 });
    expect(JSON.parse(sessionStorage.getItem('app-user-list-page')!))
      .toEqual({ first: 20, rows: 50 });
  });

  it('restores filters, sort and page from sessionStorage on init', async () => {
    sessionStorage.setItem('app-user-list-filters',
      JSON.stringify({ keyword: 'saved', isActive: false }));
    sessionStorage.setItem('app-user-list-sort',
      JSON.stringify({ sortField: 'userName', sortOrder: -1 }));
    sessionStorage.setItem('app-user-list-page',
      JSON.stringify({ first: 20, rows: 50 }));

    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: 'saved', isActive: false });
    expect(internals().sort).toEqual({ sortField: 'userName', sortOrder: -1 });
    expect(internals().page).toEqual({ first: 20, rows: 50 });
  });

  it('survives a corrupt sessionStorage entry rather than throwing', async () => {
    sessionStorage.setItem('app-user-list-filters', '{not json');

    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: null, isActive: null });
  });

  it('clears filters back to empty', async () => {
    sessionStorage.setItem('app-user-list-filters',
      JSON.stringify({ keyword: 'saved', isActive: true }));
    await setup();

    internals().clearFilters();

    expect(service.query).toHaveBeenCalledWith({ keyword: null, isActive: null });
  });

  it('deletes only after confirmation, then reloads the list', async () => {
    await setup();
    const callsBefore = service.query.calls.count();

    // Reference the record by identity, not index: p-table sorts its bound array in place, so after
    // the default userId-ASC render `users[0]` no longer points at this row.
    const target = users.find(u => u.userId === 'miles@uuu.com.tw')!;
    internals().confirmDelete(target);

    // Nothing happens until the user accepts.
    expect(service.remove).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent().args[0] as Confirmation;
    expect(confirmation.message).toContain(String(target.pkid));   // pkid in the confirm text
    expect(confirmation.message).toContain('miles@uuu.com.tw');
    confirmation.accept!();

    expect(service.remove).toHaveBeenCalledWith('miles@uuu.com.tw');
    expect(service.query.calls.count()).toBe(callsBefore + 1);
  });
});
