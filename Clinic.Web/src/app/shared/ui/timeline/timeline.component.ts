import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, contentChild, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { CellTemplateDirective } from '../data-table/data-table.model';
import { BadgeTone } from '../data-table/data-table.model';
import { IconComponent } from '../icon/icon.component';
import { IconName } from '../icon/icon.registry';

export interface TimelineFact {
  label: string;
  value: string;
}

export interface TimelineEntry {
  id: string;
  /** ISO string or Date; entries are rendered newest first by the caller. */
  date: string | Date;
  /** Translation key for the entry type, e.g. `records.typeDiagnosis`. */
  typeLabel: string;
  icon: IconName;
  tone: BadgeTone;
  /** Already-localised free text — clinical content is not translated. */
  title: string;
  summary?: string;
  facts?: TimelineFact[];
  tags?: string[];
  author?: string;
}

/**
 * The medical-history timeline.
 *
 * A patient's history is the one place in a clinic that is read chronologically
 * rather than scanned as a table — "what happened, in what order, and by whom".
 * Entries are grouped under a sticky date heading so a long history stays
 * navigable, and the rail is drawn with logical properties so it flips with the
 * writing direction.
 */
@Component({
  selector: 'ui-timeline',
  imports: [DatePipe, NgTemplateOutlet, TranslatePipe, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="timeline">
      @for (group of groups(); track group.key) {
        <section class="group">
          <h3 class="group-date">
            <ui-icon name="clock" size="sm" />
            {{ group.date | date: 'EEEE, d MMMM y' }}
          </h3>

          @for (entry of group.entries; track entry.id) {
            <article class="entry" [class.entry-clickable]="clickable()">
              <span class="rail" aria-hidden="true"></span>
              <span class="dot" [class]="'dot-' + entry.tone">
                <ui-icon [name]="entry.icon" size="sm" />
              </span>

              <div class="body">
                <header class="entry-head">
                  <span class="badge" [class]="'badge-' + entry.tone">
                    {{ entry.typeLabel | translate }}
                  </span>
                  <time class="entry-time">{{ entry.date | date: 'h:mm a' }}</time>
                  @if (entry.author; as author) {
                    <span class="entry-author">· {{ author }}</span>
                  }

                  <span class="spacer"></span>

                  @if (actionTemplate(); as slot) {
                    <span class="entry-actions">
                      <ng-container
                        [ngTemplateOutlet]="slot.template"
                        [ngTemplateOutletContext]="{ $implicit: entry }"
                      />
                    </span>
                  }
                </header>

                <button
                  type="button"
                  class="entry-title"
                  [disabled]="!clickable()"
                  (click)="entryClick.emit(entry)"
                >
                  {{ entry.title }}
                </button>

                @if (entry.summary; as summary) {
                  <p class="entry-summary">{{ summary }}</p>
                }

                @if (entry.facts?.length) {
                  <dl class="facts">
                    @for (fact of entry.facts; track fact.label) {
                      <div class="fact">
                        <dt>{{ fact.label | translate }}</dt>
                        <dd>{{ fact.value }}</dd>
                      </div>
                    }
                  </dl>
                }

                @if (entry.tags?.length) {
                  <div class="tags">
                    @for (tag of entry.tags; track tag) {
                      <span class="badge badge-neutral">{{ tag }}</span>
                    }
                  </div>
                }
              </div>
            </article>
          }
        </section>
      }
    </div>
  `,
  styleUrl: './timeline.component.scss',
})
export class TimelineComponent {
  readonly entries = input.required<readonly TimelineEntry[]>();
  readonly clickable = input(false);

  readonly entryClick = output<TimelineEntry>();

  /** `<ng-template uiCell="actions" let-entry>` renders per-entry controls. */
  protected readonly actionTemplate = contentChild(CellTemplateDirective);

  protected readonly groups = computed(() => {
    const buckets = new Map<string, { key: string; date: Date; entries: TimelineEntry[] }>();

    for (const entry of this.entries()) {
      const date = entry.date instanceof Date ? entry.date : new Date(entry.date);
      const key = `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;

      const bucket = buckets.get(key);
      if (bucket) {
        bucket.entries.push(entry);
      } else {
        buckets.set(key, { key, date, entries: [entry] });
      }
    }

    return [...buckets.values()];
  });
}
