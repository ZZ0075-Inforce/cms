import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { ConfirmationService, Confirmation, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { AppUserDetail } from './app-user-detail';
import { AppUserService } from '@core/services/app-user.service';
import { AuthService } from '@core/services/auth.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUser } from '@core/models/app-user.model';
import { AppRoleLookup } from '@core/models/app-role.model';

describe('AppUserDetail', () => {
  let fixture: ComponentFixture<AppUserDetail>;
  let component: AppUserDetail;
  let userService: jasmine.SpyObj<AppUserService>;
  let lookupService: jasmine.SpyObj<LookupService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;

  const roles: AppRoleLookup[] = [
    { roleId: 'Admin', roleName: 'Administrator' }
  ];

  const user: AppUser = {
    pkid: 1,
    userId: 'miles@uuu.com.tw',
    userName: 'Miles Sun',
    isActive: true,
    passwordUpdatedTime: null,
    roleCount: 1,
    roleIds: ['Admin']
  };

  // The signed-in caller's roles drive the Admin-only 重設密碼 button; default to Admin so the
  // existing behaviour tests still see it. Pass [] for a non-Admin caller.
  async function setup(failWith?: number, signedInRoles: string[] = ['Admin']): Promise<void> {
    userService = jasmine.createSpyObj<AppUserService>('AppUserService',
      ['getById', 'remove', 'resetPassword']);
    lookupService = jasmine.createSpyObj<LookupService>('LookupService', ['appRoles']);
    confirmationService = jasmine.createSpyObj<ConfirmationService>('ConfirmationService', ['confirm']);

    lookupService.appRoles.and.returnValue(of(roles));
    userService.getById.and.returnValue(
      failWith
        ? throwError(() => new HttpErrorResponse({ status: failWith }))
        : of(user)
    );
    userService.remove.and.returnValue(of(void 0));
    userService.resetPassword.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppUserDetail],
      providers: [
        provideNoopAnimations(),
        { provide: RowAuditService, useValue: { history: () => of([]) } },
        provideRouter([]),
        { provide: AppUserService, useValue: userService },
        { provide: AuthService, useValue: { roles: () => signedInRoles } },
        { provide: LookupService, useValue: lookupService },
        { provide: ConfirmationService, useValue: confirmationService },
        MessageService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'miles@uuu.com.tw' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppUserDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  function resetPassword(): void {
    (component as unknown as { confirmResetPassword: () => void }).confirmResetPassword();
  }

  it('loads the user named by the route param', async () => {
    await setup();

    expect(userService.getById).toHaveBeenCalledWith('miles@uuu.com.tw');
  });

  it('renders the user fields', async () => {
    await setup();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('miles@uuu.com.tw');
    expect(text).toContain('Miles Sun');
  });

  it('renders assigned roles as "RoleName (RoleId)" chips, not raw ids', async () => {
    await setup();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Administrator (Admin)');
  });

  it('resets the password only after confirmation, and never sends a password value', async () => {
    await setup();

    resetPassword();
    expect(userService.resetPassword).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent().args[0] as Confirmation;
    expect(confirmation.message).toContain('miles@uuu.com.tw');
    confirmation.accept!();

    expect(userService.resetPassword).toHaveBeenCalledWith('miles@uuu.com.tw');
    // resetPassword takes only the id — no password argument exists on the call.
    expect(userService.resetPassword.calls.mostRecent().args.length).toBe(1);
  });

  it('shows a not-found state when the user is missing', async () => {
    await setup(404);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('找不到這個使用者');
  });

  it('renders the 重設密碼 button for an Admin caller', async () => {
    await setup(undefined, ['Admin']);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('重設密碼');
  });

  it('hides the 重設密碼 button from a non-Admin caller', async () => {
    await setup(undefined, ['User']);

    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('重設密碼');
  });
});
