import { Injectable, computed, signal } from '@angular/core';

/**
 * Tracks in-flight HTTP requests for the global progress bar.
 *
 * A counter rather than a boolean: with several concurrent requests (the
 * dashboard fires four), the first response to land would otherwise switch the
 * indicator off while three are still running.
 */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly pending = signal(0);

  readonly isLoading = computed(() => this.pending() > 0);

  show(): void {
    this.pending.update((count) => count + 1);
  }

  hide(): void {
    this.pending.update((count) => Math.max(0, count - 1));
  }
}
