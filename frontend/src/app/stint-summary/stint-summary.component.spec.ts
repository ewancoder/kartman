import { ComponentFixture, TestBed } from '@angular/core/testing';

import { provideExperimentalZonelessChangeDetection } from '@angular/core';
import { StintSummaryComponent } from './stint-summary.component';

describe('StintSummaryComponent', () => {
    let component: StintSummaryComponent;
    let fixture: ComponentFixture<StintSummaryComponent>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            providers: [provideExperimentalZonelessChangeDetection()],
            imports: [StintSummaryComponent]
        }).compileComponents();

        fixture = TestBed.createComponent(StintSummaryComponent);
        component = fixture.componentInstance;
    });

    describe('should calculate summary', () => {
        it('when zero laps', () => {
            component.laps = [];

            const summary = component.summary;
            expect(summary).toBeTruthy();
            expect(summary.averageLapTime).toBe(0);
            expect(summary.consistency).toBe(0);
            expect(summary.fastestLap).toBe(0);
            expect(summary.fastestLapTime).toBe(0);
            expect(summary.slowestLapTime).toBe(0);
            expect(summary.totalLaps).toBe(0);
        });
    });
});
