import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { MessageService, ConfirmationService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { AppRoleForm } from './app-role-form';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole, AppRoleRequest } from '@core/models/app-role.model';
import { AppUserLookup } from '@core/models/app-user.model';

describe('AppRoleForm', () => {
  let fixture: ComponentFixture<AppRoleForm>;
  let component: AppRoleForm;
  let roleService: jasmine.SpyObj<AppRoleService>;
  let lookupService: jasmine.SpyObj<LookupService>;
  let messageService: jasmine.SpyObj<MessageService>;
  let router: Router;

  const users: AppUserLookup[] = [
    { userId: 'miles@uuu.com.tw', userName: 'Miles Sun', isActive: true },
    { userId: 'helen', userName: 'helen', isActive: true }
  ];

  const existingRole: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 2,
    userIds: ['helen']
  };

  /** `id` param present => edit mode; null => new mode. */
  async function setup(routeId: string | null): Promise<void> {
    roleService = jasmine.createSpyObj<AppRoleService>('AppRoleService', [
      'getById', 'create', 'update'
    ]);
    lookupService = jasmine.createSpyObj<LookupService>('LookupService', ['appUsers']);
    messageService = jasmine.createSpyObj<MessageService>('MessageService', ['add']);

    lookupService.appUsers.and.returnValue(of(users));
    roleService.getById.and.returnValue(of(existingRole));
    roleService.create.and.returnValue(of(existingRole));
    roleService.update.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppRoleForm],
      providers: [
        provideNoopAnimations(),
        { provide: RowAuditService, useValue: { history: () => of([]) } },
        { provide: AppRoleService, useValue: roleService },
        { provide: LookupService, useValue: lookupService },
        { provide: MessageService, useValue: messageService },
        ConfirmationService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(routeId ? { id: routeId } : {}) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleForm);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  /** Reaches the protected form for assertions without loosening the component's API. */
  function form(): AppRoleForm['form'] {
    return (component as unknown as { form: AppRoleForm['form'] }).form;
  }

  function save(): void {
    (component as unknown as { save: () => void }).save();
  }

  describe('new mode', () => {
    beforeEach(async () => await setup(null));

    it('defaults 權限等級 to 100, matching the DB default', () => {
      expect(form().controls.permissionLevel.value).toBe(100);
    });

    it('leaves 角色代碼 editable', () => {
      expect(form().controls.roleId.enabled).toBeTrue();
    });

    it('does not fetch a role', () => {
      expect(roleService.getById).not.toHaveBeenCalled();
    });

    it('is invalid until roleId and roleName are supplied', () => {
      expect(form().invalid).toBeTrue();

      form().patchValue({ roleId: 'Editor', roleName: 'Editor' });

      expect(form().valid).toBeTrue();
    });

    it('rejects a roleId with characters that are unsafe in a URL path', () => {
      form().patchValue({ roleId: 'bad/id', roleName: 'X' });

      expect(form().controls.roleId.hasError('pattern')).toBeTrue();
    });

    it('does not call the API when the form is invalid', () => {
      save();

      expect(roleService.create).not.toHaveBeenCalled();
    });

    it('creates with the selected userIds and normalises a blank description to null', () => {
      form().patchValue({
        roleId: 'Editor',
        roleName: 'Editor',
        description: '   ',
        userIds: ['helen']
      });

      save();

      const request = roleService.create.calls.mostRecent().args[0] as AppRoleRequest;
      expect(request.roleId).toBe('Editor');
      expect(request.userIds).toEqual(['helen']);
      expect(request.description).toBeNull();
      expect(router.navigate).toHaveBeenCalledWith(['/app-roles', 'Editor']);
    });

    it('surfaces a 409 as a duplicate-code message and stays on the form', () => {
      roleService.create.and.returnValue(
        throwError(() => new HttpErrorResponse({ status: 409 }))
      );
      form().patchValue({ roleId: 'Admin', roleName: 'Administrator' });

      save();

      expect(messageService.add).toHaveBeenCalledWith(
        jasmine.objectContaining({ severity: 'error', summary: '角色代碼重複' })
      );
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => await setup('Admin'));

    it('loads the role by the route id', () => {
      expect(roleService.getById).toHaveBeenCalledWith('Admin');
      expect(form().controls.roleName.value).toBe('Administrator');
      expect(form().controls.permissionLevel.value).toBe(1);
      expect(form().controls.userIds.value).toEqual(['helen']);
    });

    it('disables 角色代碼 — the primary key is immutable', () => {
      expect(form().controls.roleId.disabled).toBeTrue();
    });

    it('STILL sends roleId in the update payload even though the control is disabled', () => {
      // Regression guard. form.value omits disabled controls, so a save built from `value`
      // would send roleId: undefined and the PUT would 404. The component must use
      // getRawValue(). This is the single most likely bug in the feature.
      save();

      expect(roleService.update).toHaveBeenCalled();
      const request = roleService.update.calls.mostRecent().args[0] as AppRoleRequest;
      expect(request.roleId).toBe('Admin');
      expect(roleService.create).not.toHaveBeenCalled();
    });

    it('navigates to the detail page after a successful save', () => {
      save();

      expect(router.navigate).toHaveBeenCalledWith(['/app-roles', 'Admin']);
    });
  });
});
