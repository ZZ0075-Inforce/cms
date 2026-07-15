import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { QrCode } from './qr-code';
import { QrCodeService } from '@core/services/qr-code.service';

describe('QrCode', () => {
  let fixture: ComponentFixture<QrCode>;
  let component: QrCode;
  let service: jasmine.SpyObj<QrCodeService>;

  const PNG = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=';

  interface Probe { download: () => void; }
  const probe = () => component as unknown as Probe;

  async function setup(value: string, title: string | null = null, fileName = 'qrcode'): Promise<void> {
    service = jasmine.createSpyObj<QrCodeService>('QrCodeService', ['toDataUrl']);
    service.toDataUrl.and.resolveTo(PNG);

    await TestBed.configureTestingModule({
      imports: [QrCode],
      providers: [provideNoopAnimations(), { provide: QrCodeService, useValue: service }]
    }).compileComponents();

    fixture = TestBed.createComponent(QrCode);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('title', title);
    fixture.componentRef.setInput('fileName', fileName);
    fixture.detectChanges();       // runs the encode effect
    await fixture.whenStable();     // let the async encode resolve
    fixture.detectChanges();        // render the resulting <img>
  }

  it('encodes the value it is given', async () => {
    await setup('https://www.uuu.com.tw/Course/Show/1/AZ-900');

    expect(service.toDataUrl).toHaveBeenCalledWith('https://www.uuu.com.tw/Course/Show/1/AZ-900');
  });

  it('re-encodes when the value changes', async () => {
    await setup('first');

    fixture.componentRef.setInput('value', 'second');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(service.toDataUrl).toHaveBeenCalledWith('second');
  });

  it('shows the title and the generated image', async () => {
    await setup('X', 'AZ-900');

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('AZ-900');

    const img: HTMLImageElement | null = fixture.nativeElement.querySelector('img.qr-image');
    expect(img).not.toBeNull();
    expect(img!.getAttribute('src')).toBe(PNG);
  });

  it('download produces a PNG image via a synthetic anchor click', async () => {
    await setup('X', 'AZ-900', 'QR_AZ-900');

    const anchor = document.createElement('a');
    const clickSpy = spyOn(anchor, 'click');
    spyOn(document, 'createElement').and.returnValue(anchor);

    probe().download();

    expect(anchor.href).toContain('data:image/png');   // the downloaded file is an image
    expect(anchor.download).toBe('QR_AZ-900.png');
    expect(clickSpy).toHaveBeenCalled();
  });
});
