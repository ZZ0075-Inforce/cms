import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { CourseDetail } from './course-detail';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { QrCodeService } from '@core/services/qr-code.service';
import { Course } from '@core/models/course.model';

describe('CourseDetail', () => {
  let fixture: ComponentFixture<CourseDetail>;
  let service: jasmine.SpyObj<CourseService>;
  let lookups: jasmine.SpyObj<LookupService>;
  let qr: jasmine.SpyObj<QrCodeService>;

  const course = {
    pkid: 1, title: 'Azure 基礎', courseId: 'AZ-900', prodCourseId: 'P-AZ', friendlyUrl: 'az',
    displayOrder: 10, partnerPkid: 3, partnerName: 'Microsoft',
    courseGroupPkid: 4, courseGroupName: '雲端', publishStatusPkid: 1, publishStatusName: '上架',
    scheduleOn: '2026-01-01', scheduleOff: '2036-01-01', hour: 21, listPrice: 15000,
    learningCredit: 3.5, canRepeat: true,
    certificationPkids: [5], jobCategoryPkids: [7]
  } as Course;

  async function setup(failWith?: number): Promise<void> {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['getById', 'remove']);
    lookups = jasmine.createSpyObj<LookupService>('LookupService', ['certifications', 'jobCategories']);
    qr = jasmine.createSpyObj<QrCodeService>('QrCodeService', ['toDataUrl']);

    service.getById.and.returnValue(
      failWith ? throwError(() => new HttpErrorResponse({ status: failWith })) : of(course));
    service.remove.and.returnValue(of(void 0));
    lookups.certifications.and.returnValue(of([{ pkid: 5, title: 'MCSA' }]));
    lookups.jobCategories.and.returnValue(of([{ pkid: 7, description: '系統工程師' }]));
    qr.toDataUrl.and.resolveTo('data:image/png;base64,AAAA');

    await TestBed.configureTestingModule({
      imports: [CourseDetail],
      providers: [
        provideNoopAnimations(),
        { provide: RowAuditService, useValue: { history: () => of([]) } },
        provideRouter([]),
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookups },
        { provide: QrCodeService, useValue: qr },
        MessageService,
        ConfirmationService,
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseDetail);
    fixture.detectChanges();
  }

  it('loads the course named by the route param, as a number', async () => {
    await setup();

    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders the scalar fields and resolves both N-N sets to labels', async () => {
    await setup();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Azure 基礎');
    expect(text).toContain('Microsoft');    // partner label
    expect(text).toContain('雲端');           // course group label
    expect(text).toContain('MCSA');          // certification chip (pkid 5 → label)
    expect(text).toContain('系統工程師');      // job category chip (pkid 7 → label)
  });

  it('shows a not-found state when the course is missing', async () => {
    await setup(404);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('找不到這個課程');
  });

  it('encodes the course page URL built from the record pkid and CourseId', async () => {
    await setup();
    await fixture.whenStable();

    // https://www.uuu.com.tw/Course/Show/{pkid}/{CourseId}
    expect(qr.toDataUrl).toHaveBeenCalledWith('https://www.uuu.com.tw/Course/Show/1/AZ-900');
  });

  it('shows CourseId as the QR code title inside 基本資料', async () => {
    await setup();

    const qrTitle: HTMLElement | null = fixture.nativeElement.querySelector('.qr-block .qr-title');
    expect(qrTitle).not.toBeNull();
    expect(qrTitle!.textContent).toContain('AZ-900');
  });
});
