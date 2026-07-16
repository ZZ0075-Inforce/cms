import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

import { CoursePrint } from './course-print';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { QrCodeService } from '@core/services/qr-code.service';
import { Course } from '@core/models/course.model';

describe('CoursePrint', () => {
  let fixture: ComponentFixture<CoursePrint>;
  let service: jasmine.SpyObj<CourseService>;
  let lookups: jasmine.SpyObj<LookupService>;
  let qr: jasmine.SpyObj<QrCodeService>;

  const course = {
    pkid: 1, title: 'Azure 基礎', officialTitle: 'Microsoft Azure Fundamentals',
    courseId: 'AZ-900', prodCourseId: 'P-AZ-INTERNAL', friendlyUrl: 'azure-fundamentals',
    displayOrder: 10, partnerPkid: 3, partnerName: 'Microsoft',
    courseGroupPkid: 4, courseGroupName: '雲端', publishStatusPkid: 1, publishStatusName: '上架',
    scheduleOn: '2026-01-01', scheduleOff: '2036-01-01', hour: 21, listPrice: 15000,
    learningCredit: 3.5, canRepeat: true,
    material: '原廠教材', objective: '理解雲端基礎', target: '初學者',
    prerequisites: '無', outline: '第一章\n第二章', towardCertOrExam: 'AZ-900 認證',
    note: '內部備註：講師難約', otherInfo: '自備筆電',
    certificationPkids: [5], jobCategoryPkids: [7]
  } as Course;

  async function setup(overrides: Partial<Course> = {}, failWith?: number): Promise<void> {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['getById']);
    lookups = jasmine.createSpyObj<LookupService>('LookupService', ['certifications', 'jobCategories']);
    qr = jasmine.createSpyObj<QrCodeService>('QrCodeService', ['toDataUrl']);

    service.getById.and.returnValue(
      failWith
        ? throwError(() => new HttpErrorResponse({ status: failWith }))
        : of({ ...course, ...overrides }));
    lookups.certifications.and.returnValue(of([{ pkid: 5, title: 'MCSA' }]));
    lookups.jobCategories.and.returnValue(of([{ pkid: 7, description: '系統工程師' }]));
    qr.toDataUrl.and.resolveTo('data:image/png;base64,AAAA');

    await TestBed.configureTestingModule({
      imports: [CoursePrint],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookups },
        { provide: QrCodeService, useValue: qr },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CoursePrint);
    fixture.detectChanges();
  }

  function text(): string {
    return fixture.nativeElement.textContent as string;
  }

  it('loads the course named by the route param, as a number', async () => {
    await setup();

    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders the fields a customer buys on, and resolves both N-N sets to labels', async () => {
    await setup();

    expect(text()).toContain('Azure 基礎');                      // title, as the sheet heading
    expect(text()).toContain('Microsoft Azure Fundamentals');    // official title
    expect(text()).toContain('AZ-900');                          // course code — public, customers quote it
    expect(text()).toContain('Microsoft');                       // partner
    expect(text()).toContain('雲端');                             // course group
    expect(text()).toContain('21 小時');
    expect(text()).toContain('15,000');                          // price, thousands-separated
    expect(text()).toContain('理解雲端基礎');                      // objective
    expect(text()).toContain('第一章');                           // outline
    expect(text()).toContain('MCSA');                            // certification label
    expect(text()).toContain('系統工程師');                        // job category label
  });

  // The whole point of the view: these must be impossible to leak onto a customer document.
  describe('internal fields', () => {
    it('never renders the record pkid or its 主代碼 label', async () => {
      await setup();

      expect(text()).not.toContain('主代碼');
      expect(text()).not.toContain('顯示順序');
    });

    it('never renders the internal product code, friendly URL, or publish workflow state', async () => {
      await setup();

      expect(text()).not.toContain('P-AZ-INTERNAL');    // prodCourseId 科目代碼
      expect(text()).not.toContain('azure-fundamentals'); // friendlyUrl
      expect(text()).not.toContain('上架狀態');
      expect(text()).not.toContain('下架日期');
    });

    it('never renders 備註 — an admin note field is not customer-safe', async () => {
      await setup();

      expect(text()).not.toContain('內部備註：講師難約');
      expect(text()).not.toContain('備註');
    });

    it('carries no Row Audit badge and no edit/delete controls', async () => {
      await setup();

      expect(fixture.nativeElement.querySelector('app-row-audit-badge')).toBeNull();
      expect(text()).not.toContain('刪除');
      expect(text()).not.toContain('編輯');
    });
  });

  describe('empty fields', () => {
    it('drops the whole row rather than printing a dash', async () => {
      await setup({ officialTitle: null, courseGroupName: null, outline: '   ' });

      expect(text()).not.toContain('官方課程名稱');
      expect(text()).not.toContain('課程群組');
      expect(text()).not.toContain('課程大綱');
      expect(text()).not.toContain('—');
    });

    it('still renders the fields that do have a value', async () => {
      await setup({ officialTitle: null });

      expect(text()).not.toContain('官方課程名稱');
      expect(text()).toContain('AZ-900');
    });

    it('omits a section entirely when its N-N set is empty', async () => {
      await setup({ certificationPkids: [], jobCategoryPkids: [] });

      expect(text()).not.toContain('相關認證');
      expect(text()).not.toContain('適合職務');
    });
  });

  it('prints on demand rather than on load, so the sheet is never printed half-rendered', async () => {
    const print = spyOn(window, 'print');
    await setup();

    expect(print).not.toHaveBeenCalled();

    // Each <button> is the only one inside its own <p-button>, so `button:last-of-type` would
    // match both. Scope to the last p-button (列印／另存 PDF) instead.
    const button: HTMLButtonElement =
      fixture.nativeElement.querySelector('.toolbar p-button:last-of-type button');
    button.click();

    expect(print).toHaveBeenCalledTimes(1);
  });

  it('points the QR code at the public course page, not at this admin route', async () => {
    await setup();
    await fixture.whenStable();

    expect(qr.toDataUrl).toHaveBeenCalledWith('https://www.uuu.com.tw/Course/Show/1/AZ-900');
  });

  it('shows a not-found state when the course is missing', async () => {
    await setup({}, 404);

    expect(text()).toContain('找不到這個課程');
  });

  it('shows a not-found state for a non-numeric route id', async () => {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['getById']);
    lookups = jasmine.createSpyObj<LookupService>('LookupService', ['certifications', 'jobCategories']);
    qr = jasmine.createSpyObj<QrCodeService>('QrCodeService', ['toDataUrl']);

    await TestBed.configureTestingModule({
      imports: [CoursePrint],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookups },
        { provide: QrCodeService, useValue: qr },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'abc' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CoursePrint);
    fixture.detectChanges();

    expect(text()).toContain('找不到這個課程');
    expect(service.getById).not.toHaveBeenCalled();
  });
});
