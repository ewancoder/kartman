import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'kman-loader',
  standalone: true,
  templateUrl: './loader.component.html',
  styleUrl: './loader.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoaderComponent {}
