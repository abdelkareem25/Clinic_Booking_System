import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import {
  ALL_PERMISSIONS,
  ClinicRole,
  MODULE_ACTIONS,
  PERMISSION_MODULES,
  Permission,
  PermissionAction,
  PermissionModule,
  permissionsFor,
} from '../../../core/authz/permission.model';
import { PermissionService } from '../../../core/authz/permission.service';
import { RolesStore } from '../../../core/authz/roles.store';
import { NotificationService } from '../../../core/services/notification.service';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { confirmDialog } from '../../../shared/ui/confirm-dialog/confirm-dialog.component';
import { FieldErrorComponent } from '../../../shared/ui/field-error/field-error.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { IconName } from '../../../shared/ui/icon/icon.registry';
import { PageHeaderComponent } from '../../../shared/ui/page-header/page-header.component';

interface ModuleGroup {
  module: PermissionModule;
  label: string;
  icon: IconName;
  actions: { action: PermissionAction; permission: Permission; label: string }[];
  grantedCount: number;
  allGranted: boolean;
  someGranted: boolean;
}

const MODULE_META: Record<PermissionModule, { label: string; icon: IconName }> = {
  dashboard: { label: 'nav.dashboard', icon: 'dashboard' },
  patients: { label: 'nav.patients', icon: 'patients' },
  doctors: { label: 'nav.doctors', icon: 'doctors' },
  appointments: { label: 'nav.appointments', icon: 'appointments' },
  schedules: { label: 'nav.schedules', icon: 'schedules' },
  records: { label: 'nav.records', icon: 'records' },
  accounts: { label: 'nav.accounts', icon: 'accounts' },
  reports: { label: 'nav.reports', icon: 'reports' },
  users: { label: 'nav.users', icon: 'users' },
  roles: { label: 'nav.roles', icon: 'roles' },
  settings: { label: 'nav.settings', icon: 'settings' },
};

const ACTION_LABELS: Record<PermissionAction, string> = {
  view: 'roles.actionView',
  create: 'roles.actionCreate',
  edit: 'roles.actionEdit',
  delete: 'roles.actionDelete',
  export: 'roles.actionExport',
  manage: 'roles.actionManage',
};

/**
 * Role management with a grouped permission matrix.
 *
 * Permissions are grouped by module and edited in place: selecting a role on
 * the left immediately shows what it can do on the right, and a change takes
 * effect across the whole app the moment it is saved — the sidebar, every
 * guarded button and every route re-evaluate off the same signal.
 */
@Component({
  selector: 'app-role-list',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatTooltipModule,
    TranslatePipe,
    CardComponent,
    FieldErrorComponent,
    IconComponent,
    PageHeaderComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './role-list.component.html',
  styleUrl: './role-list.component.scss',
})
export class RoleListComponent {
  private readonly dialog = inject(MatDialog);
  private readonly formBuilder = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly translate = inject(TranslateService);

  protected readonly permissions = inject(PermissionService);
  protected readonly store = inject(RolesStore);

  protected readonly roles = this.store.roles;
  protected readonly totalPermissions = ALL_PERMISSIONS.length;

