import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map, startWith } from 'rxjs';

interface Crumb {
  label: string;
  url: string;
  last: boolean;
}

@Component({
  selector: 'app-breadcrumb',
  imports: [RouterLink, MatIconModule],
  template: `
    <nav class="breadcrumb" aria-label="Breadcrumb">
      <a routerLink="/dashboard" class="crumb home" aria-label="Home">
        <mat-icon>home</mat-icon>
      </a>
      @for (crumb of crumbs(); track crumb.url) {
        <mat-icon class="sep">chevron_right</mat-icon>
        @if (crumb.last) {
          <span class="crumb current">{{ crumb.label }}</span>
        } @else {
          <a class="crumb" [routerLink]="crumb.url">{{ crumb.label }}</a>
        }
      }
    </nav>
  `,
  styles: [
    `
      .breadcrumb {
        display: flex;
        align-items: center;
        gap: 4px;
        font-size: 0.85rem;
        color: var(--mat-sys-on-surface-variant);
        overflow: hidden;
      }
      .crumb {
        text-decoration: none;
        color: var(--mat-sys-on-surface-variant);
        white-space: nowrap;
      }
      .crumb:hover:not(.current) {
        color: var(--mat-sys-primary);
      }
      .current {
        color: var(--mat-sys-on-surface);
        font-weight: 600;
      }
      .home {
        display: inline-flex;
      }
      mat-icon.sep,
      .home mat-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
      }
      mat-icon.sep {
        opacity: 0.6;
      }
    `
  ]
})
export class BreadcrumbComponent {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly crumbSignal = signal<Crumb[]>([]);

  readonly crumbs = computed(() => this.crumbSignal());

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        startWith(null),
        map(() => this.build()),
        takeUntilDestroyed()
      )
      .subscribe((crumbs) => this.crumbSignal.set(crumbs));
  }

  private build(): Crumb[] {
    const crumbs: Crumb[] = [];
    let route: ActivatedRoute | null = this.route.root;
    let url = '';

    while (route) {
      const segment = route.snapshot.url.map((part) => part.path).join('/');
      if (segment) {
        url += `/${segment}`;
      }

      const label = route.snapshot.data['breadcrumb'] as string | undefined;
      if (label) {
        crumbs.push({ label, url, last: false });
      }

      route = route.firstChild;
    }

    if (crumbs.length) {
      crumbs[crumbs.length - 1].last = true;
    }

    return crumbs;
  }
}
