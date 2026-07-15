import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';

export const routes: Routes = [
  // Public. Sits OUTSIDE the guarded parent — if it were a child, the guard's redirect to /login
  // would re-enter the guard and loop.
  {
    path: 'login',
    loadComponent: () => import('@features/auth/login/login').then(m => m.Login),
    title: '登入'
  },

  // Everything below requires a signed-in user. The guard on this pathless parent covers all children.
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'app-roles', pathMatch: 'full' },

      {
        path: 'app-roles',
        loadComponent: () =>
          import('@features/app-roles/app-role-list/app-role-list').then(m => m.AppRoleList),
        title: '角色 AppRole'
      },
      // 'new' MUST precede ':id'. roleId is a string, so ':id' carries no :int constraint to exclude
      // the literal "new" — declared the other way round, /app-roles/new would resolve to the detail
      // page for a role named "new".
      {
        path: 'app-roles/new',
        loadComponent: () =>
          import('@features/app-roles/app-role-form/app-role-form').then(m => m.AppRoleForm),
        title: '新增角色'
      },
      {
        path: 'app-roles/:id/edit',
        loadComponent: () =>
          import('@features/app-roles/app-role-form/app-role-form').then(m => m.AppRoleForm),
        title: '編輯角色'
      },
      {
        path: 'app-roles/:id',
        loadComponent: () =>
          import('@features/app-roles/app-role-detail/app-role-detail').then(m => m.AppRoleDetail),
        title: '檢視角色'
      },

      {
        path: 'app-users',
        loadComponent: () =>
          import('@features/app-users/app-user-list/app-user-list').then(m => m.AppUserList),
        title: '使用者 AppUser'
      },
      // 'new' MUST precede ':id'. userId is a string, so ':id' carries no :int constraint to exclude
      // the literal "new" — declared the other way round, /app-users/new would resolve to the detail
      // page for a user named "new".
      {
        path: 'app-users/new',
        loadComponent: () =>
          import('@features/app-users/app-user-form/app-user-form').then(m => m.AppUserForm),
        title: '新增使用者'
      },
      {
        path: 'app-users/:id/edit',
        loadComponent: () =>
          import('@features/app-users/app-user-form/app-user-form').then(m => m.AppUserForm),
        title: '編輯使用者'
      },
      {
        path: 'app-users/:id',
        loadComponent: () =>
          import('@features/app-users/app-user-detail/app-user-detail').then(m => m.AppUserDetail),
        title: '檢視使用者'
      },

      {
        path: 'partners',
        loadComponent: () =>
          import('@features/partners/partner-list/partner-list').then(m => m.PartnerList),
        title: '合作廠商 Partner'
      },
      // 'new' before ':id', per the convention. Partner's pkid is numeric, so ':id' would not actually
      // swallow the literal "new" — but the ordering is the house rule and costs nothing to keep.
      {
        path: 'partners/new',
        loadComponent: () =>
          import('@features/partners/partner-form/partner-form').then(m => m.PartnerForm),
        title: '新增合作廠商'
      },
      {
        path: 'partners/:id/edit',
        loadComponent: () =>
          import('@features/partners/partner-form/partner-form').then(m => m.PartnerForm),
        title: '編輯合作廠商'
      },
      {
        path: 'partners/:id',
        loadComponent: () =>
          import('@features/partners/partner-detail/partner-detail').then(m => m.PartnerDetail),
        title: '檢視合作廠商'
      },

      {
        path: 'course-groups',
        loadComponent: () =>
          import('@features/course-groups/course-group-list/course-group-list').then(m => m.CourseGroupList),
        title: '課程群組 CourseGroup'
      },
      // 'new' before ':id', per the convention. CourseGroup's pkid is numeric, so ':id' would not
      // actually swallow the literal "new" — but the ordering is the house rule and costs nothing to keep.
      {
        path: 'course-groups/new',
        loadComponent: () =>
          import('@features/course-groups/course-group-form/course-group-form').then(m => m.CourseGroupForm),
        title: '新增課程群組'
      },
      {
        path: 'course-groups/:id/edit',
        loadComponent: () =>
          import('@features/course-groups/course-group-form/course-group-form').then(m => m.CourseGroupForm),
        title: '編輯課程群組'
      },
      {
        path: 'course-groups/:id',
        loadComponent: () =>
          import('@features/course-groups/course-group-detail/course-group-detail').then(m => m.CourseGroupDetail),
        title: '檢視課程群組'
      },

      {
        path: 'courses',
        loadComponent: () =>
          import('@features/courses/course-list/course-list').then(m => m.CourseList),
        title: '課程 Course'
      },
      // 'new' before ':id', per the convention.
      {
        path: 'courses/new',
        loadComponent: () =>
          import('@features/courses/course-form/course-form').then(m => m.CourseForm),
        title: '新增課程'
      },
      {
        path: 'courses/:id/edit',
        loadComponent: () =>
          import('@features/courses/course-form/course-form').then(m => m.CourseForm),
        title: '編輯課程'
      },
      {
        path: 'courses/:id',
        loadComponent: () =>
          import('@features/courses/course-detail/course-detail').then(m => m.CourseDetail),
        title: '檢視課程'
      },

      // 首頁 Home — 上稿作業. A bespoke board (centre tabs + week navigator + slot grid with inline
      // Edit/New/Paste), so it needs only the one list route; there is no separate detail/form page.
      {
        path: 'featured-promo-items',
        loadComponent: () =>
          import('@features/featured-promo-items/featured-promo-item-list/featured-promo-item-list')
            .then(m => m.FeaturedPromoItemList),
        title: '上稿作業 FeaturedPromoItem'
      }
    ]
  },

  { path: '**', redirectTo: 'app-roles' }
];
