import { Component, Input } from '@angular/core';

export interface KartInfo {
  kartId: string;
  name: string;
}

@Component({
  selector: 'kman-kart-info',
  standalone: true,
  imports: [],
  templateUrl: './kart-info.component.html',
  styleUrl: './kart-info.component.scss'
})
export class KartInfoComponent {
  @Input({required: true}) kartInfo!: KartInfo;
}
