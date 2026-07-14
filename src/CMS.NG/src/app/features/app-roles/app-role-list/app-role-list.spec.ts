import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, Confirmation, MessageService } from 'primeng/api';
import { of } from 'rxjs';

import { AppRoleList } from './app-role-list';
import { AppRoleService } from '@core/services/app-role.service';
import { AppRole, AppRoleQuery } from '@core/models/app-role.model';

describe('AppRoleList', () => {
  let fixture: ComponentFixture<AppRoleList>;
  let component: AppRoleList;
  let service: jasmine.SpyObj<AppRoleService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let router: Router;

  const roles: AppRole[] = [
    {
      pkid: 1, roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1,
      description: '系統管理員', userCount: 3, userIds: []
    },
    {
      pkid: 2, roleId: 'User', roleName: 'User', permissionLevel: 100,
      description: '一般使用者', userCount: 9, userIds: []
    }
  ];

  async function setup(): Promise<void> {
    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['query', 'remove']);
    confirmationService = jasmine.createSpyObj<ConfirmationService>('ConfirmationService', ['confirm']);

    service.query.and.returnValue(of(roles));
    service.remove.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppRoleList],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AppRoleService, useValue: service },
        { provide: ConfirmationService, useValue: confirmationService },
        MessageService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  // Reach protected members for assertions without loosening the component's API.
  function internals() {
    return component as unknown as {
      draftFilters: AppRoleQuery;
      appliedFilters: AppRoleQuery;
      page: { first: number; rows: number };
      sort: { sortField: string; sortOrder: number };
      applyFilters: () => void;
      clearFilters: () => void;
      confirmDelete: (role: AppRole) => void;
      onSort: (event: { field?: string; order?: number }) => void;
      onPage: (event: { first?: number; rows?: number }) => void;
    };
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('loads roles on init and renders a row per role, including 使用者數', async () => {
    await setup();

    expect(service.query).toHaveBeenCalled();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Admin');
    expect(text).toContain('Administrator');
    expect(text).toContain('系統管理員');
    expect(text).toContain('3');   // 使用者數
    expect(text).toContain('9');
  });

  it('queries with no filters by default', async () => {
    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: null, permissionLevel: null });
  });

  it('applies drawer filters, normalising blanks to null, and resets to page 1', async () => {
    await setup();
    internals().page = { first: 40, rows: 20 };
    internals().draftFilters = { keyword: '  admin  ', permissionLevel: 1 };

    internals().applyFilters();

    expect(service.query).toHaveBeenCalledWith({ keyword: 'admin', permissionLevel: 1 });
    expect(internals().page.first).toBe(0);
  });

  it('persists applied filters, sort and page to sessionStorage', async () => {
    await setup();
    internals().draftFilters = { keyword: 'admin', permissionLevel: null };
    internals().applyFilters();

    internals().onSort({ field: 'roleName', order: -1 });
    internals().onPage({ first: 20, rows: 50 });

    expect(JSON.parse(sessionStorage.getItem('app-role-list-filters')!))
      .toEqual({ keyword: 'admin', permissionLevel: null });
    expect(JSON.parse(sessionStorage.getItem('app-role-list-sort')!))
      .toEqual({ sortField: 'roleName', sortOrder: -1 });
    expect(JSON.parse(sessionStorage.getItem('app-role-list-page')!))
      .toEqual({ first: 20, rows: 50 });
  });

  it('restores filters, sort and page from sessionStorage on init', async () => {
    sessionStorage.setItem('app-role-list-filters',
      JSON.stringify({ keyword: 'saved', permissionLevel: 5 }));
    sessionStorage.setItem('app-role-list-sort',
      JSON.stringify({ sortField: 'permissionLevel', sortOrder: -1 }));
    sessionStorage.setItem('app-role-list-page',
      JSON.stringify({ first: 20, rows: 50 }));

    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: 'saved', permissionLevel: 5 });
    expect(internals().sort).toEqual({ sortField: 'permissionLevel', sortOrder: -1 });
    expect(internals().page).toEqual({ first: 20, rows: 50 });
  });

  it('survives a corrupt sessionStorage entry rather than throwing', async () => {
    sessionStorage.setItem('app-role-list-filters', '{not json');

    await setup();

    expect(service.query).toHaveBeenCalledWith({ keyword: null, permissionLevel: null });
  });

  it('clears filters back to empty', async () => {
    sessionStorage.setItem('app-role-list-filters',
      JSON.stringify({ keyword: 'saved', permissionLevel: 5 }));
    await setup();

    internals().clearFilters();

    expect(service.query).toHaveBeenCalledWith({ keyword: null, permissionLevel: null });
  });

  it('deletes only after confirmation, then reloads the list', async () => {
    await setup();
    const callsBefore = service.query.calls.count();

    internals().confirmDelete(roles[0]);

    // Nothing happens until the user accepts.
    expect(service.remove).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent().args[0] as Confirmation;
    expect(confirmation.message).toContain('1');       // pkid in the confirm text
    expect(confirmation.message).toContain('Admin');
    confirmation.accept!();

    expect(service.remove).toHaveBeenCalledWith('Admin');
    expect(service.query.calls.count()).toBe(callsBefore + 1);
  });
});
