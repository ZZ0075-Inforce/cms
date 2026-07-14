import { Routes } from '@angular/router';

export const routes: Routes = [
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

  { path: '**', redirectTo: 'app-roles' }
];
