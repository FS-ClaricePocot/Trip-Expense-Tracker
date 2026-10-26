import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { GENERIC_ERROR_MESSAGE } from '../../core/errors/problem-mapper';
import { PeopleService } from '../people.service';

/** What the People panel passes in for the person to remove (F3.R6). */
export interface RemovePersonDialogData {
  tripId: string;
  personId: string;
  name: string;
}

/**
 * How the dialog closes, when it is done with the request:
 * - `removed`: the server removed the person (204).
 * - `in-use`: the server refused, the person is in an expense or payment (409, F3.R7).
 * - `missing`: the trip or the person is gone (404), so the panel reloads the list.
 * Cancel closes without a result and sends nothing (F3.AC7).
 */
export type RemovePersonResult = 'removed' | 'in-use' | 'missing';

/**
 * Confirmation dialog for removing a person from the trip (F3.R6). Layout follows the
 * F2 delete dialog. Initial focus is on Cancel, set by the panel (the action is destructive).
 */
@Component({
  selector: 'app-remove-person-dialog',
  templateUrl: './remove-person-dialog.component.html',
  styleUrl: './remove-person-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RemovePersonDialogComponent {
  private readonly peopleService = inject(PeopleService);
  private readonly dialogRef = inject<DialogRef<RemovePersonResult | undefined>>(DialogRef);

  protected readonly data = inject<RemovePersonDialogData>(DIALOG_DATA);
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
      await this.peopleService.remove(this.data.tripId, this.data.personId);
      this.dialogRef.close('removed');
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 409) {
        this.dialogRef.close('in-use');
        return;
      }
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.dialogRef.close('missing');
        return;
      }
      // A 401 is sent to log-in by the API interceptor.
      this.errorMessage.set(GENERIC_ERROR_MESSAGE);
    } finally {
      this.submitting.set(false);
    }
  }
}
