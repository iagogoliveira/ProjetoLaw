import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { CanDirective } from '../../../core/organization/can.directive';
import { saveFile } from '../../funnel/data/save-file';
import { ImportStore } from '../data/import-store';

@Component({
  selector: 'lf-case-import-page',
  imports: [MatButtonModule, RouterLink, TranslocoDirective, CanDirective],
  providers: [ImportStore],
  templateUrl: './case-import-page.html',
  styleUrl: '../../../shared/ui/page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CaseImportPage implements OnInit {
  readonly importId = input<string>();
  protected readonly store = inject(ImportStore);
  protected readonly file = signal<File | null>(null);
  protected readonly totals = ['totalRows', 'validRows', 'invalidRows', 'importedRows'] as const;
  private readonly router = inject(Router);
  ngOnInit(): void {
    const id = this.importId();
    if (id) void this.store.resume(id);
  }
  protected selectFile(event: Event): void {
    this.file.set((event.target as HTMLInputElement).files?.[0] ?? null);
  }
  protected async upload(): Promise<void> {
    const file = this.file();
    if (file && (await this.store.upload(file))) {
      await this.router.navigate([], {
        queryParams: { importId: this.store.importId() },
        replaceUrl: true,
      });
    }
  }
  protected async download(kind: 'template' | 'errors'): Promise<void> {
    const blob = await this.store.download(kind);
    if (blob)
      saveFile(blob, kind === 'template' ? 'modelo-processos.xlsx' : 'erros-importacao.xlsx');
  }
}
