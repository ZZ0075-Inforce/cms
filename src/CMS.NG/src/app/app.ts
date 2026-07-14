import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

/** A leaf item in the sidebar. */
export interface NavItem {
  label: string;
  icon: string;
  route: string;
}

/** A collapsible group in the sidebar. */
export interface NavGroup {
  label: string;
  icon: string;
  expanded: boolean;
  items: NavItem[];
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastModule, ConfirmDialogModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly collapsed = signal(false);

  /**
   * Only 系統管理 Admin is populated — the other groups exist in the design but have no features
   * yet, so they are not rendered. New features add a NavItem to the matching group here.
   */
  protected readonly navGroups = signal<NavGroup[]>([
    {
      label: '系統管理 Admin',
      icon: 'pi pi-shield',
      expanded: true,
      items: [{ label: '角色 AppRole', icon: 'pi pi-id-card', route: '/app-roles' }]
    }
  ]);

  protected toggleSidebar(): void {
    this.collapsed.update(value => !value);
  }

  protected toggleGroup(target: NavGroup): void {
    this.navGroups.update(groups =>
      groups.map(group =>
        group === target ? { ...group, expanded: !group.expanded } : group
      )
    );
  }
}
