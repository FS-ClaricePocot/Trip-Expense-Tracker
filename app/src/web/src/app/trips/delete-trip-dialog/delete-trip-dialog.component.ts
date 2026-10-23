import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { GENERIC_ERROR_MESSAGE, TRIP_NOT_FOUND_MESSAGE } from '../../core/errors/problem-mapper';
import { TripsService } from '../trips.service';

/** What the trip page passes in. The counts come from the trip detail response (F2.R7). */
export interface DeleteTripDialogData {
  id: string;
  name: string;
  peopleCount: number;
  expenseCount: number;
  paymentCount: number;
}

/**
 * Confirmation dialog for deleting a trip (F2.R7). Cancel closes it and sends nothing.
 * Delete trip sends DELETE; on success the dialog closes with `true` so the page can
 * go to the list. Initial focus is on Cancel, set by the page (the action is destructive).
 */
@Component({
  selector: 'app-delete-trip-dialog',
  templateUrl: './delete-trip-dialog.component.html',
  styleUrl: './delete-trip-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeleteTripDialogComponent {
  private readonly tripsService = inject(TripsService);
  private readonly dialogRef = inject<DialogRef<boolean>>(DialogRef);

  protected readonly data = inject<DeleteTripDialogData>(DIALOG_DATA);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected cancel(): void {
    this.dialogRef.close();
  }

  protected async confirm(): Promise<void> {
    if (this.submitting()) {
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set(null);
    try {
      await this.tripsService.delete(this.data.id);
      this.dialogRef.close(true);
    } catch (error: unknown) {
      const notFound = error instanceof HttpErrorResponse && error.status === 404;
      this.errorMessage.set(notFound ? TRIP_NOT_FOUND_MESSAGE : GENERIC_ERROR_MESSAGE);
    } finally {
      this.submitting.set(false);
    }
  }
}
