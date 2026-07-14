import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { App } from './app';

describe('App shell', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        // The shell hosts <p-toast> and <p-confirmDialog>, which inject these.
        MessageService,
        ConfirmationService
      ]
    }).compileComponents();
  });

  it('creates the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the 系統管理 Admin group with a 角色 AppRole link to /app-roles', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('系統管理 Admin');
    expect(text).toContain('角色 AppRole');

    const link: HTMLAnchorElement | null = fixture.nativeElement.querySelector('a.nav-item');
    expect(link).not.toBeNull();
    expect(link!.getAttribute('href')).toBe('/app-roles');
  });
});
