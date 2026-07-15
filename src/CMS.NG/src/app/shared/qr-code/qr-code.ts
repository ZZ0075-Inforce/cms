import { Component, effect, inject, input, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';

import { QrCodeService } from '@core/services/qr-code.service';

/**
 * Presentational QR-code widget: encodes <c>value</c> to a PNG data URL, shows it under an optional
 * <c>title</c>, and offers a download of the generated image. Re-encodes whenever <c>value</c> changes.
 */
@Component({
  selector: 'app-qr-code',
  imports: [ButtonModule],
  templateUrl: './qr-code.html',
  styleUrl: './qr-code.scss'
})
export class QrCode {
  private readonly qrService = inject(QrCodeService);

  /** The text/URL to encode. */
  readonly value = input.required<string>();
  /** Caption shown above the code (the Course.CourseId, here). */
  readonly title = input<string | null>(null);
  /** Base name for the downloaded PNG (extension is added on download). */
  readonly fileName = input<string>('qrcode');

  protected readonly dataUrl = signal<string | null>(null);
  protected readonly failed = signal(false);

  constructor() {
    // Re-encode on every value change; encoding is async so the image fills in once resolved.
    effect(() => {
      const value = this.value();
      if (!value) {
        this.dataUrl.set(null);
        return;
      }
      this.qrService.toDataUrl(value)
        .then(url => {
          this.dataUrl.set(url);
          this.failed.set(false);
        })
        .catch(() => {
          this.dataUrl.set(null);
          this.failed.set(true);
        });
    });
  }

  /** Downloads the generated PNG via a synthetic anchor click. */
  protected download(): void {
    const url = this.dataUrl();
    if (!url) return;
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${this.fileName() || 'qrcode'}.png`;
    anchor.click();
  }
}
