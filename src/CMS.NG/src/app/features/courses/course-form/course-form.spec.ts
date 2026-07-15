import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';

import { CourseForm } from './course-form';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, CourseRequest } from '@core/models/course.model';

describe('CourseForm', () => {
  let fixture: ComponentFixture<CourseForm>;
  let component: CourseForm;
  let service: jasmine.SpyObj<CourseService>;
  let lookups: jasmine.SpyObj<LookupService>;
  let router: Router;

  const existing = {
    pkid: 1, title: 'Azure 基礎', officialTitle: null, courseId: 'AZ-900', prodCourseId: 'P-AZ',
    friendlyUrl: 'az', displayOrder: 10, partnerPkid: 3, courseGroupPkid: 4, publishStatusPkid: 1,
    scheduleOn: '2026-01-01', scheduleOff: '2036-01-01', hour: 21, listPrice: 15000,
    learningCredit: 3.5, material: null, objective: null, target: null, prerequisites: null,
    outline: null, towardCertOrExam: null, note: null, otherInfo: null, canRepeat: true,
    partnerName: 'MS', courseGroupName: '雲端', publishStatusName: '上架',
    certificationPkids: [5], jobCategoryPkids: [7]
  } as Course;

  async function setup(routeId: string | null): Promise<void> {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['getById', 'create', 'update']);
    lookups = jasmine.createSpyObj<LookupService>('LookupService',
      ['partners', 'courseGroups', 'publishStatuses', 'certifications', 'jobCategories']);

    service.getById.and.returnValue(of(existing));
    service.create.and.returnValue(of({ ...existing, pkid: 42 }));
    service.update.and.returnValue(of(void 0));
    lookups.partners.and.returnValue(of([{ pkid: 3, name: 'MS' }]));
    lookups.courseGroups.and.returnValue(of([{ pkid: 4, description: '雲端' }]));
    lookups.publishStatuses.and.returnValue(of([{ pkid: 1, description: '上架' }]));
    lookups.certifications.and.returnValue(of([{ pkid: 5, title: 'MCSA' }]));
    lookups.jobCategories.and.returnValue(of([{ pkid: 7, description: '系統工程師' }]));

    await TestBed.configureTestingModule({
      imports: [CourseForm],
      providers: [
        provideNoopAnimations(),
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookups },
        MessageService,
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(routeId ? { id: routeId } : {}) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseForm);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  function form(): CourseForm['form'] {
    return (component as unknown as { form: CourseForm['form'] }).form;
  }

  function save(): void {
    (component as unknown as { save: () => void }).save();
  }

  const required = {
    title: 'Cisco CCNA',
    courseId: 'CCNA',
    prodCourseId: 'P-CCNA',
    friendlyUrl: 'ccna',
    partnerPkid: 3,
    publishStatusPkid: 1,
    scheduleOn: new Date(2026, 0, 14)   // 14 Jan 2026
  };

  /** The toolbar must stay pinned above the (potentially long) scrolling form body. */
  function expectStickyToolbar(): void {
    const header: HTMLElement | null = fixture.nativeElement.querySelector('.page-header');
    expect(header).withContext('page-header toolbar should be rendered').not.toBeNull();

    const style = getComputedStyle(header!);
    expect(style.position).toBe('sticky');
    expect(style.top).toBe('0px');

    const text = header!.textContent ?? '';
    expect(text).toContain('取消');
    expect(text).toContain('儲存');
  }

  describe('new mode', () => {
    beforeEach(async () => await setup(null));

    it('does not fetch a course', () => {
      expect(service.getById).not.toHaveBeenCalled();
    });

    it('pins the action toolbar to the top with Save/Cancel present', () => {
      expectStickyToolbar();
    });

    it('is invalid until every required field is supplied', () => {
      expect(form().invalid).toBeTrue();

      form().patchValue(required);

      expect(form().valid).toBeTrue();
    });

    it('auto-fills 下架日期 to 上架日期 + 10 年 when 上架日期 is picked', () => {
      form().controls.scheduleOn.setValue(new Date(2026, 0, 14));

      const off = form().controls.scheduleOff.value as Date;
      expect(off.getFullYear()).toBe(2036);
      expect(off.getMonth()).toBe(0);
      expect(off.getDate()).toBe(14);
    });

    it('does not call the API when the form is invalid', () => {
      save();

      expect(service.create).not.toHaveBeenCalled();
    });

    it('creates with pkid 0 and serialises dates as local yyyy-MM-dd', () => {
      form().patchValue(required);

      save();

      const request = service.create.calls.mostRecent().args[0] as CourseRequest;
      expect(request.pkid).toBe(0);
      expect(request.scheduleOn).toBe('2026-01-14');   // toIso, not UTC-shifted
      expect(request.scheduleOff).toBe('2036-01-14');  // auto-defaulted
    });

    it('navigates to the pkid returned by the API, not the one it sent', () => {
      form().patchValue(required);

      save();

      expect(router.navigate).toHaveBeenCalledWith(['/courses', 42]);
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => await setup('1'));

    it('loads the course and its N-N sets by the route id', () => {
      expect(service.getById).toHaveBeenCalledWith(1);
      expect(form().controls.title.value).toBe('Azure 基礎');
      expect(form().controls.certificationPkids.value).toEqual([5]);
      expect(form().controls.jobCategoryPkids.value).toEqual([7]);
    });

    it('pins the action toolbar to the top with Save/Cancel present', () => {
      expectStickyToolbar();
    });

    it('keeps the loaded 下架日期 rather than the +10 年 auto value', () => {
      // The auto-default fires on the scheduleOn patch, but the loaded scheduleOff patched right
      // after must win: 2036-01-01, not 10 years past 2026-01-01 computed some other way.
      const off = form().controls.scheduleOff.value as Date;
      expect(off.getFullYear()).toBe(2036);
      expect(off.getMonth()).toBe(0);
      expect(off.getDate()).toBe(1);
    });

    it('sends the loaded pkid in the update payload, built from getRawValue()', () => {
      save();

      expect(service.update).toHaveBeenCalled();
      const request = service.update.calls.mostRecent().args[0] as CourseRequest;
      expect(request.pkid).toBe(1);
      expect(request.title).toBe('Azure 基礎');
      expect(request.certificationPkids).toEqual([5]);
      expect(service.create).not.toHaveBeenCalled();
    });

    it('navigates to the detail page after a successful save', () => {
      save();

      expect(router.navigate).toHaveBeenCalledWith(['/courses', 1]);
    });
  });
});
