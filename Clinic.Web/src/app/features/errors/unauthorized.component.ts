import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-unauthorized',
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <section class="error-page">
      <span class="badge"><mat-icon>lock</mat-icon></span>
      <h1>403 — Access denied</h1>
      <p>Your account role does not have permission to view this page. Contact an administrator if you believe this is a mistake.</p>
      <a mat-flat-button color="primary" routerLink="/dashboard">
        <mat-icon>arrow_back</mat-icon>
        Back to dashboard
      </a>
    </section>
  `,
  styles: [
    `
      .error-page {
        display: grid;
        place-items: center;
        text-align: center;
        gap: 12px;
        padding: clamp(32px, 8vw, 96px) 16px;
        max-width: 560px;
        margin: 0 auto;
      }
      .badge {
        display: grid;
        place-items: center;
        width: 84px;
        height: 84px;
        border-radius: 26px;
        background: var(--mat-sys-error-container);
        color: var(--mat-sys-on-error-container);
      }
      .badge mat-icon {
        font-size: 40px;
        width: 40px;
        height: 40px;
      }
      h1 {
        margin: 8px 0 0;
        font-size: 1.6rem;
      }
      p {
        margin: 0;
        color: var(--mat-sys-on-surface-variant);
        line-height: 1.6;
      }
    `
  ]
})
export class UnauthorizedComponent {}
