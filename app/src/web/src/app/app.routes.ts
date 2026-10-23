import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { LoginComponent } from './auth/login/login.component';
import { RegisterComponent } from './auth/register/register.component';
import { TripListComponent } from './trips/trip-list/trip-list.component';
import { TripPageComponent } from './trips/trip-page/trip-page.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'trips' },
  { path: 'register', component: RegisterComponent },
  { path: 'login', component: LoginComponent },
  { path: 'trips', component: TripListComponent, canActivate: [authGuard] },
  { path: 'trips/:tripId', component: TripPageComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: 'trips' },
];
