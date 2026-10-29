import { DIALOG_DATA, Dialog, DialogRef } from '@angular/cdk/dialog';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { fireEvent, screen, waitFor, within } from '@testing-library/angular';
import { RecordedPayment } from '../settle-up.service';
import { DeletePaymentDialogComponent, DeletePaymentDialogData } from './delete-payment-dialog.component';

const BODY = 'Jo paid Sam 40.00. Deleting it puts the amount back into the balances.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';

const PAYMENT: RecordedPayment = {
  id: 'pay-1',
  payerPersonId: 'p-jo',
  payerName: 'Jo',
  payeePersonId: 'p-sam',
  payeeName: 'Sam',
  amountCents: 4000,
  markedPaidAt: '2026-10-20T10:00:00Z',
};

let fixture: ComponentFixture<DeletePaymentDialogComponent>;
let http: HttpTestingController;
let close: jasmine.Spy;

/** Renders the dialog for trip-1 and the Jo paid Sam payment. */
function render(): void {
  const data: DeletePaymentDialogData = { tripId: 'trip-1', payment: PAYMENT };
  close = jasmine.createSpy('close');
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: DIALOG_DATA, useValue: data },
      { provide: DialogRef, useValue: { close } },
    ],
  });
  fixture = TestBed.createComponent(DeletePaymentDialogComponent);
  http = TestBed.inject(HttpTestingController);
  fixture.detectChanges();
}

/** Lets pending work resolve, then re-checks the view. */
async function settle(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 20));
  fixture.detectChanges();
}

describe('DeletePaymentDialogComponent (F6.R8, F6.AC6)', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the exact title, body and buttons (F6.R8)', () => {
    render();
    const dialog = fixture.nativeElement as HTMLElement;

    expect(within(dialog).getByRole('heading', { name: 'Delete this payment?' })).toBeTruthy();
    expect(within(dialog).getByText(BODY)).toBeTruthy();
    expect(within(dialog).getByRole('button', { name: 'Delete payment' })).toBeTruthy();
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toBeTruthy();
  });

  it('closes on Cancel with no result and sends no DELETE, so the payment stays (F6.AC6)', () => {
    render();

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(close).toHaveBeenCalledWith();
    http.expectNone({ method: 'DELETE' });
  });

  it('sends DELETE for the payment, then closes with deleted', async () => {
    render();

    fireEvent.click(screen.getByRole('button', { name: 'Delete payment' }));
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/payments/pay-1' }).flush(null, { status: 204, statusText: 'No Content' });
    await settle();

    expect(close).toHaveBeenCalledWith('deleted');
  });

  it('closes with missing on 404, so the list can say the payment no longer exists', async () => {
    render();

    fireEvent.click(screen.getByRole('button', { name: 'Delete payment' }));
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/payments/pay-1' }).flush(null, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(close).toHaveBeenCalledWith('missing');
  });

  it('shows the generic message inside the dialog for other errors and stays open', async () => {
    render();

    fireEvent.click(screen.getByRole('button', { name: 'Delete payment' }));
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/payments/pay-1' }).flush(null, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(close).not.toHaveBeenCalled();
    expect(screen.getByRole('alert').textContent).toContain(GENERIC_ERROR);
  });
});

/** Opens the real dialog the way the Payments made list does, so the CDK focus behaviour is tested. */
@Component({
  selector: 'app-delete-opener-host',
  template: '<button type="button" id="opener" (click)="open()">Delete Jo paid Sam 40.00</button>',
})
class DeleteOpenerHostComponent {
  private readonly dialog = inject(Dialog);

  open(): void {
    this.dialog.open(DeletePaymentDialogComponent, {
      data: { tripId: 'trip-1', payment: PAYMENT },
      role: 'dialog',
      ariaModal: true,
      ariaLabelledBy: 'delete-payment-title',
      ariaDescribedBy: 'delete-payment-body',
      autoFocus: '.delete-payment__cancel',
    });
  }
}

describe('DeletePaymentDialogComponent focus and Escape (F6.R8, Accessibility)', () => {
  /** Renders the host, opens the dialog from its button, and returns the opener and the dialog. */
  async function openDialog(): Promise<{ opener: HTMLElement; dialog: HTMLElement }> {
    TestBed.configureTestingModule({
      imports: [DeleteOpenerHostComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const host = TestBed.createComponent(DeleteOpenerHostComponent);
    host.detectChanges();
    const opener = screen.getByRole('button', { name: 'Delete Jo paid Sam 40.00' });
    opener.focus();
    fireEvent.click(opener);
    host.detectChanges();
    const dialog = await screen.findByRole('dialog');
    return { opener, dialog };
  }

  it('opens with initial focus on Cancel (F6.R8)', async () => {
    const { dialog } = await openDialog();

    await waitFor(() => expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Cancel' })));
  });

  it('closes on Escape and returns focus to the button that opened it', async () => {
    const { opener, dialog } = await openDialog();

    fireEvent.keyDown(document.activeElement!, { key: 'Escape', code: 'Escape', keyCode: 27 });

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(opener));
    expect(dialog).toBeTruthy();
  });

  it('keeps focus inside while open: focus wraps back into the dialog from the end anchor', async () => {
    const { dialog } = await openDialog();
    const endAnchor = document.querySelectorAll<HTMLElement>('.cdk-focus-trap-anchor')[1];
    endAnchor.focus();

    await waitFor(() => expect(dialog.contains(document.activeElement)).toBeTrue());
  });
});
