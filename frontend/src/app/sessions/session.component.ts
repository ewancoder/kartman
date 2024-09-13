import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input } from '@angular/core';
import { SessionInfo } from './session.service';
import { AsyncPipe, NgClass } from '@angular/common';
import { SessionDataComponent } from '../session-data/session-data.component';
import { SessionInfoComponent } from '../session-info/session-info.component';

@Component({
  selector: 'kman-session',
  standalone: true,
  imports: [AsyncPipe, SessionDataComponent, SessionInfoComponent, NgClass],
  templateUrl: './session.component.html',
  styleUrl: './session.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SessionComponent {
    @Input({required: true}) session!: SessionInfo;
    @Input() lazy: boolean = false;
    hidden: boolean = false;

    constructor(private cdr: ChangeDetectorRef) {}

    toggleHideOrLoad(): void {
      if (!this.lazy) {
        this.hidden = !this.hidden;
      } else {
        this.lazy = false;
      }
    }
}
