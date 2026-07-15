import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';

import { CourseGroupForm } from './course-group-form';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup, CourseGroupRequest } from '@core/models/course-group.model';

describe('CourseGroupForm', () => {
  let fixture: ComponentFixture<CourseGroupForm>;
  let component: CourseGroupForm;
  let service: jasmine.SpyObj<CourseGroupService>;
  let router: Router;

  const existing: CourseGroup = { pkid: 1, description: '雲端', courseCount: 7 };

  /** `id` param present => edit mode; null => new mode. */
  async function setup(routeId: string | null): Promise<void> {
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['getById', 'create', 'update']);

    service.getById.and.returnValue(of(existing));
    service.create.and.returnValue(of({ ...existing, pkid: 42, description: '資安' }));
    service.update.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [CourseGroupForm],
      providers: [
        provideNoopAnimations(),
        { provide: CourseGroupService, useValue: service },
        MessageService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(routeId ? { id: routeId } : {}) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseGroupForm);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  /** Reaches the protected form for assertions without loosening the component's API. */
  function form(): CourseGroupForm['form'] {
    return (component as unknown as { form: CourseGroupForm['form'] }).form;
  }

  function save(): void {
    (component as unknown as { save: () => void }).save();
  }

  describe('new mode', () => {
    beforeEach(async () => await setup(null));

    it('does not fetch a group', () => {
      expect(service.getById).not.toHaveBeenCalled();
    });

    it('is invalid until 群組名稱 is supplied', () => {
      expect(form().invalid).toBeTrue();

      form().patchValue({ description: '資安' });

      expect(form().valid).toBeTrue();
    });

    it('does not call the API when the form is invalid', () => {
      save();

      expect(service.create).not.toHaveBeenCalled();
    });

    it('creates with pkid 0 — the DB assigns the IDENTITY', () => {
      form().patchValue({ description: '資安' });

      save();

      const request = service.create.calls.mostRecent().args[0] as CourseGroupRequest;
      expect(request.pkid).toBe(0);
      expect(request.description).toBe('資安');
    });

    it('navigates to the pkid returned by the API, not the one it sent', () => {
      // On create the request carries pkid 0 — only the response knows the real one.
      form().patchValue({ description: '資安' });

      save();

      expect(router.navigate).toHaveBeenCalledWith(['/course-groups', 42]);
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => await setup('1'));

    it('loads the group by the route id', () => {
      expect(service.getById).toHaveBeenCalledWith(1);
      expect(form().controls.description.value).toBe('雲端');
    });

    it('sends the loaded pkid in the update payload, built from getRawValue()', () => {
      save();

      expect(service.update).toHaveBeenCalled();
      const request = service.update.calls.mostRecent().args[0] as CourseGroupRequest;
      expect(request.pkid).toBe(1);
      expect(request.description).toBe('雲端');
      expect(service.create).not.toHaveBeenCalled();
    });

    it('navigates to the detail page after a successful save', () => {
      save();

      expect(router.navigate).toHaveBeenCalledWith(['/course-groups', 1]);
    });
  });
});
