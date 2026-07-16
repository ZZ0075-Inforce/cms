import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';

import { PartnerForm } from './partner-form';
import { PartnerService } from '@core/services/partner.service';
import { Partner, PartnerRequest } from '@core/models/partner.model';

describe('PartnerForm', () => {
  let fixture: ComponentFixture<PartnerForm>;
  let component: PartnerForm;
  let service: jasmine.SpyObj<PartnerService>;
  let router: Router;

  const existing: Partner = {
    pkid: 1,
    name: 'Microsoft',
    appKey: 'MS',
    nameOnPartnerMenu: 'Microsoft 微軟',
    nameOnCourseDetailPage: '微軟',
    displayOrder: 10,
    imageFilename: 'ms.png',
    courseCount: 7
  };

  /** `id` param present => edit mode; null => new mode. */
  async function setup(routeId: string | null): Promise<void> {
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['getById', 'create', 'update']);

    service.getById.and.returnValue(of(existing));
    service.create.and.returnValue(of({ ...existing, pkid: 42, name: 'Cisco' }));
    service.update.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PartnerForm],
      providers: [
        provideNoopAnimations(),
        { provide: RowAuditService, useValue: { history: () => of([]) } },
        { provide: PartnerService, useValue: service },
        MessageService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(routeId ? { id: routeId } : {}) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerForm);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  /** Reaches the protected form for assertions without loosening the component's API. */
  function form(): PartnerForm['form'] {
    return (component as unknown as { form: PartnerForm['form'] }).form;
  }

  function save(): void {
    (component as unknown as { save: () => void }).save();
  }

  const validValues = {
    name: 'Cisco',
    appKey: 'CSCO',
    nameOnPartnerMenu: 'Cisco 思科',
    nameOnCourseDetailPage: '思科',
    displayOrder: 20
  };

  describe('new mode', () => {
    beforeEach(async () => await setup(null));

    it('does not fetch a partner', () => {
      expect(service.getById).not.toHaveBeenCalled();
    });

    it('is invalid until every required field is supplied', () => {
      expect(form().invalid).toBeTrue();

      form().patchValue(validValues);

      expect(form().valid).toBeTrue();
    });

    it('treats 圖片檔名 as optional', () => {
      form().patchValue(validValues);

      expect(form().controls.imageFilename.valid).toBeTrue();
      expect(form().valid).toBeTrue();
    });

    it('does not call the API when the form is invalid', () => {
      save();

      expect(service.create).not.toHaveBeenCalled();
    });

    it('creates with pkid 0 and normalises a blank 圖片檔名 to null', () => {
      form().patchValue({ ...validValues, imageFilename: '   ' });

      save();

      const request = service.create.calls.mostRecent().args[0] as PartnerRequest;
      expect(request.pkid).toBe(0);          // the DB assigns the IDENTITY
      expect(request.name).toBe('Cisco');
      expect(request.imageFilename).toBeNull();
    });

    it('navigates to the pkid returned by the API, not the one it sent', () => {
      // On create the request carries pkid 0 — only the response knows the real one.
      form().patchValue(validValues);

      save();

      expect(router.navigate).toHaveBeenCalledWith(['/partners', 42]);
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => await setup('1'));

    it('loads the partner by the route id', () => {
      expect(service.getById).toHaveBeenCalledWith(1);
      expect(form().controls.name.value).toBe('Microsoft');
      expect(form().controls.appKey.value).toBe('MS');
      expect(form().controls.displayOrder.value).toBe(10);
    });

    it('leaves 廠商代碼 editable — AppKey is not a key and nothing FKs to it', () => {
      expect(form().controls.appKey.enabled).toBeTrue();
    });

    it('sends the loaded pkid in the update payload, built from getRawValue()', () => {
      // Regression guard: form.value omits disabled controls. The component must use
      // getRawValue() so a field disabled later cannot silently drop out of the PUT.
      save();

      expect(service.update).toHaveBeenCalled();
      const request = service.update.calls.mostRecent().args[0] as PartnerRequest;
      expect(request.pkid).toBe(1);
      expect(request.name).toBe('Microsoft');
      expect(service.create).not.toHaveBeenCalled();
    });

    it('navigates to the detail page after a successful save', () => {
      save();

      expect(router.navigate).toHaveBeenCalledWith(['/partners', 1]);
    });
  });
});
