import { Component } from '@angular/core';
import { HeaderComponent } from '../core/header/header.component';

/**
 * Post-login placeholder (F1.R16). F2 replaces the body of this component with
 * the trip list. The route and the shared header stay as they are.
 */
@Component({
  selector: 'app-trips-placeholder',
  imports: [HeaderComponent],
  template: `
    <app-header />
    <p>You're logged in. Your trips will appear here.</p>
  `,
})
export class TripsPlaceholderComponent {}
