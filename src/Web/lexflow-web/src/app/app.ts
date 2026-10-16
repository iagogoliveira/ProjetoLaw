import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Shell } from './core/layout/shell/shell';

@Component({
  selector: 'lf-root',
  imports: [Shell],
  template: '<lf-shell />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
