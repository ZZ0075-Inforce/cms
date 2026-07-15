import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, Confirmation, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { CourseList } from './course-list';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, EMPTY_COURSE_QUERY } from '@core/models/course.model';

describe('CourseList', () => {
  let fixture: ComponentFixture<CourseList>;
  let component: CourseList;
  let service: jasmine.SpyObj<CourseService>;
  let lookups: jasmine.SpyObj<LookupService>;
  let confirmationService: jasmine.SpyObj<ConfirmationService>;
  let messageService: jasmine.SpyObj<MessageService>;
  let router: Router;

  const azure = {
    pkid: 1, title: 'Azure 基礎', courseId: 'AZ-900', prodCourseId: 'P-AZ', displayOrder: 10,
    partnerName: 'Microsoft', courseGroupName: '雲端', publishStatusName: '上架',
    scheduleOn: '2026-01-01', scheduleOff: '2036-01-01', hour: 21, listPrice: 15000,
    learningCredit: 3.5, canRepeat: true
  } as Course;
  const ccna = {
    pkid: 2, title: 'CCNA', courseId: 'CCNA', prodCourseId: 'P-CC', displayOrder: 20,
    partnerName: 'Cisco', courseGroupName: null, publishStatusName: '草稿',
    scheduleOn: '2026-02-01', scheduleOff: '2036-02-01', hour: 35, listPrice: 30000,
    learningCredit: 5, canRepeat: false
  } as Course;
  const courses: Course[] = [azure, ccna];

  // What getById returns for row 1 — a full record incl. the N-N sets absent from list rows.
  const azureFull = {
    ...azure, officialTitle: 'Azure Fundamentals', friendlyUrl: 'az-900',
    partnerPkid: 3, courseGroupPkid: 4, publishStatusPkid: 1,
    material: null, objective: null, target: null, prerequisites: null, outline: null,
    towardCertOrExam: null, note: null, otherInfo: null,
    certificationPkids: [5], jobCategoryPkids: [7]
  } as Course;

  async function setup(): Promise<void> {
    service = jasmine.createSpyObj<CourseService>('CourseService',
      ['query', 'remove', 'getById', 'update']);
    lookups = jasmine.createSpyObj<LookupService>('LookupService',
      ['partners', 'courseGroups', 'publishStatuses']);
    confirmationService = jasmine.createSpyObj<ConfirmationService>('ConfirmationService', ['confirm']);
    messageService = jasmine.createSpyObj<MessageService>('MessageService', ['add']);

    // Fresh row copies each run, so inline-edit mutations never leak between tests.
    service.query.and.returnValue(of([{ ...azure }, { ...ccna }]));
    service.remove.and.returnValue(of(void 0));
    service.getById.and.returnValue(of({ ...azureFull }));
    service.update.and.returnValue(of(void 0));
    lookups.partners.and.returnValue(of([]));
    lookups.courseGroups.and.returnValue(of([]));
    lookups.publishStatuses.and.returnValue(of([{ pkid: 1, description: '上架' }, { pkid: 2, description: '草稿' }]));

    await TestBed.configureTestingModule({
      imports: [CourseList],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookups },
        { provide: ConfirmationService, useValue: confirmationService },
        { provide: MessageService, useValue: messageService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();
  }

  function internals() {
    return component as unknown as {
      draftFilters: Record<string, unknown>;
      page: { first: number; rows: number };
      sort: { sortField: string; sortOrder: number };
      applyFilters: () => void;
      confirmDelete: (course: Course) => void;
    };
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('loads courses on init and renders the JOIN labels', async () => {
    await setup();

    expect(service.query).toHaveBeenCalled();
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Microsoft');   // partnerName
    expect(text).toContain('草稿');          // publishStatusName
  });

  it('queries with no filters by default', async () => {
    await setup();

    expect(service.query).toHaveBeenCalledWith(EMPTY_COURSE_QUERY);
  });

  it('defaults the sort to 顯示順序', async () => {
    await setup();

    expect(internals().sort).toEqual({ sortField: 'displayOrder', sortOrder: 1 });
  });

  it('serialises a date-range filter to a local ISO string and resets to page 1', async () => {
    await setup();
    internals().page = { first: 40, rows: 20 };
    internals().draftFilters = {
      ...internals().draftFilters,
      partnerPkid: 7,
      scheduleOnFrom: new Date(2026, 0, 1)   // 1 Jan 2026 local — must serialise as 2026-01-01
    };

    internals().applyFilters();

    expect(service.query).toHaveBeenCalledWith(
      jasmine.objectContaining({ partnerPkid: 7, scheduleOnFrom: '2026-01-01' }));
    expect(internals().page.first).toBe(0);
  });

  it('persists applied filters and restores them on init', async () => {
    sessionStorage.setItem('course-list-filters',
      JSON.stringify({ ...EMPTY_COURSE_QUERY, keyword: 'azure', publishStatusPkid: 2 }));

    await setup();

    expect(service.query).toHaveBeenCalledWith(
      jasmine.objectContaining({ keyword: 'azure', publishStatusPkid: 2 }));
  });

  it('survives a corrupt sessionStorage entry rather than throwing', async () => {
    sessionStorage.setItem('course-list-filters', '{not json');

    await setup();

    expect(service.query).toHaveBeenCalledWith(EMPTY_COURSE_QUERY);
  });

  it('deletes only after confirmation, then reloads the list', async () => {
    await setup();
    const callsBefore = service.query.calls.count();

    internals().confirmDelete(azure);
    expect(service.remove).not.toHaveBeenCalled();

    const confirmation = confirmationService.confirm.calls.mostRecent().args[0] as Confirmation;
    expect(confirmation.message).toContain('AZ-900');   // courseId in the confirm text
    confirmation.accept!();

    expect(service.remove).toHaveBeenCalledWith(1);
    expect(service.query.calls.count()).toBe(callsBefore + 1);
  });

  it('reports a 409 delete as 課程使用中, not a generic failure', async () => {
    await setup();
    service.remove.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));

    internals().confirmDelete(azure);
    (confirmationService.confirm.calls.mostRecent().args[0] as Confirmation).accept!();

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', summary: '課程使用中' }));
  });

  // ---------- inline cell editing ----------

  interface InlineEdit {
    courses: () => Course[];
    editing: () => { pkid: number; field: string } | null;
    editError: () => string | null;
    editDraft: unknown;
    startEdit: (course: Course, field: string) => void;
    commitEdit: (course: Course, field: string) => void;
    isEditing: (course: Course, field: string) => boolean;
  }
  const ie = () => component as unknown as InlineEdit;

  // Column order: 0 pkid, 1 顯示順序, 2 簡介代碼, 3 科目代碼, 4 課程名稱, 5 原廠,
  //               6 課程群組, 7 上架狀態, 8 上架日期, 9 下架日期, 10 時數, 11 定價, 12 點數, 13 允許重聽.
  const cells = (row: number): HTMLTableCellElement[] =>
    Array.from(fixture.nativeElement.querySelectorAll('tbody tr')[row].querySelectorAll('td'));

  it('enters edit mode on double-click, but not on single-click', async () => {
    await setup();
    const titleCell = cells(0)[4];

    titleCell.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    fixture.detectChanges();
    expect(ie().editing()).toBeNull();
    expect(titleCell.querySelector('input')).toBeNull();

    titleCell.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    fixture.detectChanges();
    expect(ie().editing()).toEqual({ pkid: 1, field: 'title' });
    expect(titleCell.querySelector('input')).not.toBeNull();
  });

  it('does not edit the read-only columns (主代碼 / 原廠 / 課程群組)', async () => {
    await setup();
    [cells(0)[0], cells(0)[5], cells(0)[6]].forEach(c =>
      c.dispatchEvent(new MouseEvent('dblclick', { bubbles: true })));
    fixture.detectChanges();
    expect(ie().editing()).toBeNull();

    // A direct call with a non-editable field is guarded too.
    ie().startEdit(ie().courses()[0], 'pkid');
    expect(ie().editing()).toBeNull();
  });

  it('persists on blur via getById + update, preserving the N-N sets, then updates the row', async () => {
    await setup();
    const row = ie().courses()[0];
    ie().startEdit(row, 'title');
    ie().editDraft = 'Azure 進階';
    ie().commitEdit(row, 'title');

    expect(service.getById).toHaveBeenCalledWith(1);
    const req = service.update.calls.mostRecent().args[0];
    expect(req.pkid).toBe(1);
    expect(req.title).toBe('Azure 進階');
    expect(req.certificationPkids).toEqual([5]);   // preserved, not wiped
    expect(req.jobCategoryPkids).toEqual([7]);
    expect(ie().editing()).toBeNull();              // exits on success
    expect(ie().courses()[0].title).toBe('Azure 進階');
  });

  it('blocks clearing a required field and keeps the cell in edit mode', async () => {
    await setup();
    const row = ie().courses()[0];
    ie().startEdit(row, 'title');
    ie().editDraft = '   ';
    ie().commitEdit(row, 'title');

    expect(service.update).not.toHaveBeenCalled();
    expect(ie().editError()).toBe('此欄位為必填');
    expect(ie().isEditing(row, 'title')).toBeTrue();
  });

  it('blocks a negative number and a non-numeric value', async () => {
    await setup();
    const row = ie().courses()[0];

    ie().startEdit(row, 'hour');
    ie().editDraft = -5;
    ie().commitEdit(row, 'hour');
    expect(service.update).not.toHaveBeenCalled();
    expect(ie().editError()).toBe('請輸入有效的非負數字');
    expect(ie().isEditing(row, 'hour')).toBeTrue();

    ie().editDraft = null;
    ie().commitEdit(row, 'hour');
    expect(service.update).not.toHaveBeenCalled();
  });

  it('blocks an invalid date', async () => {
    await setup();
    const row = ie().courses()[0];
    ie().startEdit(row, 'scheduleOn');
    ie().editDraft = new Date('not a date');
    ie().commitEdit(row, 'scheduleOn');

    expect(service.update).not.toHaveBeenCalled();
    expect(ie().editError()).toBe('請輸入有效的日期');
  });

  it('blocks 上架日期 later than 下架日期, and accepts an in-range date', async () => {
    await setup();
    const row = ie().courses()[0];   // scheduleOff = 2036-01-01

    ie().startEdit(row, 'scheduleOn');
    ie().editDraft = new Date(2037, 0, 1);   // after 下架日期
    ie().commitEdit(row, 'scheduleOn');
    expect(service.update).not.toHaveBeenCalled();
    expect(ie().editError()).toBe('上架日期不可晚於下架日期');

    ie().editDraft = new Date(2026, 5, 1);   // 2026-06-01, within range
    ie().commitEdit(row, 'scheduleOn');
    expect(service.update).toHaveBeenCalled();
    expect(service.update.calls.mostRecent().args[0].scheduleOn).toBe('2026-06-01');
  });

  it('reverts the row and surfaces an error when the save fails', async () => {
    await setup();
    service.update.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400 })));
    const row = ie().courses()[0];
    const original = row.title;

    ie().startEdit(row, 'title');
    ie().editDraft = 'X';
    ie().commitEdit(row, 'title');

    expect(ie().courses()[0].title).toBe(original);   // reverted — never mutated
    expect(ie().editing()).toBeNull();                 // edit mode exited
    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', summary: '儲存失敗' }));
  });
});
