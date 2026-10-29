import { ComponentFixture, TestBed } from '@angular/core/testing';
import { fireEvent, screen } from '@testing-library/angular';
import { TripViewSwitchComponent, TripView } from './trip-view-switch.component';

function render(view: TripView): ComponentFixture<TripViewSwitchComponent> {
  TestBed.configureTestingModule({});
  const fixture = TestBed.createComponent(TripViewSwitchComponent);
  fixture.componentRef.setInput('view', view);
  fixture.detectChanges();
  return fixture;
}

describe('TripViewSwitchComponent', () => {
  it('is labelled "Trip view" and shows Expenses as pressed by default (F6 spec, Accessibility)', () => {
    render('expenses');

    expect(screen.getByRole('group', { name: 'Trip view' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Expenses' }).getAttribute('aria-pressed')).toBe('true');
    expect(screen.getByRole('button', { name: 'Settle up' }).getAttribute('aria-pressed')).toBe('false');
  });

  it('emits settle-up when Settle up is selected (F6 spec, User flow step 1)', () => {
    const fixture = render('expenses');
    const picked: TripView[] = [];
    fixture.componentInstance.viewChange.subscribe((view) => picked.push(view));

    fireEvent.click(screen.getByRole('button', { name: 'Settle up' }));

    expect(picked).toEqual(['settle-up']);
  });
});
