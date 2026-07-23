import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';

import { AuthService } from '../../../core/services/auth.service';
import { JwtService } from '../../../core/services/jwt.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import {
  RowActionEvent,
  TableColumn,
  TableRowAction
} from '../../../shared/components/data-table/data-table.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SessionUser } from '../user.model';

@Component({
  selector: 'app-user-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, PageHeaderComponent, DataTableComponent],
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.scss'
})
export class UserListComponent {
  private readonly auth = inject(AuthService);
  private readonly jwt = inject(JwtService);
  private readonly router = inject(Router);

  readonly users: SessionUser[] = this.buildRows();

  readonly columns: TableColumn<SessionUser>[] = [
    { key: 'displayName', header: 'Name', value: (row) => row.displayName, variant: 'strong' },
    { key: 'email', header: 'Email', value: (row) => row.email },
    {
      key: 'roles',
      header: 'Roles',
      align: 'center',
      value: (row) => (row.roles.length ? row.roles.join(', ') : 'None'),
      variant: 'chip',
      chip: (row) => ({
        label: row.roles.length ? row.roles.join(', ') : 'No role',
        tone: row.roles.includes('Admin') ? 'primary' : 'neutral'
      })
    }
  ];

  readonly actions: TableRowAction<SessionUser>[] = [
    { id: 'view', icon: 'visibility', tooltip: 'View profile', color: 'primary' }
  ];

  onRowAction(event: RowActionEvent<SessionUser>): void {
    if (event.action === 'view') {
      void this.router.navigate(['/users', 'me']);
    }
  }

  private buildRows(): SessionUser[] {
    const user = this.auth.currentUser;
    if (!user) {
      return [];
    }

    return [
      {
        id: this.jwt.getUserId(user.token) ?? 'me',
        displayName: user.displayName,
        username: this.jwt.getUserName(user.token),
        email: user.email,
        roles: user.roles,
        expiresAt: this.jwt.getExpiry(user.token)
      }
    ];
  }
}
