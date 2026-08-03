import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { LucideDynamicIcon } from '@lucide/angular';

import { IconName, iconData } from './icon.registry';

/** Three sizes only — anything else and icons stop aligning to the type scale. */
export type IconSize = 'sm' | 'md' | 'lg';

const SIZE_PX: Record<IconSize, number> = {
  sm: 16,
  md: 18,
  lg: 20,
};

/**
 * The single way an icon is rendered in this app.
 *
 *   <ui-icon name="patients" />
 *   <ui-icon name="delete" size="sm" />
 *
 * Wrapping Lucide behind a named registry keeps icons out of component imports,
 * pins them to the type scale, and gives every icon the same stroke weight and
 * accessibility treatment (decorative by default, labelled on request).
 */
@Component({
  selector: 'ui-icon',
  imports: [LucideDynamicIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      [lucideIcon]="data()"
      [size]="pixels()"
      [strokeWidth]="strokeWidth()"
      [attr.aria-hidden]="label() ? null : 'true'"
      [attr.role]="label() ? 'img' : null"
      [attr.aria-label]="label()"
    ></svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: 0 0 auto;
      color: inherit;
      line-height: 0;
    }

    svg {
      display: block;
    }
  `,
})
export class IconComponent {
  readonly name = input.required<IconName>();
  readonly size = input<IconSize>('md');
  readonly strokeWidth = input(1.75);
  /** Set only when the icon carries meaning no adjacent text already conveys. */
  readonly label = input<string | null>(null);

  protected readonly data = computed(() => iconData(this.name()));
  protected readonly pixels = computed(() => SIZE_PX[this.size()]);
}
