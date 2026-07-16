import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { AppRoleDetail } from './app-role-detail';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole } from '@core/models/app-role.model';
import { AppUserLookup } from '@core/models/app-user.model';

describe('AppRoleDetail', () => {
  let fixture: ComponentFixture<AppRoleDetail>;
  let roleService: jasmine.SpyObj<AppRoleService>;
  let lookupService: jasmine.SpyObj<LookupService>;

  const users: AppUserLookup[] = [
    { userId: 'miles@uuu.com.tw', userName: 'Miles Sun', isActive: true }
  ];

  const role: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 1,
    userIds: ['miles@uuu.com.tw']
  };

  async function setup(failWith?: number): Promise<void> {
    roleService = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['getById', 'remove']);
    lookupService = jasmine.createSpyObj<LookupService>('LookupService', ['appUsers']);

    lookupService.appUsers.and.returnValue(of(users));
    roleService.getById.and.returnValue(
      failWith
        ? throwError(() => new HttpErrorResponse({ status: failWith }))
        : of(role)
    );
    roleService.remove.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppRoleDetail],
      providers: [
        provideNoopAnimations(),
        { provide: RowAuditService, useValue: { history: () => of([]) } },
        provideRouter([]),
        { provide: AppRoleService, useValue: roleService },
        { provide: LookupService, useValue: lookupService },
        MessageService,
        ConfirmationService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'Admin' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleDetail);
    fixture.detectChanges();
  }

  it('loads the role named by the route param', async () => {
    await setup();

    expect(roleService.getById).toHaveBeenCalledWith('Admin');
  });

  it('renders the role fields', async () => {
    await setup();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Admin');
    expect(text).toContain('Administrator');
    expect(text).toContain('系統管理員');
  });

  it('renders assigned users as "UserName (UserId)" chips, not raw ids', async () => {
    await setup();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Miles Sun (miles@uuu.com.tw)');
  });

  it('shows a not-found state when the role is missing', async () => {
    await setup(404);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('找不到這個角色');
  });
});
