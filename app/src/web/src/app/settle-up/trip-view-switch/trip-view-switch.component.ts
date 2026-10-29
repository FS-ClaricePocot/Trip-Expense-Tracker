import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** The two trip views (F6 spec, User flow step 1). Expenses is the default. */
export type TripView = 'expenses' | 'settle-up';

/**
 * The "Trip view" switch: two buttons, Expenses and Settle up. Each button announces its state with aria-pressed.
 */
@Component({
  selector: 'app-trip-view-switch',
  templateUrl: './trip-view-switch.component.html',
  styleUrl: './trip-view-switch.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TripViewSwitchComponent {
  /** The view that is shown now. */
  readonly view = input.required<TripView>();
  /** Emitted with the view the user picked. */
  readonly viewChange = output<TripView>();

  protected select(view: TripView): void {
    this.viewChange.emit(view);
  }
}
