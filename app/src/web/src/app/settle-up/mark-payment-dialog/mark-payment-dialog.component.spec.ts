import { DIALOG_DATA, Dialog, DialogRef } from '@angular/cdk/dialog';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { fireEvent, screen, waitFor, within } from '@testing-library/angular';
import { SuggestedPayment } from '../settle-up.service';
import { MarkPaymentDialogComponent, MarkPaymentDialogData } from './mark-payment-dialog.component';

const SAM_PAID = 'Jo pays Sam 40.00. This records that the money has moved outside the app.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';

const SUGGESTION: SuggestedPayment = {
  payerPersonId: 'p-jo',
  payerName: 'Jo',
  payeePersonId: 'p-sam',
  payeeName: 'Sam',
  amountCents: 4000,
};

let fixture: ComponentFixture<MarkPaymentDialogComponent>;
let http: HttpTestingController;
let close: jasmine.Spy;

/** Renders the dialog for trip-1 and the Example 1 suggestion (F6.AC1). */
function render(): void {
  const data: MarkPaymentDialogData = { tripId: 'trip-1', suggestion: SUGGESTION };
  close = jasmine.createSpy('close');
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: DIALOG_DATA, useValue: data },
      { provide: DialogRef, useValue: { close } },
    ],
  });
  fixture = TestBed.createComponent(MarkPaymentDialogComponent);
  http = TestBed.inject(HttpTestingController);
  fixture.detectChanges();
}

/** Lets pending work resolve, then re-checks the view. */
async function settle(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 20));
  fixture.detectChanges();
}

describe('MarkPaymentDialogComponent (F6.R5, F6.R6, F6.AC4, F6.AC5, F6.AC9)', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the exact title, body and buttons with the names and amount (F6.R5)', () => {
    render();
    const dialog = fixture.nativeElement as HTMLElement;

    expect(within(dialog).getByRole('heading', { name: 'Mark this payment as paid?' })).toBeTruthy();
    expect(within(dialog).getByText(SAM_PAID)).toBeTruthy();
    expect(within(dialog).getByRole('button', { name: 'Mark as paid' })).toBeTruthy();
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toBeTruthy();
  });

  it('closes on Cancel, sends no request and records nothing (F6.AC4)', () => {
    render();

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(close).toHaveBeenCalledWith();
    http.expectNone(() => true);
  });

  it('sends the payer, payee and amountCents, then closes with recorded (F6.AC5)', async () => {
    render();

    fireEvent.click(screen.getByRole('button', { name: 'Mark as paid' }));
    const request = http.expectOne({ method: 'POST', url: '/api/trips/trip-1/payments' });
    expect(request.request.body).toEqual({ payerPersonId: 'p-jo', payeePersonId: 'p-sam', amountCents: 4000 });
    request.flush({ id: 'pay-1', payerPersonId: 'p-jo', payeePersonId: 'p-sam', amountCents: 4000, markedPaidAt: '2026-10-20T10:00:00Z' }, { status: 201, statusText: 'Created' });
    await settle();

    expect(close).toHaveBeenCalledWith('recorded');
  });

  it('closes with stale on 409, so the view shows the message in the panel (F6.AC9)', async () => {
    render();

    fireEvent.click(screen.getByRole('button', { name: 'Mark as paid' }));
    http
      .expectOne({ method: 'POST', url: '/api/trips/trip-1/payments' })
      .flush({ type: 'suggestions_changed' }, { status: 409, statusText: 'Conflict' });
    await settle();

    expect(close).toHaveBeenCalledWith('stale');
  });

  it('closes with missing on 404, so the view reloads and hands over to the trip page', async () => {
    render();

    fireEvent.click(screen.getByRole('button', { name: 'Mark as paid' }));
    http.expectOne({ method: 'POST', url: '/api/trips/trip-1/payments' }).flush(null, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(close).toHaveBeenCalledWith('missing');
  });

  it('shows the generic message inside the dialog for other errors and stays open', async () => {
    render();

    fireEvent.click(screen.getByRole('button', { name: 'Mark as paid' }));
    http.expectOne({ method: 'POST', url: '/api/trips/trip-1/payments' }).flush(null, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(close).not.toHaveBeenCalled();
    expect(screen.getByRole('alert').textContent).toContain(GENERIC_ERROR);
  });
});

/** Opens the real dialog the way the settle-up view does, so the CDK focus behaviour is tested. */
@Component({
  selector: 'app-mark-opener-host',
  template: '<button type="button" id="opener" (click)="open()">Mark as paid</button>',
})
class MarkOpenerHostComponent {
  private readonly dialog = inject(Dialog);

  open(): void {
    this.dialog.open(MarkPaymentDialogComponent, {
      data: { tripId: 'trip-1', suggestion: SUGGESTION },
      role: 'dialog',
      ariaModal: true,
      ariaLabelledBy: 'mark-payment-title',
      ariaDescribedBy: 'mark-payment-body',
      autoFocus: '.mark-payment__cancel',
    });
  }
}

describe('MarkPaymentDialogComponent focus and Escape (F6.R5, Accessibility)', () => {
  /** Renders the host, opens the dialog from its button, and returns the opener and the dialog. */
  async function openDialog(): Promise<{ opener: HTMLElement; dialog: HTMLElement }> {
    TestBed.configureTestingModule({
      imports: [MarkOpenerHostComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const host = TestBed.createComponent(MarkOpenerHostComponent);
    host.detectChanges();
    const opener = screen.getByRole('button', { name: 'Mark as paid' });
    opener.focus();
    fireEvent.click(opener);
    host.detectChanges();
    const dialog = await screen.findByRole('dialog');
    return { opener, dialog };
  }

  it('opens with initial focus on Cancel (F6.R5)', async () => {
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
    // CDK places focus-trap anchors around the dialog. Moving focus to the end anchor must bring it back inside.
    const endAnchor = document.querySelectorAll<HTMLElement>('.cdk-focus-trap-anchor')[1];
    endAnchor.focus();

    await waitFor(() => expect(dialog.contains(document.activeElement)).toBeTrue());
  });
});
