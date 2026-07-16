import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';

import { PublishStatusForm } from './publish-status-form';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus, PublishStatusRequest } from '@core/models/publish-status.model';

describe('PublishStatusForm', () => {
  let fixture: ComponentFixture<PublishStatusForm>;
  let component: PublishStatusForm;
  let service: jasmine.SpyObj<PublishStatusService>;
  let router: Router;

  const existing: PublishStatus = {
    pkid: 1,
    description: '草稿',
    isDraft: true,
    isPublished: false,
    isDiscontinued: false,
    courseCount: 3
  };

  /** `id` param present => edit mode; null => new mode. */
  async function setup(routeId: string | null): Promise<void> {
    service = jasmine.createSpyObj<PublishStatusService>(
      'PublishStatusService', ['getById', 'create', 'update']);

    service.getById.and.returnValue(of(existing));
    service.create.and.returnValue(of({ ...existing, pkid: 9, description: '已上架' }));
    service.update.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PublishStatusForm],
      providers: [
        provideNoopAnimations(),
        { provide: RowAuditService, useValue: { history: () => of([]) } },
        { provide: PublishStatusService, useValue: service },
        MessageService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(routeId ? { id: routeId } : {}) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusForm);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  /** Reaches the protected form for assertions without loosening the component's API. */
  function form(): PublishStatusForm['form'] {
    return (component as unknown as { form: PublishStatusForm['form'] }).form;
  }

  function save(): void {
    (component as unknown as { save: () => void }).save();
  }

  const validValues = {
    pkid: 9,
    description: '已上架',
    isDraft: false,
    isPublished: true,
    isDiscontinued: false
  };

  describe('new mode', () => {
    beforeEach(async () => await setup(null));

    it('does not fetch a status', () => {
      expect(service.getById).not.toHaveBeenCalled();
    });

    it('is invalid until 主代碼 and 狀態說明 are supplied', () => {
      form().patchValue({ pkid: 9, description: '' });
      expect(form().invalid).toBeTrue();

      form().patchValue(validValues);

      expect(form().valid).toBeTrue();
    });

    it('does not call the API when the form is invalid', () => {
      form().patchValue({ description: '' });

      save();

      expect(service.create).not.toHaveBeenCalled();
    });

    it('creates with the client-supplied pkid and trims the description', () => {
      form().patchValue({ ...validValues, description: '  已上架  ' });

      save();

      const request = service.create.calls.mostRecent().args[0] as PublishStatusRequest;
      expect(request.pkid).toBe(9);          // pkid is chosen by the user, not the DB
      expect(request.description).toBe('已上架');
      expect(request.isPublished).toBeTrue();
    });

    it('navigates to the created status detail page', () => {
      form().patchValue(validValues);

      save();

      expect(router.navigate).toHaveBeenCalledWith(['/publish-statuses', 9]);
    });

    it('leaves 主代碼 editable in new mode', () => {
      expect(form().controls.pkid.enabled).toBeTrue();
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => await setup('1'));

    it('loads the status by the route id', () => {
      expect(service.getById).toHaveBeenCalledWith(1);
      expect(form().controls.description.value).toBe('草稿');
      expect(form().controls.isDraft.value).toBeTrue();
    });

    it('disables 主代碼 — it is the immutable primary key', () => {
      expect(form().controls.pkid.disabled).toBeTrue();
    });

    it('sends the loaded pkid in the update payload, built from getRawValue()', () => {
      // Regression guard: form.value omits disabled controls. Because pkid is disabled in edit mode,
      // the component MUST use getRawValue() or the key would drop out of the PUT and it would 404.
      save();

      expect(service.update).toHaveBeenCalled();
      const request = service.update.calls.mostRecent().args[0] as PublishStatusRequest;
      expect(request.pkid).toBe(1);
      expect(request.description).toBe('草稿');
      expect(service.create).not.toHaveBeenCalled();
    });

    it('navigates to the detail page after a successful save', () => {
      save();

      expect(router.navigate).toHaveBeenCalledWith(['/publish-statuses', 1]);
    });
  });
});
