import { render, screen } from '@testing-library/angular';
import { TripsPlaceholderComponent } from './trips-placeholder.component';

describe('TripsPlaceholderComponent', () => {
  it('shows the post-login placeholder copy', async () => {
    await render(TripsPlaceholderComponent);

    expect(screen.getByText("You're logged in. Your trips will appear here.")).toBeTruthy();
  });
});
