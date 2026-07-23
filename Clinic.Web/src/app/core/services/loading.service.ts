import { Injectable } from '@angular/core';
import { BehaviorSubject, distinctUntilChanged, map } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly pendingRequests = new BehaviorSubject(0);
  readonly isLoading$ = this.pendingRequests.pipe(
    map((count) => count > 0),
    distinctUntilChanged()
  );

  show(): void {
    this.pendingRequests.next(this.pendingRequests.value + 1);
  }

  hide(): void {
    this.pendingRequests.next(Math.max(0, this.pendingRequests.value - 1));
  }
}

