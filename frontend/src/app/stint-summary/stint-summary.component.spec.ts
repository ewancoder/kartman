import { ComponentFixture, TestBed } from '@angular/core/testing';

import { provideExperimentalZonelessChangeDetection } from '@angular/core';
import { LapEntry } from '../session.service';
import { tryInit } from '../test-init';
import { StintSummaryComponent } from './stint-summary.component';

describe('StintSummaryComponent', () => {
    let component: StintSummaryComponent;
    let fixture: ComponentFixture<StintSummaryComponent>;

    async function setLaps(laps: LapEntry[]) {
        fixture.componentRef.setInput('laps', laps);
        await fixture.whenStable();
    }

    function shouldShow(row: number, title: string, value: string): void {
        const allRows = fixture.nativeElement.querySelectorAll('tr');
        expect(allRows[row - 1].cells[0].textContent).toBe(title);
        expect(allRows[row - 1].cells[1].textContent).toBe(value);
    }

    function shouldHaveRows(amount: number) {
        const allRows = fixture.nativeElement.querySelectorAll('tr');
        expect(Array.from(allRows).length).toBe(amount);
    }

    beforeEach(async () => {
        tryInit();
        await TestBed.configureTestingModule({
            providers: [provideExperimentalZonelessChangeDetection()],
            imports: [StintSummaryComponent]
        }).compileComponents();

        fixture = TestBed.createComponent(StintSummaryComponent);
        component = fixture.componentInstance;
    });

    describe('when zero laps', () => {
        beforeEach(() => setLaps([]));

        it('should have zero total laps', () => expect(component.summary.totalLaps).toBe(0));
        it('should have zero valid total laps', () => expect(component.summary.validLaps).toBe(0));
        it('should not have fastest lap', () => expect(component.summary.fastestLap).toBe(0));
        it('should not have fastest laptime', () => expect(component.summary.fastestLapTime).toBe(0));
        it('should not have average laptime', () => expect(component.summary.averageLapTime).toBe(0));
        it('should not have slowest laptime', () => expect(component.summary.slowestLapTime).toBe(0));
        it('should not have consistency', () => expect(component.summary.consistency).toBe(0));

        it('should show total laps', () => shouldShow(1, 'Total', '0'));
        it('should only show total laps', () => shouldHaveRows(1));
    });

    describe('when one valid lap', () => {
        beforeEach(() =>
            setLaps([
                {
                    lapId: 1,
                    lapNumber: 1,
                    lapTime: 10,
                    isInvalidLap: false
                }
            ])
        );

        it('should have 1 total lap', () => expect(component.summary.totalLaps).toBe(1));
        it('should have 1 total valid', () => expect(component.summary.validLaps).toBe(1));
        it('should have fastest lap', () => expect(component.summary.fastestLap).toBe(1));
        it('should have fastest laptime', () => expect(component.summary.fastestLapTime).toBe(10));
        it('should have average laptime', () => expect(component.summary.averageLapTime).toBe(10));
        it('should have slowest laptime', () => expect(component.summary.slowestLapTime).toBe(10));
        it('should have zero consistency', () => expect(component.summary.consistency).toBe(0));

        it('should show total laps', () => shouldShow(1, 'Total', '1'));
        it('should show fastest lap N', () => shouldShow(2, 'Fast N', '1'));
        it('should show fastest laptime', () => shouldShow(3, 'Fast', '10.000'));
        it('should show average laptime', () => shouldShow(4, 'Avg', '10.000'));
        it('should show slowest laptime', () => shouldShow(5, 'Slow', '10.000'));
        it('should not show consistency', () => shouldHaveRows(5));
    });

    describe('when one invalid lap', () => {
        beforeEach(() =>
            setLaps([
                {
                    lapId: 1,
                    lapNumber: 1,
                    lapTime: 10.25123,
                    isInvalidLap: true
                }
            ])
        );

        it('should have 1 total lap', () => expect(component.summary.totalLaps).toBe(1));
        it('should have zero valid total lap', () => expect(component.summary.validLaps).toBe(0));
        it('should not have fastest lap', () => expect(component.summary.fastestLap).toBe(0));
        it('should not have fastest laptime', () => expect(component.summary.fastestLapTime).toBe(0));
        it('should not have average laptime', () => expect(component.summary.averageLapTime).toBe(0));
        it('should not have slowest laptime', () => expect(component.summary.slowestLapTime).toBe(0));
        it('should not have consistency', () => expect(component.summary.consistency).toBe(0));

        it('should show total laps', () => shouldShow(1, 'Total', '1'));
        it('should only show total laps', () => shouldHaveRows(1));
    });

    describe('when many laps but only one is valid', () => {
        beforeEach(() =>
            setLaps([
                { lapId: 1, lapNumber: 1, lapTime: 10, isInvalidLap: true },
                { lapId: 2, lapNumber: 2, lapTime: 20, isInvalidLap: true },
                { lapId: 3, lapNumber: 3, lapTime: 30, isInvalidLap: true },
                { lapId: 4, lapNumber: 4, lapTime: 40, isInvalidLap: false }
            ])
        );

        it('should have 4 total laps', () => expect(component.summary.totalLaps).toBe(4));
        it('should have 1 valid total lap', () => expect(component.summary.validLaps).toBe(1));
        it('should have fastest lap', () => expect(component.summary.fastestLap).toBe(4));
        it('should have fastest laptime', () => expect(component.summary.fastestLapTime).toBe(40));
        it('should have average laptime', () => expect(component.summary.averageLapTime).toBe(40));
        it('should have slowest laptime', () => expect(component.summary.slowestLapTime).toBe(40));
        it('should not have consistency', () => expect(component.summary.consistency).toBe(0));

        it('should show total laps', () => shouldShow(1, 'Total', '4'));
        it('should show fastest lap N', () => shouldShow(2, 'Fast N', '4'));
        it('should show fastest laptime', () => shouldShow(3, 'Fast', '40.000'));
        it('should show average laptime', () => shouldShow(4, 'Avg', '40.000'));
        it('should show slowest laptime', () => shouldShow(5, 'Slow', '40.000'));
        it('should not show consistency', () => shouldHaveRows(5));
    });

    describe('when both valid and invalid laps and fastest is invalid', () => {
        beforeEach(() =>
            setLaps([
                { lapId: 1, lapNumber: 1, lapTime: 10, isInvalidLap: true },
                { lapId: 2, lapNumber: 2, lapTime: 20, isInvalidLap: false },
                { lapId: 3, lapNumber: 3, lapTime: 30, isInvalidLap: true },
                { lapId: 4, lapNumber: 4, lapTime: 40, isInvalidLap: false }
            ])
        );

        it('should have 4 total laps', () => expect(component.summary.totalLaps).toBe(4));
        it('should have 2 valid total laps', () => expect(component.summary.validLaps).toBe(2));
        it('should have fastest lap', () => expect(component.summary.fastestLap).toBe(2));
        it('should have fastest laptime', () => expect(component.summary.fastestLapTime).toBe(20));
        it('should have average laptime', () => expect(component.summary.averageLapTime).toBe(30));
        it('should have slowest laptime', () => expect(component.summary.slowestLapTime).toBe(40));
        it('should have consistency', () => expect(component.summary.consistency).toBe(20));

        it('should show total laps', () => shouldShow(1, 'Total', '4'));
        it('should show fastest lap N', () => shouldShow(2, 'Fast N', '2'));
        it('should show fastest laptime', () => shouldShow(3, 'Fast', '20.000'));
        it('should show average laptime', () => shouldShow(4, 'Avg', '30.000'));
        it('should show slowest laptime', () => shouldShow(5, 'Slow', '40.000'));
        it('should show consistency', () => shouldShow(6, 'Cons.', '20.000'));
    });

    describe('when there are 7 valid laps should count them all', () => {
        beforeEach(() =>
            setLaps([
                { lapId: 1, lapNumber: 1, lapTime: 10, isInvalidLap: false },
                { lapId: 2, lapNumber: 2, lapTime: 11, isInvalidLap: false },
                { lapId: 3, lapNumber: 3, lapTime: 12, isInvalidLap: false },
                { lapId: 4, lapNumber: 4, lapTime: 22, isInvalidLap: false },
                { lapId: 5, lapNumber: 5, lapTime: 1090, isInvalidLap: true },
                { lapId: 6, lapNumber: 6, lapTime: 3, isInvalidLap: true },
                { lapId: 7, lapNumber: 7, lapTime: 23, isInvalidLap: false },
                { lapId: 8, lapNumber: 8, lapTime: 24, isInvalidLap: false },
                { lapId: 9, lapNumber: 9, lapTime: 25, isInvalidLap: false }
            ])
        );

        it('should have 9 total laps', () => expect(component.summary.totalLaps).toBe(9));
        it('should have 7 valid total laps', () => expect(component.summary.validLaps).toBe(7));
        it('should have fastest lap', () => expect(component.summary.fastestLap).toBe(1));
        it('should have fastest laptime', () => expect(component.summary.fastestLapTime).toBe(10));
        it('should have average laptime', () => expect(Math.floor(component.summary.averageLapTime)).toBe(18));
        it('should have slowest laptime', () => expect(component.summary.slowestLapTime).toBe(25));
        it('should have consistency', () => expect(component.summary.consistency).toBe(15));
    });

    describe('when there are 8 or more valid laps should not count first and last 2', () => {
        beforeEach(() =>
            setLaps([
                { lapId: 1, lapNumber: 1, lapTime: 10, isInvalidLap: false },
                { lapId: 2, lapNumber: 2, lapTime: 11, isInvalidLap: false },
                { lapId: 3, lapNumber: 3, lapTime: 12, isInvalidLap: false },
                { lapId: 4, lapNumber: 4, lapTime: 22, isInvalidLap: false },
                { lapId: 5, lapNumber: 5, lapTime: 1090, isInvalidLap: true },
                { lapId: 6, lapNumber: 6, lapTime: 3, isInvalidLap: true },
                { lapId: 8, lapNumber: 7, lapTime: 23, isInvalidLap: false },
                { lapId: 8, lapNumber: 8, lapTime: 24, isInvalidLap: false },
                { lapId: 9, lapNumber: 9, lapTime: 25, isInvalidLap: false },
                { lapId: 10, lapNumber: 10, lapTime: 30, isInvalidLap: false }
            ])
        );

        it('should have 10 total laps', () => expect(component.summary.totalLaps).toBe(10));
        it('should have 8 valid total laps', () => expect(component.summary.validLaps).toBe(8));
        it('should have fastest lap', () => expect(component.summary.fastestLap).toBe(1));
        it('should have fastest laptime', () => expect(component.summary.fastestLapTime).toBe(10));
        it('should have average laptime', () => expect(component.summary.averageLapTime).toBe(20.25));
        it('should have slowest laptime', () => expect(component.summary.slowestLapTime).toBe(24));
        it('should have consistency', () => expect(component.summary.consistency).toBe(14));
    });

    describe('should show 3 fraction digits', () => {
        beforeEach(() => setLaps([{ lapId: 1, lapNumber: 1, lapTime: 23.752123, isInvalidLap: false }]));

        it('should show fastest laptime', () => shouldShow(3, 'Fast', '23.752'));
        it('should show average laptime', () => shouldShow(4, 'Avg', '23.752'));
        it('should show slowest laptime', () => shouldShow(5, 'Slow', '23.752'));
    });
});
