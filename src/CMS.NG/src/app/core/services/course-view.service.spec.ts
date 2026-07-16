import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';

import { CourseViewService } from './course-view.service';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course } from '@core/models/course.model';

describe('CourseViewService', () => {
  let sut: CourseViewService;
  let courses: jasmine.SpyObj<CourseService>;
  let lookups: jasmine.SpyObj<LookupService>;

  const course = {
    pkid: 1, title: 'Azure 基礎', courseId: 'AZ-900',
    certificationPkids: [5], jobCategoryPkids: [7]
  } as Course;

  beforeEach(() => {
    courses = jasmine.createSpyObj<CourseService>('CourseService', ['getById']);
    lookups = jasmine.createSpyObj<LookupService>('LookupService', ['certifications', 'jobCategories']);

    courses.getById.and.returnValue(of(course));
    lookups.certifications.and.returnValue(of([{ pkid: 5, title: 'MCSA' }]));
    lookups.jobCategories.and.returnValue(of([{ pkid: 7, description: '系統工程師' }]));

    TestBed.configureTestingModule({
      providers: [
        { provide: CourseService, useValue: courses },
        { provide: LookupService, useValue: lookups }
      ]
    });
    sut = TestBed.inject(CourseViewService);
  });

  it('resolves both N-N sets to labels alongside the course', done => {
    sut.load(1).subscribe(view => {
      expect(courses.getById).toHaveBeenCalledWith(1);
      expect(view.course).toBe(course);
      expect(view.certificationLabels).toEqual(['MCSA']);
      expect(view.jobCategoryLabels).toEqual(['系統工程師']);
      done();
    });
  });

  // The course is the payload; prettier labels are not worth taking the page down for.
  it('degrades labels to raw ids when a lookup fails, rather than failing the load', done => {
    lookups.certifications.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

    sut.load(1).subscribe({
      next: view => {
        expect(view.course).toBe(course);
        expect(view.certificationLabels).toEqual(['#5']);
        expect(view.jobCategoryLabels).toEqual(['系統工程師']);
        done();
      },
      error: () => done.fail('a failing lookup must not fail the whole load')
    });
  });

  it('propagates a getById failure so the caller can show its not-found state', done => {
    courses.getById.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));

    sut.load(404).subscribe({
      next: () => done.fail('expected the load to fail'),
      error: (error: HttpErrorResponse) => {
        expect(error.status).toBe(404);
        done();
      }
    });
  });
});
