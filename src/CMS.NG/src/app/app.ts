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
   * Groups with no features yet are not rendered at all. New features add a NavItem to the
   * matching group here (or add the group, if it is the first feature in it).
   */
  protected readonly navGroups = signal<NavGroup[]>([
    {
      label: '首頁 Home',
      icon: 'pi pi-home',
      expanded: true,
      items: [
        { label: '上稿作業 FeaturedPromoItem', icon: 'pi pi-megaphone', route: '/featured-promo-items' }
      ]
    },
    {
      label: '系統管理 Admin',
      icon: 'pi pi-shield',
      expanded: true,
      items: [
        { label: '角色 AppRole', icon: 'pi pi-id-card', route: '/app-roles' },
        { label: '使用者 AppUser', icon: 'pi pi-users', route: '/app-users' }
      ]
    },
    {
      label: '課程管理 Course',
      icon: 'pi pi-book',
      expanded: true,
      items: [
        { label: '合作廠商 Partner', icon: 'pi pi-building', route: '/partners' },
        { label: '課程群組 CourseGroup', icon: 'pi pi-sitemap', route: '/course-groups' },
        { label: '課程 Course', icon: 'pi pi-book', route: '/courses' }
      ]
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
