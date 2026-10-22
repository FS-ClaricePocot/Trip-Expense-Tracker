import { expect, type Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';

// Every test gets its own email, so no test depends on another test's account.
export const PASSWORD = 'correct-horse-42';

export function uniqueEmail(): string {
  return `e2e-${Date.now()}-${randomUUID().slice(0, 8)}@example.com`;
}

export async function registerViaUi(page: Page, email: string, password = PASSWORD): Promise<void> {
  await page.goto('/register');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByText("You're logged in. Your trips will appear here.")).toBeVisible();
}

export async function logOutViaUi(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Log out' }).click();
  await expect(page).toHaveURL(/\/login$/);
}
