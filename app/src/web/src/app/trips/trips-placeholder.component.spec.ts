import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { render, screen } from '@testing-library/angular';
import { TripsPlaceholderComponent } from './trips-placeholder.component';

async function renderPlaceholder(): Promise<void> {
  await render(TripsPlaceholderComponent, {
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
  });
}

describe('TripsPlaceholderComponent', () => {
  it('shows the post-login placeholder copy', async () => {
    await renderPlaceholder();

    expect(screen.getByText("You're logged in. Your trips will appear here.")).toBeTruthy();
  });

  it('shows the Log out button from the shared header (F1.R16)', async () => {
    await renderPlaceholder();

    expect(screen.getByRole('button', { name: 'Log out' })).toBeTruthy();
  });
});
