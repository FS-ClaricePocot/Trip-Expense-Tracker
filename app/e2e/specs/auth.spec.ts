import { expect, test } from '@playwright/test';
import { PASSWORD, logOutViaUi, registerViaUi, uniqueEmail } from './helpers';

// F1.AC1, AC2, AC3, AC5, AC6, AC13. Each test registers its own unique email and starts with a fresh browser context.

test('register, see the trip list, then log out and land on the log-in page', async ({ page }) => {
  await registerViaUi(page, uniqueEmail());
  await logOutViaUi(page);
  await expect(page.getByRole('heading', { name: 'Welcome back' })).toBeVisible();
});

for (const path of ['/trips', '/trips/abc']) {
  test(`logged out, opening ${path} redirects to log-in with the message`, async ({ page }) => {
    await page.goto(path);
    await expect(page).toHaveURL(/\/login/);
    await expect(page.getByRole('alert')).toContainText('Please log in to see your trips.');
  });
}

test('a duplicate email with different capitals shows the generic message', async ({ page }) => {
  const email = uniqueEmail();
  await registerViaUi(page, email);
  await logOutViaUi(page);

  await page.goto('/register');
  await page.getByLabel('Email').fill(email.toUpperCase());
  await page.getByLabel('Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Create account' }).click();

  await expect(page.getByRole('alert')).toContainText("We couldn't create your account with these details.");
  await expect(page).toHaveURL(/\/register$/);
});

test('a wrong password shows "Email or password is incorrect."', async ({ page }) => {
  const email = uniqueEmail();
  await registerViaUi(page, email);
  await logOutViaUi(page);

  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill('wrong-password-99');
  await page.getByRole('button', { name: 'Log in' }).click();

  await expect(page.getByRole('alert')).toContainText('Email or password is incorrect.');
  await expect(page).toHaveURL(/\/login$/);
});

test('after logout, going to /trips redirects to the log-in page', async ({ page }) => {
  await registerViaUi(page, uniqueEmail());
  await logOutViaUi(page);

  await page.goto('/trips');
  await expect(page).toHaveURL(/\/login/);
  await expect(page.getByRole('heading', { name: 'Welcome back' })).toBeVisible();
});