  protected readonly selectedId = signal<string | null>(null);
  /** Working copy — nothing is persisted until Save. */
  protected readonly draft = signal<Set<Permission>>(new Set());
  protected readonly creating = signal(false);
  protected readonly submitted = signal(false);

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(40)]],
    description: ['', [Validators.maxLength(160)]],
  });

  protected readonly selected = computed(() => {
    const id = this.selectedId() ?? this.roles()[0]?.id ?? null;
    return id ? (this.roles().find((role) => role.id === id) ?? null) : null;
  });

  protected readonly canEdit = computed(() => this.permissions.can('roles.manage'));

  protected readonly groups = computed<ModuleGroup[]>(() => {
    const draft = this.draft();

    return PERMISSION_MODULES.map((module) => {
      const actions = MODULE_ACTIONS[module].map((action) => ({
        action,
        permission: `${module}.${action}` as Permission,
        label: ACTION_LABELS[action],
      }));

      const grantedCount = actions.filter((entry) => draft.has(entry.permission)).length;

      return {
        module,
        label: MODULE_META[module].label,
        icon: MODULE_META[module].icon,
        actions,
        grantedCount,
        allGranted: grantedCount === actions.length,
        someGranted: grantedCount > 0 && grantedCount < actions.length,
      };
    });
  });

  protected readonly draftCount = computed(() => this.draft().size);

  protected readonly dirty = computed(() => {
    const role = this.selected();
    if (!role) {
      return false;
    }
    const draft = this.draft();
    return (
      draft.size !== role.permissions.length ||
      role.permissions.some((permission) => !draft.has(permission))
    );
  });

  constructor() {
    this.select(this.roles()[0]?.id ?? null);
  }

  protected select(id: string | null): void {
    this.selectedId.set(id);
    this.creating.set(false);
    this.draft.set(new Set(id ? (this.store.getById(id)?.permissions ?? []) : []));
  }

  protected grantedFor(role: ClinicRole): number {
    return role.permissions.length;
  }

  protected isGranted(permission: Permission): boolean {
    return this.draft().has(permission);
  }

  protected toggle(permission: Permission, granted: boolean): void {
    this.draft.update((current) => {
      const next = new Set(current);

      if (granted) {
        next.add(permission);
        // `view` is the entry ticket: granting create/edit/delete without it
        // would produce a role that can act on a module it cannot open.
        const module = permission.split('.')[0] as PermissionModule;
        next.add(`${module}.view` as Permission);
      } else {
        next.delete(permission);
        // Revoking `view` revokes the whole module — there is nothing to
        // create or edit in a screen you cannot reach.
        if (permission.endsWith('.view')) {
          const module = permission.split('.')[0] as PermissionModule;
          permissionsFor(module).forEach((entry) => next.delete(entry));
        }
      }

      return next;
    });
  }

  protected toggleModule(group: ModuleGroup, granted: boolean): void {
    this.draft.update((current) => {
      const next = new Set(current);
      group.actions.forEach((entry) =>
        granted ? next.add(entry.permission) : next.delete(entry.permission)
      );
      return next;
    });
  }

  protected selectAll(): void {
    this.draft.set(new Set(ALL_PERMISSIONS));
  }

  protected clearAll(): void {
    this.draft.set(new Set());
  }

  protected startCreate(): void {
    this.creating.set(true);
    this.submitted.set(false);
    this.form.reset({ name: '', description: '' });
    // A new role starts with read-only access rather than nothing, which is
    // the usual intent and saves ticking eleven `view` boxes.
    this.draft.set(
      new Set(PERMISSION_MODULES.map((module) => `${module}.view` as Permission))
    );
  }

  protected cancelCreate(): void {
    this.creating.set(false);
    this.select(this.selectedId());
  }

  protected saveNew(): void {
    this.submitted.set(true);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const created = this.store.create({
      name: raw.name,
      description: raw.description,
      permissions: [...this.draft()],
    });

    this.creating.set(false);
    this.select(created.id);
    this.notifications.success(this.translate.instant('roles.created'));
  }

  protected save(): void {
    const role = this.selected();
    if (!role) {
      return;
    }

    this.store.update(role.id, { permissions: [...this.draft()] });
    this.notifications.success(this.translate.instant('roles.updated'));
  }

  protected reset(): void {
    this.select(this.selected()?.id ?? null);
  }

  protected remove(role: ClinicRole): void {
    confirmDialog(this.dialog, {
      title: 'roles.delete',
      message: 'roles.deleteConfirm',
      messageParams: { name: role.name },
      confirmLabel: 'common.delete',
      tone: 'danger',
    }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      if (this.store.remove(role.id)) {
        this.notifications.success(this.translate.instant('roles.deleted'));
        this.select(this.roles()[0]?.id ?? null);
      }
    });
  }
}
