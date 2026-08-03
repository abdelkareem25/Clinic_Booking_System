import { Signal, computed, signal } from '@angular/core';

export interface Identified {
  id: string;
}

export interface CollectionQuery {
  search?: string;
  pageIndex?: number;
  pageSize?: number;
  sort?: string;
  direction?: 'asc' | 'desc';
}

export interface CollectionPage<T> {
  data: T[];
  count: number;
  pageIndex: number;
  pageSize: number;
}

export interface LocalCollectionOptions<T extends Identified> {
  /** Storage key suffix, e.g. `roles` → `clinic.store.roles`. */
  key: string;
  /** Rows written on first run (or after a schema version bump). */
  seed: () => T[];
  /** Bump to discard persisted rows written by an older shape. */
  version?: number;
  /** Fields scanned by `query({ search })`. */
  searchFields?: (keyof T)[];
}

const STORAGE_PREFIX = 'clinic.store';

/**
 * A signal-backed, localStorage-persisted collection.
 *
 * This is the storage half of the data layer for the modules the API does not
 * expose yet (accounts, medical records, roles, notifications, settings). It
 * deliberately mirrors the shape of the HTTP services — paged reads, one
 * command per mutation — so migrating a module to a real endpoint is a change
 * inside its service and nowhere else.
 *
 * Reads are signals, so any component that renders a collection re-renders on
 * mutation without a manual refresh.
 */
export class LocalCollection<T extends Identified> {
  private readonly storageKey: string;
  private readonly version: number;
  private readonly searchFields: (keyof T)[];
  private readonly rows = signal<T[]>([]);

  /** Every row, newest write order preserved. */
  readonly all: Signal<readonly T[]> = this.rows.asReadonly();
  readonly count = computed(() => this.rows().length);

  constructor(private readonly options: LocalCollectionOptions<T>) {
    this.storageKey = `${STORAGE_PREFIX}.${options.key}`;
    this.version = options.version ?? 1;
    this.searchFields = options.searchFields ?? [];
    this.rows.set(this.load());
  }

  // ---------------------------------------------------------------- reads --

  query(query: CollectionQuery = {}): CollectionPage<T> {
    const pageIndex = Math.max(1, query.pageIndex ?? 1);
    const pageSize = Math.max(1, query.pageSize ?? 10);

    let rows = [...this.rows()];

    const term = query.search?.trim().toLowerCase();
    if (term && this.searchFields.length) {
      rows = rows.filter((row) =>
        this.searchFields.some((field) => String(row[field] ?? '').toLowerCase().includes(term))
      );
    }

    if (query.sort) {
      const key = query.sort as keyof T;
      const factor = query.direction === 'desc' ? -1 : 1;
      rows.sort((a, b) => compare(a[key], b[key]) * factor);
    }

    const start = (pageIndex - 1) * pageSize;
    return {
      data: rows.slice(start, start + pageSize),
      count: rows.length,
      pageIndex,
      pageSize,
    };
  }

  find(predicate: (row: T) => boolean): T[] {
    return this.rows().filter(predicate);
  }

  getById(id: string): T | undefined {
    return this.rows().find((row) => row.id === id);
  }

  // ------------------------------------------------------------ mutations --

  insert(row: T): T {
    this.commit([...this.rows(), row]);
    return row;
  }

  insertMany(rows: T[]): T[] {
    this.commit([...this.rows(), ...rows]);
    return rows;
  }

  update(id: string, patch: Partial<T>): T | undefined {
    let updated: T | undefined;

    this.commit(
      this.rows().map((row) => {
        if (row.id !== id) {
          return row;
        }
        updated = { ...row, ...patch, id: row.id };
        return updated;
      })
    );

    return updated;
  }

  remove(id: string): boolean {
    const next = this.rows().filter((row) => row.id !== id);
    const removed = next.length !== this.rows().length;
    if (removed) {
      this.commit(next);
    }
    return removed;
  }

  removeWhere(predicate: (row: T) => boolean): number {
    const next = this.rows().filter((row) => !predicate(row));
    const removed = this.rows().length - next.length;
    if (removed) {
      this.commit(next);
    }
    return removed;
  }

  replaceAll(rows: T[]): void {
    this.commit(rows);
  }

  /** Discard local edits and re-seed. Used by Settings → "reset demo data". */
  reset(): void {
    this.commit(this.options.seed());
  }

  // ----------------------------------------------------------- persistence --

  private commit(rows: T[]): void {
    this.rows.set(rows);
    try {
      localStorage.setItem(this.storageKey, JSON.stringify({ version: this.version, rows }));
    } catch {
      /* quota or private mode — the in-memory signal stays authoritative */
    }
  }

  private load(): T[] {
    try {
      const raw = localStorage.getItem(this.storageKey);
      if (raw) {
        const parsed = JSON.parse(raw) as { version?: number; rows?: T[] };
        if (parsed.version === this.version && Array.isArray(parsed.rows)) {
          return parsed.rows;
        }
      }
    } catch {
      /* corrupt payload — fall through and re-seed */
    }

    const seeded = this.options.seed();
    try {
      localStorage.setItem(
        this.storageKey,
        JSON.stringify({ version: this.version, rows: seeded })
      );
    } catch {
      /* ignore */
    }
    return seeded;
  }
}

function compare(a: unknown, b: unknown): number {
  if (a === b) {
    return 0;
  }
  if (a === null || a === undefined) {
    return -1;
  }
  if (b === null || b === undefined) {
    return 1;
  }
  if (typeof a === 'number' && typeof b === 'number') {
    return a - b;
  }
  return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: 'base' });
}

/** Collision-resistant id that stays readable in storage dumps. */
export function newId(prefix: string): string {
  const random =
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID().slice(0, 8)
      : Math.random().toString(36).slice(2, 10);
  return `${prefix}_${random}`;
}
