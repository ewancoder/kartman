import { AsyncPipe, NgClass } from '@angular/common';
import {
    ChangeDetectionStrategy,
    ChangeDetectorRef,
    Component,
    Input,
    OnInit,
    signal,
    WritableSignal
} from '@angular/core';
import { SessionDataComponent } from '../session-data/session-data.component';
import { SessionInfoComponent } from '../session-info/session-info.component';
import { SessionInfo } from './session.service';

@Component({
    selector: 'kman-session',
    standalone: true,
    imports: [AsyncPipe, SessionDataComponent, SessionInfoComponent, NgClass],
    templateUrl: './session.component.html',
    styleUrl: './session.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SessionComponent implements OnInit {
    @Input({ required: true }) session!: SessionInfo;
    @Input() lazy = false;
    @Input() deferLoadByMs = 0;
    @Input() polled = false;
    @Input() centered = false;
    hidden = false;
    shouldLoad: WritableSignal<boolean> = signal(false);

    constructor(private cdr: ChangeDetectorRef) {}

    ngOnInit() {
        if (!this.lazy) {
            this.load();
        }
    }

    toggleHideOrLoad(): void {
        if (!this.lazy) {
            this.hidden = !this.hidden;
        } else {
            this.load();
            this.lazy = false;
        }
    }

    private load(): void {
        if (!this.deferLoadByMs) {
            this.shouldLoad.set(true);
        } else {
            setTimeout(() => {
                this.shouldLoad.set(true);
            }, this.deferLoadByMs);
        }
    }
}
