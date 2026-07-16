import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { MessageService, ConfirmationService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { AppUserForm } from './app-user-form';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUser, AppUserRequest } from '@core/models/app-user.model';
import { AppRoleLookup } from '@core/models/app-role.model';

describe('AppUserForm', () => {
  let fixture: ComponentFixture<AppUserForm>;
  let component: AppUserForm;
  let userService: jasmine.SpyObj<AppUserService>;
  let lookupService: jasmine.SpyObj<LookupService>;
  let messageService: jasmine.SpyObj<MessageService>;
  let router: Router;

  const roles: AppRoleLookup[] = [
    { roleId: 'Admin', roleName: 'Administrator' },
    { roleId: 'Editor', roleName: 'Editor' }
  ];

  const existingUser: AppUser = {
    pkid: 1,
    userId: 'miles@uuu.com.tw',
    userName: 'Miles Sun',
    isActive: true,
    passwordUpdatedTime: null,
    roleCount: 1,
    roleIds: ['Admin']
  };

  /** `id` param present => edit mode; null => new mode. */
  async function setup(routeId: string | null): Promise<void> {
    userService = jasmine.createSpyObj<AppUserService>('AppUserService', [
      'getById', 'create', 'update'
    ]);
    lookupService = jasmine.createSpyObj<LookupService>('LookupService', ['appRoles']);
    messageService = jasmine.createSpyObj<MessageService>('MessageService', ['add']);

    lookupService.appRoles.and.returnValue(of(roles));
    userService.getById.and.returnValue(of(existingUser));
    userService.create.and.returnValue(of(existingUser));
    userService.update.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppUserForm],
      providers: [
        provideNoopAnimations(),
        { provide: RowAuditService, useValue: { history: () => of([]) } },
        { provide: AppUserService, useValue: userService },
        { provide: LookupService, useValue: lookupService },
        { provide: MessageService, useValue: messageService },
        ConfirmationService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(routeId ? { id: routeId } : {}) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppUserForm);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  /** Reaches the protected form for assertions without loosening the component's API. */
  function form(): AppUserForm['form'] {
    return (component as unknown as { form: AppUserForm['form'] }).form;
  }

  function save(): void {
    (component as unknown as { save: () => void }).save();
  }

  describe('new mode', () => {
    beforeEach(async () => await setup(null));

    it('defaults 啟用 to true, matching the DB default', () => {
      expect(form().controls.isActive.value).toBeTrue();
    });

    it('leaves 使用者代碼 editable', () => {
      expect(form().controls.userId.enabled).toBeTrue();
    });

    it('does not fetch a user', () => {
      expect(userService.getById).not.toHaveBeenCalled();
    });

    it('is invalid until userId and userName are supplied', () => {
      expect(form().invalid).toBeTrue();

      form().patchValue({ userId: 'editor@uuu.com.tw', userName: 'Editor' });

      expect(form().valid).toBeTrue();
    });

    it('accepts an email userId but rejects one containing a slash (unsafe in a URL path)', () => {
      form().patchValue({ userId: 'miles@uuu.com.tw', userName: 'X' });
      expect(form().controls.userId.valid).toBeTrue();

      form().patchValue({ userId: 'bad/id', userName: 'X' });
      expect(form().controls.userId.hasError('pattern')).toBeTrue();
    });

    it('does not call the API when the form is invalid', () => {
      save();

      expect(userService.create).not.toHaveBeenCalled();
    });

    it('creates with the selected roleIds', () => {
      form().patchValue({
        userId: 'editor@uuu.com.tw',
        userName: 'Editor',
        isActive: false,
        roleIds: ['Admin']
      });

      save();

      const request = userService.create.calls.mostRecent().args[0] as AppUserRequest;
      expect(request.userId).toBe('editor@uuu.com.tw');
      expect(request.isActive).toBeFalse();
      expect(request.roleIds).toEqual(['Admin']);
      expect(router.navigate).toHaveBeenCalledWith(['/app-users', 'editor@uuu.com.tw']);
    });

    it('surfaces a 409 as a duplicate-code message and stays on the form', () => {
      userService.create.and.returnValue(
        throwError(() => new HttpErrorResponse({ status: 409 }))
      );
      form().patchValue({ userId: 'miles@uuu.com.tw', userName: 'Miles Sun' });

      save();

      expect(messageService.add).toHaveBeenCalledWith(
        jasmine.objectContaining({ severity: 'error', summary: '使用者代碼重複' })
      );
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => await setup('miles@uuu.com.tw'));

    it('loads the user by the route id', () => {
      expect(userService.getById).toHaveBeenCalledWith('miles@uuu.com.tw');
      expect(form().controls.userName.value).toBe('Miles Sun');
      expect(form().controls.roleIds.value).toEqual(['Admin']);
    });

    it('disables 使用者代碼 — the primary key is immutable', () => {
      expect(form().controls.userId.disabled).toBeTrue();
    });

    it('STILL sends userId in the update payload even though the control is disabled', () => {
      // Regression guard. form.value omits disabled controls, so a save built from `value`
      // would send userId: undefined and the PUT would 404. The component must use getRawValue().
      save();

      expect(userService.update).toHaveBeenCalled();
      const request = userService.update.calls.mostRecent().args[0] as AppUserRequest;
      expect(request.userId).toBe('miles@uuu.com.tw');
      expect(userService.create).not.toHaveBeenCalled();
    });

    it('navigates to the detail page after a successful save', () => {
      save();

      expect(router.navigate).toHaveBeenCalledWith(['/app-users', 'miles@uuu.com.tw']);
    });
  });
});
