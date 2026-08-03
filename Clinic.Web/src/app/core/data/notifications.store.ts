import { Injectable, computed } from '@angular/core';

import { IconName } from '../../shared/ui/icon/icon.registry';
import { LocalCollection, newId } from './local-collection';

export type NotificationTone = 'info' | 'success' | 'warning' | 'danger';

export interface AppNotification {
  id: string;
  /** Already-localised text — notifications are generated at runtime. */
  title: string;
  body: string;
  icon: IconName;
  tone: NotificationTone;
  /** ISO timestamp. */
  createdAt: string;
  read: boolean;
  /** Router link opened when the notification is activated. */
  route?: string;
}

/**
 * In-app alerts.
 *
 * The API has no notification endpoint, so entries are raised by the app itself
 * (a booking conflict avoided, a payment recorded, a low balance) and persisted
 * locally. The read/unread contract is the part that matters: when a real
 * endpoint arrives, only `load` and the mutations here change.
 */
@Injectable({ providedIn: 'root' })
export class NotificationsStore {
  private readonly collection = new LocalCollection<AppNotification>({
    key: 'notifications',
    version: 1,
    seed: () => [],
    searchFields: ['title', 'body'],
  });

  /** Newest first — the only order a notification list is ever read in. */
  readonly all = computed(() =>
    [...this.collection.all()].sort((a, b) => b.createdAt.localeCompare(a.createdAt))
  );

  readonly unread = computed(() => this.all().filter((entry) => !entry.read));
  readonly unreadCount = computed(() => this.unread().length);

  push(
    input: Omit<AppNotification, 'id' | 'createdAt' | 'read'> & { createdAt?: string }
  ): AppNotification {
    return this.collection.insert({
      ...input,
      id: newId('ntf'),
      createdAt: input.createdAt ?? new Date().toISOString(),
      read: false,
    });
  }

  markRead(id: string): void {
    this.collection.update(id, { read: true });
  }

  markAllRead(): void {
    this.collection.replaceAll(this.collection.all().map((entry) => ({ ...entry, read: true })));
  }

  remove(id: string): void {
    this.collection.remove(id);
  }

  clear(): void {
    this.collection.replaceAll([]);
  }
}
