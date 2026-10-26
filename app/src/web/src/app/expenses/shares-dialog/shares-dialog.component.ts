import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { formatCents } from '../money';

/** One participant's share, in the order they were added (F4.R10a). Amounts are whole cents. */
export interface SharesDialogParticipant {
  id: string;
  name: string;
  shareCents: number;
}

/** What the Expenses panel passes in: the expense's description, total and participants (F4.R10a). */
export interface SharesDialogData {
  description: string;
  amountCents: number;
  participants: SharesDialogParticipant[];
}

/**
 * Read-only dialog listing each participant's share and the total (F4.R10a, F4.AC18).
 * It sends nothing. Close closes it and focus returns to the row that opened it.
 */
@Component({
  selector: 'app-shares-dialog',
  templateUrl: './shares-dialog.component.html',
  styleUrl: './shares-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SharesDialogComponent {
  private readonly dialogRef = inject<DialogRef<void>>(DialogRef);

  protected readonly data = inject<SharesDialogData>(DIALOG_DATA);
  protected readonly formatCents = formatCents;

  protected close(): void {
    this.dialogRef.close();
  }
}
