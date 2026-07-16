import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { PublishStatusDetail } from './publish-status-detail';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus } from '@core/models/publish-status.model';

describe('PublishStatusDetail', () => {
  let fixture: ComponentFixture<PublishStatusDetail>;
  let service: jasmine.SpyObj<PublishStatusService>;

  const status: PublishStatus = {
    pkid: 1,
    description: '草稿',
    isDraft: true,
    isPublished: false,
    isDiscontinued: false,
    courseCount: 3
  };

  async function setup(failWith?: number): Promise<void> {
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['getById', 'remove']);

    service.getById.and.returnValue(
      failWith
        ? throwError(() => new HttpErrorResponse({ status: failWith }))
        : of(status)
    );
    service.remove.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PublishStatusDetail],
      providers: [
        provideNoopAnimations(),
        { provide: RowAuditService, useValue: { history: () => of([]) } },
        provideRouter([]),
        { provide: PublishStatusService, useValue: service },
        MessageService,
        ConfirmationService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusDetail);
    fixture.detectChanges();
  }

  it('loads the status named by the route param, as a number', async () => {
    await setup();

    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders every status field', async () => {
    await setup();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('草稿');
    expect(text).toContain('3');   // 對應課程數
  });

  it('shows a not-found state when the status is missing', async () => {
    await setup(404);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('找不到這個上架狀態');
  });
});
