import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../core/services/auth.service';
import { JwtService } from '../../../core/services/jwt.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SessionUser } from '../user.model';

@Component({
  selector: 'app-user-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    EmptyStateComponent,
    PageHeaderComponent
  ],
  templateUrl: './user-detail.component.html',
  styleUrl: './user-detail.component.scss'
})
export class UserDetailComponent {
  private readonly auth = inject(AuthService);
  private readonly jwt = inject(JwtService);

  readonly user: SessionUser | null = this.buildUser();

  initials(name: string): string {
    return name
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part.charAt(0).toUpperCase())
      .join('');
  }

  private buildUser(): SessionUser | null {
    const current = this.auth.currentUser;
    if (!current) {
      return null;
    }

    return {
      id: this.jwt.getUserId(current.token) ?? 'me',
      displayName: current.displayName,
      username: this.jwt.getUserName(current.token),
      email: current.email,
      roles: current.roles,
      expiresAt: this.jwt.getExpiry(current.token)
    };
  }
}
