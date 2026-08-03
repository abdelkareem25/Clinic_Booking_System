import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterOutlet } from '@angular/router';

import { LoadingService } from './core/services/loading.service';

/**
 * The application root.
 *
 * The only chrome here is a top progress bar driven by the HTTP loading
 * interceptor — a 2px line at the very top of the viewport rather than a
 * blocking spinner, so an in-flight request never takes the UI away from the
 * user mid-task.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatProgressBarModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly loading = inject(LoadingService);
}
