import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { CourseGroupDetail } from './course-group-detail';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup } from '@core/models/course-group.model';

describe('CourseGroupDetail', () => {
  let fixture: ComponentFixture<CourseGroupDetail>;
  let service: jasmine.SpyObj<CourseGroupService>;

  const group: CourseGroup = { pkid: 1, description: '雲端', courseCount: 7 };

  async function setup(failWith?: number): Promise<void> {
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['getById', 'remove']);

    service.getById.and.returnValue(
      failWith
        ? throwError(() => new HttpErrorResponse({ status: failWith }))
        : of(group)
    );
    service.remove.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [CourseGroupDetail],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: CourseGroupService, useValue: service },
        MessageService,
        ConfirmationService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseGroupDetail);
    fixture.detectChanges();
  }

  it('loads the group named by the route param, as a number', async () => {
    await setup();

    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders every group field', async () => {
    await setup();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('雲端');
    expect(text).toContain('7');   // 對應課程數
  });

  it('shows a not-found state when the group is missing', async () => {
    await setup(404);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('找不到這個課程群組');
  });
});
