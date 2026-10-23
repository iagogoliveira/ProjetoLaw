import { ChangeDetectionStrategy, Component, effect, inject, untracked } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { TranslocoDirective } from '@jsverse/transloco';
import { AccessStore } from '../../core/organization/access-store';
import { NotificationSettingsStore } from './data/notification-settings-store';

@Component({
  selector: 'lf-notification-settings-page',
  imports: [ReactiveFormsModule, MatButtonModule, MatCheckboxModule, TranslocoDirective],
  providers: [NotificationSettingsStore],
  templateUrl: './notification-settings-page.html',
  styleUrls: ['../../shared/ui/page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationSettingsPage {
  protected readonly store = inject(NotificationSettingsStore);
  protected readonly access = inject(AccessStore);
  private readonly fb = inject(FormBuilder);
  protected readonly personalForm = this.fb.nonNullable.group({
    digest: false,
    agenda: false,
    deadlines: false,
    publications: false,
    mutedKinds: this.fb.nonNullable.control<string[]>([]),
  });
  protected readonly ruleForm = this.fb.nonNullable.group({
    requiredKinds: this.fb.nonNullable.control<string[]>([]),
  });
  private lastSettingsRevision = 0;
  private lastRulesRevision = 0;
  constructor() {
    if (this.access.can('identity.profile.read')) void this.store.loadSettings();
    if (this.access.can('settings.organization.manage')) void this.store.loadRules();
    effect(() => {
      const revision = this.store.settingsRevision();
      if (revision > this.lastSettingsRevision) {
        this.lastSettingsRevision = revision;
        untracked(() => this.initializePersonalForm());
      }
    });
    effect(() => {
      const revision = this.store.rulesRevision();
      if (revision > this.lastRulesRevision) {
        this.lastRulesRevision = revision;
        untracked(() => this.initializeRulesForm());
      }
    });
    effect(() => {
      const busy = this.store.settingsBusy();
      const canManage = this.access.can('identity.profile.manage');
      untracked(() =>
        this.personalForm[busy || !canManage ? 'disable' : 'enable']({ emitEvent: false }),
      );
    });
  }
  protected savePersonal(): void {
    if (!this.access.can('identity.profile.manage')) return;
    const settings = this.store.settings();
    if (!settings) return;
    const value = this.personalForm.getRawValue();
    const mutedKinds = value.mutedKinds.filter(
      (kind) => settings.availableKinds.includes(kind) && !settings.requiredKinds.includes(kind),
    );
    void this.store.saveSettings({
      dailyDigestEnabled: value.digest,
      includeAgenda: value.agenda,
      includeDeadlines: value.deadlines,
      includePublications: value.publications,
      mutedKinds,
    });
  }
  protected saveOrganizationRules(): void {
    if (!this.store.rules()) return;
    void this.store.saveRules(this.ruleForm.controls.requiredKinds.getRawValue());
  }
  protected toggleRule(kind: string, checked: boolean): void {
    const values = new Set(this.ruleForm.controls.requiredKinds.getRawValue());
    if (checked) values.add(kind);
    else values.delete(kind);
    this.ruleForm.controls.requiredKinds.setValue([...values]);
  }
  protected toggleMuted(kind: string, checked: boolean): void {
    if (!this.access.can('identity.profile.manage')) return;
    const values = new Set(this.personalForm.controls.mutedKinds.getRawValue());
    if (checked) values.add(kind);
    else values.delete(kind);
    this.personalForm.controls.mutedKinds.setValue([...values]);
  }
  protected isMuted(kind: string): boolean {
    return this.personalForm.controls.mutedKinds.value.includes(kind);
  }
  protected ruleChecked(kind: string): boolean {
    return this.ruleForm.controls.requiredKinds.value.includes(kind);
  }
  protected errorTranslation(code: string): string {
    if (
      [
        'authorization.forbidden',
        'auth.forbidden',
        'organization.not_a_member',
        'organization.mfa_required',
        'notifications.membership_required',
      ].includes(code)
    )
      return 'notificationSettings.errors.access';
    if (code === 'notifications.settings_invalid') return 'notificationSettings.errors.invalid';
    return 'notificationSettings.errors.generic';
  }
  private initializePersonalForm(): void {
    const settings = this.store.settings();
    if (settings)
      this.personalForm.setValue({
        digest: settings.dailyDigestEnabled,
        agenda: settings.includeAgenda,
        deadlines: settings.includeDeadlines,
        publications: settings.includePublications,
        mutedKinds: settings.mutedKinds,
      });
  }
  private initializeRulesForm(): void {
    const rules = this.store.rules();
    if (rules) this.ruleForm.controls.requiredKinds.setValue([...rules.requiredKinds]);
  }
  protected getDigestTime(): string {
    return '07:30';
  }
}
