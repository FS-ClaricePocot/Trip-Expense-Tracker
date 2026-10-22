import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { LoginPlaceholderComponent } from './auth/login-placeholder.component';
import { TripsPlaceholderComponent } from './trips/trips-placeholder.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'trips' },
  { path: 'login', component: LoginPlaceholderComponent },
  { path: 'trips', component: TripsPlaceholderComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: 'trips' },
];
