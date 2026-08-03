import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { AppNotification, NotificationsStore } from '../../core/data/notifications.store';
import { CardComponent } from '../../shared/ui/card/card.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';

type Filter = 'all' | 'unread';

@Component({
  selector: 'app-notifications',
  imports: [
    DatePipe,
    MatButtonModule,
    MatTooltipModule,
    TranslatePipe,
    CardComponent,
    EmptyStateComponent,
    IconComponent,
    PageHeaderComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.scss',
})
export class NotificationsComponent {
  private readonly router = inject(Router);

  protected readonly store = inject(NotificationsStore);
  protected readonly filter = signal<Filter>('all');

  protected readonly entries = computed(() =>
    this.filter() === 'unread' ? this.store.unread() : this.store.all()
  );

  protected open(entry: AppNotification): void {
    this.store.markRead(entry.id);
    if (entry.route) {
      void this.router.navigate([entry.route]);
    }
  }

  protected markRead(entry: AppNotification, event: Event): void {
    event.stopPropagation();
    this.store.markRead(entry.id);
  }

  protected remove(entry: AppNotification, event: Event): void {
    event.stopPropagation();
    this.store.remove(entry.id);
  }
}
