import { Routes } from '@angular/router';
import { authGuard } from './features/auth/auth.guard';

export const routes: Routes = [
  { path: 'auth', loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES) },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage) },
      { path: 'accounts', loadComponent: () => import('./features/accounts/accounts-list.page').then((m) => m.AccountsListPage) },
      { path: 'accounts/detail', loadComponent: () => import('./features/accounts/account-detail.page').then((m) => m.AccountDetailPage) },
      { path: 'transfers', loadComponent: () => import('./features/transfers/transfer.page').then((m) => m.TransferPage) },
      { path: 'cards', loadComponent: () => import('./features/cards/cards.page').then((m) => m.CardsPage) },
      { path: 'settings', loadComponent: () => import('./features/settings/settings.page').then((m) => m.SettingsPage) },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
