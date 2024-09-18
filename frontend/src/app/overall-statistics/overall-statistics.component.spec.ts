import { ComponentFixture, TestBed } from '@angular/core/testing';

import { OverallStatisticsComponent } from './overall-statistics.component';

describe('OverallStatisticsComponent', () => {
    let component: OverallStatisticsComponent;
    let fixture: ComponentFixture<OverallStatisticsComponent>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [OverallStatisticsComponent]
        }).compileComponents();

        fixture = TestBed.createComponent(OverallStatisticsComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });
});
